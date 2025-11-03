using System;
using System.Diagnostics;
using System.IO;

namespace cashboxNet
{
    /// <summary>
    /// A line in the 'muh2'-file.
    /// </summary>
    public class Entry : IComparable
    {
        public enum EnumVerb
        {
            /// <summary>2017-01-04a b 90.00 mitgliederbeitrag VCS</summary>
            BUCHUNG,
            /// <summary>2017-01-04a vorschlag 90.00 mitgliederbeitrag VCS</summary>
            BUCHUNGSVORSCHLAG,
            /// <summary>2017-01-04a f 90.00 mitgliederbeitrag VCS</summary>
            FILE_BUCHUNG,
        };

        public LazyStringList MessagesTodo = new LazyStringList();
        public LazyStringList MessagesErrors = new LazyStringList();
        public LazyStringList MessagesComments = new LazyStringList();
        public LazyStringList Tags = new LazyStringList();
        public EnumVerb Verb;
        public TValuta Valuta;
        public readonly string Referenz;
        public string Kommentar { get; private set; }
        public Decimal Betrag;
        public Konto KontoSoll;
        public Konto KontoHaben;
        public MwstSatz MWST = null;
        public Buchungsvorlage Buchungsvorlage;

        /// <summary>
        /// If this entry is set, we represent a BankEntry.
        /// </summary>
        public BankEntry BankEntry = null;

        public string Line { get; private set; }

        public Entry(Configuration config, RegexBeginReferenzVerbRest regexReferenz)
        {
            Line = regexReferenz.Match.Value;
            Verb = regexReferenz.Verb;
            Referenz = regexReferenz.Referenz;
            Betrag = regexReferenz.Betrag;
            Valuta = regexReferenz.Valuta;
            Kommentar = regexReferenz.Kommentar;

            N.AssertNoSubcents(Referenz, Betrag);

            Action<string> actionError = error =>
            {
                MessagesErrors.Add($"In '{regexReferenz.Buchungsanweisung}': {error}");
                //  throw new CashboxException($"In '{regexReferenz.Buchungsanweisung}': {error}");
            };

            Action<Buchungsvorlage> actionVorlage = vorlage =>
            {
                Buchungsvorlage = vorlage;
                KontoHaben = vorlage.KontoHaben;
                KontoSoll = vorlage.KontoSoll;
                MWST = vorlage.MWST;
            };

            Action<string> actionTag = tag => { Tags.Add(tag); };

            Action<string, KontoErsatz> actionKontoErsatz = (einzelanweisung, kontoErsatz) =>
            {
                if (kontoErsatz.Contains(KontoHaben))
                {
                    KontoHaben = kontoErsatz.Konto;
                    return;
                }
                if (kontoErsatz.Contains(KontoSoll))
                {
                    KontoSoll = kontoErsatz.Konto;
                    return;
                }
                actionError($"Vorlage '{einzelanweisung}': Kann weder {KontoHaben.KontoNr} noch {KontoSoll.KontoNr} ersetzen!");
            };

            Action<string, MwstSatz> actionMwstSatz = (einzelanweisung, mwstSatz) => { MWST = mwstSatz; };

            // auto-ohneMwst-FAHRZEUG
            config.HandleBuchungsanweisungen(regexReferenz.Buchungsanweisungen, actionError, actionVorlage, actionTag, actionKontoErsatz, actionMwstSatz);

            Trace.Assert(Buchungsvorlage != null);
        }

        public Entry(Configuration config, Konto kontoBank, BankEntry bankEntry, string referenz)
        {
            Trace.Assert(bankEntry != null);
            Trace.Assert(bankEntry.BankBuchungstext != null);
            Verb = EnumVerb.BUCHUNGSVORSCHLAG;
            Referenz = referenz;
            Valuta = bankEntry.Valuta;
            BankEntry = bankEntry;
            Kommentar = "";

            bool direktBuchen = false;
            string vorlageText;
            Buchungsvorschlag buchungsvorschlag = config.FindVorschlag(bankEntry.BankBuchungstext);
            if (buchungsvorschlag != null)
            {
                direktBuchen = buchungsvorschlag.DirektBuchen;
                Buchungsvorlage = buchungsvorschlag.Vorlage;
                Kommentar = buchungsvorschlag.Buchungstext;
                vorlageText = buchungsvorschlag.VorlageTextAll; // auto-SUZUKI
            }
            else
            {
                Buchungsvorlage = bankEntry.Statement == BankStatement.Credit ? kontoBank.BuchungsvorlageNichtGefundenCredit : kontoBank.BuchungsvorlageNichtGefundenDebit;
                vorlageText = Buchungsvorlage.VorlageText;
                if (Buchungsvorlage.BuchungsText == "")
                {
                    // Falls kein Text in 'cashbox_config_vorlagebuchungen.cs' vorgegeben ist: Aus dem Bankjournal �bernehmen.
                    Kommentar = bankEntry.BankBuchungstext;
                }
            }

            KontoSoll = Buchungsvorlage.KontoSoll;
            KontoHaben = Buchungsvorlage.KontoHaben;

            if ((KontoHaben == kontoBank) == (bankEntry.Statement == BankStatement.Credit))
            {
                KontoHaben = Buchungsvorlage.KontoSoll;
                KontoSoll = Buchungsvorlage.KontoHaben;
            }

            MessagesComments.Add($"{RegexBeginReferenzVerbRest.VERB_BUCHUNGSVORSCHLAG}: {bankEntry.Comment}");

            Betrag = bankEntry.Betrag;
            N.AssertNoSubcents(Referenz, Betrag);

            string verb = direktBuchen ? RegexBeginReferenzVerbRest.VERB_BUCHUNG : RegexBeginReferenzVerbRest.VERB_BUCHUNGSVORSCHLAG;
            string line = $"{Referenz} {verb} {N.Muh2(Betrag)} {vorlageText} {Buchungsvorlage.BuchungsText} {Kommentar}";
            Line = line.Replace("  ", " ").TrimEnd();
        }

        public void Write(TextWriter fs)
        {
            foreach (string todo in MessagesTodo)
            {
                fs.WriteLine($"{RegexBeginBasic.VERB_TODO} N�chste Zeile: {todo}");
            }
            foreach (string error in MessagesErrors)
            {
                fs.WriteLine($"{RegexBeginBasic.VERB_FEHLER} N�chste Zeile: {error}");
            }
            foreach (string comment in MessagesComments)
            {
                fs.WriteLine($"{RegexBeginBasic.VERB_COMMENT} {comment}");
            }

            fs.WriteLine(Line);
        }

        /// <summary>
        /// Sortierung 2017-12-31a, b, c, d.... aa, ab, ac..... aaa, aab, aac.....
        /// </summary>
        public static int Compare(Entry a, Entry b)
        {
            string referenceA = a.Referenz;
            string referenceB = b.Referenz;
            int compareLength = referenceA.Length.CompareTo(referenceB.Length);
            if (compareLength != 0)
            {
                return compareLength;
            }
            return referenceA.CompareTo(referenceB);
        }

        private int CompareTo(Entry other)
        {
            string referenceB = other.Referenz;
            int compareLength = Referenz.Length.CompareTo(referenceB.Length);
            if (compareLength != 0)
            {
                return compareLength;
            }
            return Referenz.CompareTo(referenceB);
        }

        public int CompareTo(object other)
        {
            return CompareTo((Entry)other);
        }
    }


}
