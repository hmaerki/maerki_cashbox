using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace cashboxNet
{
    public enum KontoType { AKTIVEN, PASSIVEN, EINNAHMEN, AUSGABEN };

    /// <summary>
    /// 1500 / Mobiliar und Einrichtungen
    /// </summary>
    public class Konto
    {
        /// <summary>
        /// From 'cashbox_config_kontenplan.cs'
        /// 
        /// GroupBilanzAktiven() / GroupBilanzPassiven() / ...
        /// </summary>
        public readonly KontoType KontoType;
        public readonly bool IsAktivenPassiven;
        public readonly bool IsEinnahmenAusgaben;
        /// <summary>
        /// Normalerweise ist: Saldo = Soll - Haben
        /// Hingegen f�r diese Konten ist: Saldo = Haben - Soll
        /// </summary>
        public readonly bool IsEinnahmenPassiven;

        /// <summary>
        /// From 'cashbox_config_kontenplan.cs'
        /// 
        /// 1500
        /// </summary>
        public readonly int KontoNr;

        /// <summary>
        /// From 'cashbox_config_kontenplan.cs'
        /// 
        /// Mobiliar und Einrichtungen
        /// </summary>
        public readonly string Text;

        /// <summary>
        /// From 'cashbox_config_jahr.cs'
        /// 
        /// 123.40
        /// </summary>
        public Decimal EroeffnungsKontostand { get; private set; }
        public void SetEroeffnungKontostand(decimal eroeffnungsKontostand_)
        {
            EroeffnungsKontostand = eroeffnungsKontostand_;
        }

        /// <summary>
        /// From 'cashbox_config_vorlagebuchungen.cs'
        /// 
        /// "privat"
        /// </summary>
        public Buchungsvorlage BuchungsvorlageNichtGefundenCredit = null;
        public Buchungsvorlage BuchungsvorlageNichtGefundenDebit = null;

        public KontoDays KontoDays { get; private set; }

        public Konto(Configuration config, KontoType kontoType, int konto, string text)
        {
            KontoType = kontoType;
            IsAktivenPassiven = (KontoType == KontoType.AKTIVEN) || (KontoType == KontoType.PASSIVEN);
            IsEinnahmenAusgaben = (KontoType == KontoType.EINNAHMEN) || (KontoType == KontoType.AUSGABEN);
            IsEinnahmenPassiven = (KontoType == KontoType.EINNAHMEN) || (KontoType == KontoType.PASSIVEN);
            KontoNr = konto;
            Text = text;
            KontoDays = null;
        }

        public void UpdateByJournalDays(Configuration config, JournalDays journalDays)
        {
            KontoDays = new KontoDays(config, journalDays, this);
        }

        /// <summary>
        /// Selects all entries belonging to this account
        /// </summary>
        public IEnumerable<Entry> WhereForThisKonto(IEnumerable<Entry> entries)
        {
            return entries.Where(e => (this == e.KontoHaben) || (this == e.KontoSoll));
        }
    }


    public class KontoDay : IDay
    {
        public TValuta Valuta { get; private set; }
        public Konto Konto { get; private set; }
        public Decimal Saldo { get; private set; }
        public List<AccountEntry> Entries = new List<AccountEntry>();
        public IEnumerable<AccountEntry> EntriesOrdered { get { return Entries.OrderBy(e => e.Entry.Referenz); } }
        public JournalDay JournalDay { get; private set; }
        public LazyStringList MessagesErrors = new LazyStringList();
        public void SetSaldo(Decimal saldo)
        {
            // May only be set once!
            Trace.Assert(Saldo == Decimal.MinValue);
            Saldo = saldo;
        }

        /// <summary>
        /// Collect the saldo at the end of each day.
        /// This saldo could be from the Bank-Account.
        /// This allows to write errors in the 'muh2'-file if the saldo differs.
        /// </summary>
        public Decimal SaldoExpected { get; private set; }

        public void SetSaldoExpected(decimal saldo)
        {
            // May only be set once!
            Trace.Assert(SaldoExpected == Decimal.MinValue);
            SaldoExpected = saldo;
        }

        public KontoDay(JournalDay journalDay, Konto konto, TValuta valuta)
        {
            JournalDay = journalDay;
            Konto = konto;
            Valuta = valuta;
            SaldoExpected = Decimal.MinValue;
            Saldo = Decimal.MinValue;
            journalDay.AddKontoDay(this);
        }
    }

    public class KontoDays : AbstractDays<KontoDay>
    {
        private Konto konto;
        private JournalDays journalDays;

        public KontoDays(Configuration config, JournalDays journalDays_, Konto konto_) : base(config)
        {
            konto = konto_;
            journalDays = journalDays_;
        }
        protected override KontoDay CreateDay(TValuta valuta)
        {
            return new KontoDay(journalDays.GetDay(valuta), konto, valuta);
        }
    }

}
