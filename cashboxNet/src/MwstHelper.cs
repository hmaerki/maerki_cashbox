using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace cashboxNet
{
    public interface IMwstAbrechnung
    {
        void Abrechnung(Journal journal, BookkeepingBook book);
        decimal CalculateBetragMwst(Entry entry);
    }
}

namespace cashboxNet.MWST
{
    public class KontoWrapper
    {
        public decimal Saldo { get; private set; }
        public int KontoNr { get { ValidateIfDefined(); return konto.KontoNr; } }

        /// <summary>
        /// 4'751.92 2250 Geschuldete MWST Satz 1 (3.7%)
        /// </summary>
        public string Text { get { return $"{N.F(Saldo)} {konto.KontoNr} {konto.Text}"; } }

        public Konto Konto { get { ValidateIfDefined(); return konto; } }
        private Konto konto = null;
        private string propertyName;
        public KontoWrapper(string propertyName_)
        {
            propertyName = propertyName_;
        }

        public void SetKontoNr(Configuration config, int kontoNr)
        {
            konto = config.FindKonto(kontoNr);
        }

        public void ValidateIfDefined()
        {
            if (konto == null)
            {
                throw new CashboxException($"MWST-Konfiguration nicht vollständig: '{propertyName}' muss definiert sein.");
            }
        }

        public void UpdateSaldo(BookkeepingBook book, TValuta date)
        {
            BookkeepingAccount account = book[konto];
            Saldo = account.GetSaldo(date);
        }

        public void UpdateSaldoEndeMinusAnfang(BookkeepingBook book, TValuta date, TValuta startDate)
        {
            BookkeepingAccount account = book[konto];
            decimal startSaldo = account.GetSaldo(startDate);
            decimal endSaldo = account.GetSaldo(date);
            Saldo = endSaldo - startSaldo;
        }

        /*
        public string N0()
        {
          return Saldo.ToString("N0");
        }

        public string N2()
        {
          return Saldo.ToString("N2");
        }
        */
    }

    public class VorlageWrapper
    {
        private string propertyName;
        public Buchungsvorlage Vorlage { get { ValidateIfDefined(); return vorlage; } }
        private Buchungsvorlage vorlage = null;
        private Konto kontoHaben = null;

        public VorlageWrapper(string propertyName_)
        {
            propertyName = propertyName_;
        }
        public void Add(Configuration config, KontoWrapper kontoHaben, KontoWrapper kontoSoll, string vorlageText, string buchungsText)
        {
            Add(config, kontoHaben.Konto, kontoSoll.Konto, vorlageText, buchungsText);
        }
        public void Add(Configuration config, int kontoHaben_, KontoWrapper kontoSoll, string vorlageText, string buchungsText)
        {
            Konto kontoHaben = config.FindKonto(kontoHaben_);
            Add(config, kontoHaben, kontoSoll.Konto, vorlageText, buchungsText);
        }
        private void Add(Configuration config, Konto kontoHaben, Konto kontoSoll, string vorlageText, string buchungsText)
        {
            this.kontoHaben = kontoHaben;
            vorlage = new Buchungsvorlage(config, vorlageText, kontoHaben.KontoNr, kontoSoll.KontoNr, buchungsText: buchungsText);
            config.AddBuchungsvorlage(vorlage);
        }
        public void ValidateIfDefined()
        {
            if (vorlage == null)
            {
                throw new CashboxException($"MWST-Konfiguration nicht vollständig: '{propertyName}' muss definiert sein.");
            }
        }

        /// <summary>
        /// Verify if this 'Vorlage' is found in 'KontoHaben' on 'abrechnungsDatum'
        /// </summary>
        public bool TryBereitsAbgerechnetObsolete(AbrechnungsDatum abrechnungsDatum)
        {
            // Erkennen, ob ein MWST-Datum erreicht wurde
            if (kontoHaben.KontoDays.TryGetDay(abrechnungsDatum.Date, out KontoDay kontoDay))
            {
                foreach (AccountEntry accountEntry in kontoDay.EntriesOrdered)
                {
                    if (accountEntry.Entry.Buchungsvorlage == Vorlage)
                    {
                        // Bereits abgerechnet
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public abstract class MwstHelperBase : IMwstAbrechnung
    {
        protected const string MWST_ABRECHNUNG_INDEX = "zz";
        protected readonly Configuration config;
        public abstract decimal CalculateBetragMwst(Entry entry);

        /// <summary>
        /// cashbox_config_kontenplan.cs
        /// </summary>
        protected MwstHelperBase(Configuration config_)
        {
            config = config_;
            config.MwstAbrechnung = this;
        }

        protected abstract IEnumerable<AbrechnungsDatum> AbrechnungsDaten { get; }
        protected abstract AbrechnungImplementationBase AbrechnungFactory(BookkeepingBook book, AbrechnungsDatum abrechnungsDatum);

        protected Buchungsvorlage Add(int kontoHaben, int kontoSoll, string vorlageText, string mwst, string buchungsText)
        {
            Buchungsvorlage vorlage = new Buchungsvorlage(config, vorlageText, kontoHaben, kontoSoll, mwst: mwst, buchungsText: buchungsText);
            config.AddBuchungsvorlage(vorlage);
            return vorlage;
        }

        /// <summary>
        /// Add MWST-Satz
        /// </summary>
        public void Add(string tag)
        {
            config.MwstSaetze.Add(tag, null);
        }

        /// <summary>
        /// Add MWST-Satz
        /// </summary>
        public void Add(string tag, double mwst, int kontoNr, string text)
        {
            AddMwstSatz(tag, mwst, kontoNr, text);
        }

        protected void AddMwstSatz(string tag, double mwst, int kontoNr, string text)
        {
            Konto konto = config.FindKonto(kontoNr);

            config.MwstSaetze.Add(tag, new MwstSatz(tag, mwst, konto, text));
        }

        public void Abrechnung(Journal journal, BookkeepingBook book)
        {
            TValuta lastEntryValuta = journal.LastEntryValuta;
            foreach (AbrechnungsDatum abrechnungsDatum in AbrechnungsDaten)
            {
                if (lastEntryValuta < abrechnungsDatum.Date)
                {
                    // TODO: Remove comment
                    return;
                }
                AbrechnungImplementationBase i = AbrechnungFactory(book, abrechnungsDatum);
                i.Abrechnen();
            }
        }
    }

    public abstract class AbrechnungImplementationBase
    {
        protected const string SUBDIRECTORY = "mwst";
        protected readonly BookkeepingBook book;
        protected readonly AbrechnungsDatum abrechnungsDatum;
        private VorlageWrapper vorlageMwstZahlung;

        protected AbrechnungImplementationBase(BookkeepingBook book_, AbrechnungsDatum abrechnungsDatum_, VorlageWrapper vorlageMwstZahlung_)
        {
            book = book_;
            abrechnungsDatum = abrechnungsDatum_;
            vorlageMwstZahlung = vorlageMwstZahlung_;
        }

        protected void UpdateSaldo(KontoWrapper kontoSaldo)
        {
            kontoSaldo.UpdateSaldo(book, abrechnungsDatum.Date);
        }

        protected void UpdateSaldoEndeMinusAnfang(KontoWrapper kontoSaldo)
        {
            kontoSaldo.UpdateSaldoEndeMinusAnfang(book, abrechnungsDatum.Date, abrechnungsDatum.StartDate);
        }

        public abstract void Abrechnen(string filename);

        public void Abrechnen()
        {
            // Erkennen, ob ein MWST-Datum erreicht wurde
            if (vorlageMwstZahlung.TryBereitsAbgerechnetObsolete(abrechnungsDatum))
            {
                // Bereits abgerechnet
                // TODO: Remove comment
                // return;
            }

            // Erkennen, ob bereits eine Abrechnung durchgeführt wurde.
            // string referenz = journal.GetNextFreeReferenz(date);
            // string referenz = $"{date}{MWST_ABRECHNUNG_INDEX}";
            // string filename = $@"{SUBDIRECTORY}/{referenz} {RegexBeginReferenzVerbRest.VERB_FILEBUCHUNG} 47.11 {m.buchungsvorlageAbrechnung.VorlageText} MWST Abrechnung.txt";
            string filename = $@"{SUBDIRECTORY}/{abrechnungsDatum.Date} MWST Abrechnung {abrechnungsDatum.YearQuartalSemester}.txt";
            if (File.Exists(filename))
            {
                // TODO: Remove comment
                return;
            }

            if (!Directory.Exists(SUBDIRECTORY))
            {
                Directory.CreateDirectory(SUBDIRECTORY);
            }

            // Abrechnen
            Abrechnen(filename);
        }
    }

    public struct AbrechnungsDatum
    {
        public enum Duration { Quartal = 3, Semester = 6 };
        private readonly int startMonth;
        private readonly int endMonth;
        private readonly int endDay;
        public readonly string QuartalSemester;
        public readonly string YearQuartalSemester;
        public readonly TValuta Date;
        public readonly TValuta StartDate;
        public AbrechnungsDatum(Configuration config, string quartalsemester, Duration duration, int endMonth_, int endDay_)
        {
            QuartalSemester = quartalsemester;
            YearQuartalSemester = $"{config.DateStart.Year} {quartalsemester}";
            startMonth = endMonth_ - ((int)duration) + 1;
            Trace.Assert(startMonth > 0);
            endMonth = endMonth_;
            endDay = endDay_;
            StartDate = new TValuta(new DateTime(config.DateStart.Year, startMonth, 1));
            Date = new TValuta(new DateTime(config.DateStart.Year, endMonth, endDay));
        }

    }
}
