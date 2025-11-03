using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace cashboxNet
{
    public class KlangspielAutomation
    {
        public const string SAMPLE = "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vesr22244.doc";

        // public const string BUCHUNGSVORLAGE = "klangspielschweiz";
        // public const string BUCHUNGSVORLAGE_OHNE_MWST = "klangspielschweiz-ohneMWST";
        // public const string BUCHUNGSVORLAGEAUSLAND = "klangspielausland";
        // public const double MWST_SCHWELLE = 0.9; // Unterhalb 8%*MWST_SCHWELLE wird BUCHUNGSVORLAGE_OHNE_MWST verendet
        private KlangspielZahlungseingangConstraint constraint;
        private BookkeepingAccount konto;
        private Journal journal;
        private List<RegexpKlangspielRechnung> rechnungen = new List<RegexpKlangspielRechnung>();

        public KlangspielAutomation(KlangspielZahlungseingangConstraint constraint_, Configuration config, BookkeepingBook book, Journal journal_)
        {
            constraint = constraint_;
            konto = book[constraint.Konto];
            journal = journal_;
        }

        public void Run()
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(constraint.Directory))
            {
                string filedirectoryname = Path.GetFileName(path);
                if (filedirectoryname.Contains(BuchungenDirectory.SKIP))
                {
                    continue;
                }

                RegexpKlangspielRechnung rechnung = RegexpKlangspielRechnung.TryMatch(filedirectoryname);
                if (rechnung == null)
                {
                    journal.AddErrorLine($"{path}: File entspricht nicht der geforderten Syntax! Korrektes Beispiel: '{RegexpKlangspielRechnung.SAMPLE}'.");
                    continue;
                }
                rechnungen.Add(rechnung);
            }

            foreach (AccountEntry entry in konto.Entries)
            {
                Entry entry_ = entry.Entry;
                if (entry_.Verb != Entry.EnumVerb.BUCHUNGSVORSCHLAG)
                {
                    continue;
                }
                BankEntry bankEntry = entry_.BankEntry;
                if (bankEntry == null)
                {
                    continue;
                }
                List<Hit> hits = HandleEntry(entry);
                if (hits.Count == 0)
                {
                    continue;
                }
                hits.Sort((a, b) => b.Precision.CompareTo(a.Precision));

                Hit bestHit = hits[0];
                if (bestHit.Precision >= 0.99)
                {
                    bestHit.MoveFile();
                    continue;
                }

                foreach (Hit hit in hits)
                {
                    hit.AddHelperComment();
                }
            }
        }

        public class Hit
        {
            public readonly double Precision;
            private readonly string Hint;
            private readonly RegexpKlangspielRechnung Rechnung;
            private readonly Entry Entry;
            private KlangspielAutomation automation;

            public string FilenameNew { get { return $"{Entry.Referenz} {RegexBeginReferenzVerbRest.VERB_FILEBUCHUNG} {N.F(Entry.Betrag)} {Rechnung.Rest}"; } }
            public string PathNew { get { return Path.Combine(BuchungenDirectory.SUBDIRECTORY, FilenameNew); } }
            public string PathOld { get { return Path.Combine(automation.constraint.Directory, Rechnung.FileDirectoryName); } }

            public Hit(KlangspielAutomation automation_, double precision, RegexpKlangspielRechnung rechnung, Entry entry, string hint)
            {
                automation = automation_;
                Precision = precision;
                Rechnung = rechnung;
                Entry = entry;
                Hint = hint;
            }

            public void MoveFile()
            {
                automation.rechnungen.Remove(Rechnung);

                FileAttributes attr = File.GetAttributes(PathOld);

                if (attr.HasFlag(FileAttributes.Directory))
                {
                    // Directory
                    Directory.Move(PathOld, PathNew);
                }
                else
                {
                    // File
                    File.Move(PathOld, PathNew);
                }

                Entry.MessagesErrors.Add($"Cashbox nochmals starten! Die Rechnung wurde verschoben nach '{PathNew}'.");
            }

            public void AddHelperComment()
            {
                Entry.MessagesTodo.Add(Hint);
                Entry.MessagesTodo.Add($"Zahlungseingang überprüfen und File umbenennen.");
                Entry.MessagesTodo.Add($"von:  {Rechnung.FileDirectoryName}");
                Entry.MessagesTodo.Add($"nach: {FilenameNew}");
            }
        }

        private List<Hit> HandleEntry(AccountEntry accountEntry)
        {
            Entry entry = accountEntry.Entry;
            BankEntry bankEntry = entry.BankEntry;

            List<Hit> hits = new List<Hit>();

            foreach (RegexpKlangspielRechnung rechnung in rechnungen)
            {
                if (entry.Valuta < rechnung.Date)
                {
                    // Die Zahlung ist eingegangen, bevor die Rechnung gestellt wurde.
                    continue;
                }
                decimal difference = Math.Abs(accountEntry.Betrag - rechnung.Betrag);
                if (difference == 0M)
                {
                    if (bankEntry.VESR != null)
                    {
                        if (rechnung.VESR != null)
                        {
                            // Beide haben VESR
                            if (rechnung.VESR == bankEntry.VESR)
                            {
                                // Gefunden
                                hits.Add(new Hit(this, 1.0, rechnung, entry, "Betrag und VESR stimmen überein"));
                                return hits;
                            }
                        }
                        hits.Add(new Hit(this, 0.9, rechnung, entry, "Rechnung ohne VESR."));
                        continue;
                    }
                    hits.Add(new Hit(this, 0.8, rechnung, entry, "Einzahlung ohne VESR."));
                    continue;
                }
                decimal Limit = 2.5M;
                if (difference < Limit)
                {
                    double precision_reduction = 0.1 * ((double)difference) / ((double)Limit);
                    Trace.Assert(precision_reduction >= -0.01);
                    Trace.Assert(precision_reduction <= 0.11);
                    hits.Add(new Hit(this, 0.7 - precision_reduction, rechnung, entry, $"Bezahlter Betrag weicht um {N.F(difference)} ab."));
                }
            }
            return hits;
        }
    }

    public class RegexpKlangspielRechnung
    {
        public const string SAMPLE = "2017-01-03 168.50 klangspielschweiz 2017-01-03_09-05-10_vesr22244_R22244.kagi-mller.doc";

        public string FileDirectoryName { get; private set; }
        public decimal Betrag { get; private set; }
        public string Vorlagebuchung { get; private set; }
        public bool Vorauskasse { get; private set; }
        public string VESR { get; private set; }
        public string DateString { get; private set; }
        public TValuta Date { get; private set; }
        public string Rest { get; private set; }

        public static Regex regexStatic = new Regex($@"^(\d\d\d\d-\d\d-\d\d) +(?<betrag>\d+?\.\d\d) +(?<rest>(?<vorlagebuchung>[a-zA-Z]+) +(?<date>\d\d\d\d-\d\d-\d\d)_\d\d-\d\d-\d\d(_vesr(?<vesr>\d+?))?(?<vorauskasse>_vorauskasse)?((_|\.).*)?)$", RegexOptions.Compiled);
        public static RegexpKlangspielRechnung TryMatch(string fileDirectoryName)
        {
            Match match = regexStatic.Match(fileDirectoryName);
            if (!match.Success)
            {
                return null;
            }

            RegexpKlangspielRechnung regex = new RegexpKlangspielRechnung();
            regex.FileDirectoryName = fileDirectoryName;
            string betrag_ = match.Groups["betrag"].Value;
            regex.Betrag = decimal.Parse(betrag_);
            regex.Vorlagebuchung = match.Groups["vorlagebuchung"].Value;
            regex.DateString = match.Groups["date"].Value;
            regex.Date = new TValuta(regex.DateString);
            regex.VESR = match.Groups["vesr"].Value;
            regex.Vorauskasse = match.Groups["vorauskasse"].Success;
            regex.Rest = match.Groups["rest"].Value;
            return regex;
        }
    }
}
