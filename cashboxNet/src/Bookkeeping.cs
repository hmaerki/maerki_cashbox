using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace cashboxNet
{
    public enum BookkeepingRelation { SOLL, HABEN };

    /// <summary>
    /// Repräsentiert eine Zeile im Kontenplan.
    /// Der 'Entry' gehört dem Journal. Wird ein Kommentar oder ein Fehler dem 'Entry' angefügt, so erscheint dies im muh2-File.
    /// </summary>
    public class AccountEntry
    {
        public BookkeepingRelation Relation { get; private set; }
        public bool IsMwst { get; private set; }

        /// <summary>
        /// Kapselung
        /// </summary>
        public string Referenz { get { return Entry.Referenz; } }

        /// <summary>
        /// Kapselung
        /// </summary>
        public string HtmlAnchor { get { return $"{Konto.KontoNr}_{Entry.Referenz}"; } }

        /// <summary>
        /// Dieser 'AccountEntry' ist eine Zeile in diesem 'Konto'
        /// </summary>
        public Konto Konto { get; private set; }

        /// <summary>
        /// Dies erscheint im Kontenplan bei 'Gegenkonto'
        /// </summary>
        public AccountEntry GegenEntry { get; set; }

        /// <summary>
        /// Dies erscheint im Kontenplan bei 'MWST'
        /// </summary>
        public AccountEntry MwstEntry { get; set; }

        /// <summary>
        /// Der relevante Betrag
        /// </summary>
        public Decimal Betrag { get; private set; }

        /// <summary>
        /// 'Entry': Die Zeile im Journal
        /// </summary>
        public Entry Entry { get; private set; }

        public AccountEntry(Entry entry, BookkeepingRelation relation, Konto konto, Decimal betrag, bool isMwst = false)
        {
            Entry = entry;
            Relation = relation;
            Konto = konto;
            Betrag = betrag;
            IsMwst = isMwst;
        }
    }

    // TODO: Move into helpers somewhere
    public static class N
    {
        static NumberFormatInfo swissNumberFormatInfo = CultureInfo.GetCultureInfo("de-CH").NumberFormat;

#pragma warning disable IDE1006 // Benennungsstile
        public static string F(Decimal d)
#pragma warning restore IDE1006 // Benennungsstile
        {
            return d.ToString("N2", swissNumberFormatInfo);
        }

        public static string Muh2(Decimal d)
        {
            return d.ToString("0.00", swissNumberFormatInfo);
        }

        public static decimal Round5Rappen(decimal betrag)
        {
            return decimal.Round(betrag * 2M, 1) / 2M;
        }

        public static decimal Round1Rappen(decimal betrag)
        {
            return decimal.Round(betrag, 2);
        }

        public static decimal Round10Rappen(decimal betrag)
        {
            return decimal.Round(betrag, 1);
        }

        public static decimal Round1Franken(decimal betrag)
        {
            return decimal.Round(betrag, 0);
        }

        public static void AssertNoSubcents(string referenz, decimal betrag)
        {
            // TODO: Uncomment the following lines to get better performace.
            decimal Rest = betrag % 0.01M;
            Trace.Assert(Rest == 0M, $"Buchung {referenz} mit Betrag {betrag:#.00000} wurde nicht auf einen Rappen gerundet. Der Rest beträgt {Rest:#.00000}!");
        }
    }

    /// <summary>
    /// Repräsentiert ein Konto mit allen Buchungen zum Konto.
    /// </summary>
    public class BookkeepingAccount
    {
        // TODO: Vermerk ob Account-Entry
        // TODO: Gemäss Kontenplan vergeben: Aktiven, Passiven, Einnahmen, Ausgaben, MWST
        public Konto Konto { get; private set; }
        public Decimal Saldo { get; private set; }

        public IEnumerable<AccountEntry> Entries
        {
            get
            {
                foreach (KontoDay kontoDay in Konto.KontoDays.DaysOrdered)
                {
                    foreach (AccountEntry accountEntry in kontoDay.EntriesOrdered)
                    {
                        yield return accountEntry;
                    }
                }
            }
        }

        public bool HasEntries { get; private set; }
        private Configuration config;

        public BookkeepingAccount(Configuration config_, Konto konto)
        {
            config = config_;
            Konto = konto;
            Saldo = decimal.MinValue;
            HasEntries = false;
        }

        public void AddEntry(AccountEntry entry)
        {
            HasEntries = true;
            N.AssertNoSubcents(entry.Referenz, entry.Betrag);
            KontoDay day = Konto.KontoDays.GetDay(entry.Entry.Valuta);
            day.Entries.Add(entry);
        }

        public void Write(string directory)
        {
            string filename = $@"{directory}/Konto_{Konto.KontoNr}.txt";
            using (StreamWriter fs = new StreamWriter(filename))
            {
                fs.WriteLine($"Konto {Konto.KontoNr}: {Konto.Text}");
                fs.WriteLine($"EroeffnungsKontostand: {Konto.EroeffnungsKontostand}");

                foreach (AccountEntry entry in Entries)
                {
                    fs.WriteLine(entry.Entry.Line);
                    fs.WriteLine($"  '{entry.Entry.Buchungsvorlage.VorlageText}'");
                    if (entry.Entry.BankEntry != null)
                    {
                        fs.WriteLine($"  Bank: {entry.Entry.BankEntry.BankBuchungstext}");
                    }
                    fs.WriteLine($"  MWST-Gegenkonto {entry.GegenEntry.Konto.KontoNr}");
                    foreach (var errorAfter in entry.Entry.MessagesErrors)
                    {
                        fs.WriteLine($" >> {errorAfter}");
                    }
                }
            }
        }

        public void UpdateSaldo()
        {
            Saldo = Konto.EroeffnungsKontostand;
            foreach (KontoDay kontoDay in Konto.KontoDays.DaysOrdered)
            {
                foreach (AccountEntry entry in kontoDay.Entries)
                {
                    // Normalfall: Saldo = Soll - Haben
                    int sign = (entry.Relation == BookkeepingRelation.SOLL) ? 1 : -1;
                    if (Konto.IsEinnahmenPassiven)
                    {
                        /// Spezialfall: Saldo = Haben - Soll
                        sign = -sign;
                    }
                    Saldo += sign * entry.Betrag;
                }
                kontoDay.SetSaldo(Saldo);
            }
        }

        public decimal GetSaldo(TValuta datum)
        {
            decimal saldo = 0;
            foreach (KontoDay kontoDay in Konto.KontoDays.DaysOrdered)
            {
                if (kontoDay.Valuta > datum)
                {
                    break;
                }
                saldo = kontoDay.Saldo;
            }
            return saldo;
        }
    }

    /// <summary>
    /// Alle Konten mit allen Buchungen.
    /// Wird aus dem Journal generiert.
    /// </summary>
    public class BookkeepingBook
    {
        private Dictionary<Konto, BookkeepingAccount> Accounts = new Dictionary<Konto, BookkeepingAccount>();
        public IEnumerable<BookkeepingAccount> AccountsOrdered { get { return Accounts.Values.OrderBy(a => a.Konto.KontoNr); } }

        private Configuration config;

        public BookkeepingBook(Configuration config_)
        {
            config = config_;
            foreach (Konto konto in config.Kontenplan.Values)
            {
                Accounts.Add(konto, new BookkeepingAccount(this.config, konto));
            }
        }

        public BookkeepingAccount this[Konto konto] { get { return Accounts[konto]; } }

        public BookkeepingClosing CreateErfolgsrechnung()
        {
            return new BookkeepingClosing(config, this);
        }

        public void Update(Entry entry)
        {
            // KEINE MWST: Es wurde kein MWST-Schlüssel angegeben
            bool HasMwst = entry.MWST != null;
            if (!HasMwst)
            {
                // Keine MWST
                UpdateOhneMwst(entry);
                return;
            }

            // Wir müssen die MWST berechnen
            if (entry.KontoHaben == entry.KontoSoll)
            {
                entry.MessagesErrors.Add("Das Soll und Haben-Konto ist dasselbe und darum ergibt sich keine MWST. Bitte keine MWST angeben!");
                HasMwst = false;
            }

            UpdateMitMwst(entry);
        }

        private void UpdateOhneMwst(Entry entry)
        {
            AccountEntry haben = new AccountEntry(entry, BookkeepingRelation.HABEN, entry.KontoHaben, entry.Betrag);
            AccountEntry soll = new AccountEntry(entry, BookkeepingRelation.SOLL, entry.KontoSoll, entry.Betrag);
            haben.GegenEntry = soll;
            soll.GegenEntry = haben;
            Accounts[entry.KontoHaben].AddEntry(haben);
            Accounts[entry.KontoSoll].AddEntry(soll);
        }

        private void UpdateMitMwst(Entry entry)
        {
            bool KontoSollBelastetMwst = entry.KontoSoll.IsEinnahmenAusgaben;
            bool KontoHabenBelastetMwst = entry.KontoHaben.IsEinnahmenAusgaben;
            if (KontoSollBelastetMwst == KontoHabenBelastetMwst)
            {
                if ((entry.KontoSoll.KontoType == KontoType.AKTIVEN) && (entry.KontoHaben.KontoType == KontoType.AKTIVEN))
                {
                    // I bought something which goes into the inventory.
                    // For example a car.
                    // This code is very speculative. It might be better to define the MWST-Konto somewhere else.
                    KontoHabenBelastetMwst = entry.KontoSoll.KontoNr < entry.KontoHaben.KontoNr;
                    KontoSollBelastetMwst = !KontoHabenBelastetMwst;
                }
                else
                {
                    entry.MessagesErrors.Add($"Um MWST abrechnen zu können muss genau ein Konto zur Gruppe EinnahmenAusgeben gehören! KontoSoll:{entry.KontoSoll.KontoType} {entry.KontoSoll.KontoNr}({entry.KontoSoll.Text}), KontoHaben:{entry.KontoHaben.KontoType} {entry.KontoHaben.KontoNr}({entry.KontoHaben.Text})");
                }
            }

            Decimal BetragMwst = config.MwstAbrechnung.CalculateBetragMwst(entry);
            BetragMwst = N.Round1Rappen(BetragMwst);
            Decimal BetragOhneMwst = entry.Betrag - BetragMwst;

            AccountEntry haben = new AccountEntry(entry,
              BookkeepingRelation.HABEN,
              entry.KontoHaben,
              KontoSollBelastetMwst ? entry.Betrag : BetragOhneMwst);
            AccountEntry soll = new AccountEntry(entry,
              BookkeepingRelation.SOLL,
              entry.KontoSoll,
              KontoSollBelastetMwst ? BetragOhneMwst : entry.Betrag);
            AccountEntry mwst = new AccountEntry(entry,
              KontoSollBelastetMwst ? BookkeepingRelation.SOLL : BookkeepingRelation.HABEN,
              entry.MWST.Konto,
              BetragMwst, isMwst: true);
            haben.GegenEntry = soll;
            haben.MwstEntry = mwst;
            soll.GegenEntry = haben;
            soll.MwstEntry = mwst;
            mwst.GegenEntry = KontoSollBelastetMwst ? haben : soll;

            Accounts[entry.KontoHaben].AddEntry(haben);
            Accounts[entry.KontoSoll].AddEntry(soll);
            Accounts[entry.MWST.Konto].AddEntry(mwst);
        }

        public void WriteTxtDirectory()
        {
            if (!config.ProgramArguments.CreateTxt)
            {
                return;
            }

            foreach (BookkeepingAccount account in AccountsOrdered)
            {
                account.Write(Directories.DIRECTORY_TRACE);
            }
        }

        public void WriteTxt()
        {
            string filename = config.ProgramArguments.CreateTxtOrDelete($"{Directories.DIRECTORY_TRACE}/out_Konten.txt");
            if (filename != null)
            {
                using (StreamWriter fs = new StreamWriter(filename))
                {
                    TextRendererBook r = new TextRendererBook(config, fs);
                    WriteBook(r);
                }
            }
        }


        public void WriteHtml(BookkeepingClosing erfolgsrechnung, Journal journal)
        {
            string filename = config.ProgramArguments.CreateHtmlOrDelete("out_KontenUndAbschluss.html");
            if (filename != null)
            {
                using (HtmlStreamWriter html = new HtmlStreamWriter(config, filename))
                {
                    WriteHtmlIndex(html);
                    journal.WriteBuchungsvorlagen(html);
                    erfolgsrechnung.WriteHtml(html);
                    WriteHtml(html);
                }
            }
        }

        public void WritePdf()
        {
#if DISABLED
      string filename = config.ProgramArguments.CreatePdfOrDelete("out_Konten.pdf");
      if (filename != null)
      {
        PdfDocument pdf = new PdfDocument(config);
        WritePdf(pdf);
        pdf.Write(filename);
      }
#endif // DISABLED
        }

        /// <summary>
        /// Write Book and Closing (Erfolgsrechnung)
        /// </summary>
        /// <param name="erfolgsrechnung"></param>
        public void WritePdf(BookkeepingClosing erfolgsrechnung)
        {
            string filename = config.ProgramArguments.CreatePdfOrDelete("out_KontenUndAbschluss.pdf");
            if (filename != null)
            {
                PdfDocument pdf = new PdfDocument(config);
                erfolgsrechnung.WritePdf(pdf);
                WritePdf(pdf);
                pdf.Write(filename);
            }
        }

        private void WritePdf(PdfDocument pdf)
        {
            PdfRendererBook rendererBook = new PdfRendererBook(config, pdf);
            WriteBook(rendererBook);
        }

        public void WriteHtml(HtmlStreamWriter html)
        {
            HtmlRendererBook rendererBook = new HtmlRendererBook(config, html);
            WriteBook(rendererBook);
        }

        public void WriteHtmlIndex(HtmlStreamWriter html)
        {
            HtmlIndexRenderer.Render(html, AccountsOrdered);
        }

        private void WriteBook(IRendererBook rendererBook)
        {
            foreach (BookkeepingAccount account in AccountsOrdered)
            {
                if (account.HasEntries)
                {
                    IRendererAccount rendererAccount = rendererBook.CreateAccountRenderer(account);
                    WriteAccount(rendererAccount);
                }
            }
        }

        private void WriteAccount(IRendererAccount rendererAccount)
        {
            decimal endOfAccountSaldo = 0M;
            foreach (KontoDay kontoDay in rendererAccount.Account.Konto.KontoDays.DaysOrdered)
            {
                List<AccountEntry> entries = kontoDay.EntriesOrdered.ToList();
                if (entries.Count > 0)
                {
                    AccountEntry lastEntry = entries.Last();

                    foreach (AccountEntry entry in entries)
                    {
                        if (entry == lastEntry)
                        {
                            rendererAccount.WriteEntry(entry, kontoDay);
                            endOfAccountSaldo = kontoDay.Saldo;
                            break;
                        }
                        rendererAccount.WriteEntry(entry);
                    }
                }
            }
            rendererAccount.WriteEndOfAccount(endOfAccountSaldo);
        }

        /// <summary>
        /// Loop for all accounts and update the end-of-day saldo
        /// </summary>
        public void UpdateAccounts()
        {
            foreach (BookkeepingAccount account in Accounts.Values)
            {
                account.UpdateSaldo();
            }
        }

        public void MwstAbrechnung(Journal journal)
        {
            if (config.MwstAbrechnung != null)
            {
                config.MwstAbrechnung.Abrechnung(journal, this);
            }
        }
    }

    public class TextRendererBook : IRendererBook
    {
        private StreamWriter fs;
        private Configuration config;

        public TextRendererBook(Configuration config_, StreamWriter fs_)
        {
            config = config_;
            fs = fs_;
        }

        public IRendererAccount CreateAccountRenderer(BookkeepingAccount account)
        {
            Konto Konto = account.Konto;
            fs.WriteLine($"Konto {Konto.KontoNr}: {Konto.Text}");
            fs.WriteLine($"EroeffnungsKontostand: {Konto.EroeffnungsKontostand}");
            return new TextRendererAccount(config, fs, account);
        }
    }

    public class TextRendererAccount : IRendererAccount
    {
        private Configuration config;
        private StreamWriter fs;
        public BookkeepingAccount Account { get; }

        public TextRendererAccount(Configuration config_, StreamWriter fs_, BookkeepingAccount account)
        {
            config = config_;
            fs = fs_;
            Account = account;
        }

        public void WriteEntry(AccountEntry entry, KontoDay day)
        {
            fs.WriteLine($"{entry.Referenz}, {N.F(entry.Betrag)} '{entry.Entry.Buchungsvorlage.VorlageText}' {entry.Entry.Kommentar} ({entry.Entry.Line})");
            fs.WriteLine($"  MWST-Gegenkonto {entry.GegenEntry.Konto.KontoNr}");
        }

        public void WriteEndOfAccount(decimal saldo)
        {
        }
    }

    /// <summary>
    /// Renders all Accounts of the Book
    /// </summary>
    public interface IRendererBook
    {
        IRendererAccount CreateAccountRenderer(BookkeepingAccount account);
    }

    /// <summary>
    /// Renders one Account
    /// </summary>
    public interface IRendererAccount
    {
        BookkeepingAccount Account { get; }

        void WriteEntry(AccountEntry entry, KontoDay day = null);

        void WriteEndOfAccount(decimal saldo);
    }
}
