using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace cashboxNet
{
    public class BuchungsanweisungenCollector
    {
        public const string FILENAME_OUT_CODECOMPLETION_BUCHUNGEN = "out_codecompletion_buchungen.json";

        private HashSet<string> buchungsanweisungen = new HashSet<string>();

        public void Add(string buchungsanweisung)
        {
            buchungsanweisungen.Add(buchungsanweisung);
        }

        public void WriteToJson()
        {
            var buchungsanweisungenObjects = buchungsanweisungen
              .OrderBy(b => b)
              .Select(b => new { label = b, detail = "" })
              .ToList();

            string json = System.Text.Json.JsonSerializer.Serialize(
              buchungsanweisungenObjects,
              new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(FILENAME_OUT_CODECOMPLETION_BUCHUNGEN, json);
        }
    }

    public class JournalDay : IDay
    {
        private class LazyOrderedEntries
        {
            private List<Entry> entries = new List<Entry>();
            private bool entriesAreOrdered = false;
            private readonly TValuta valuta;
            private ISet<string> references = new HashSet<string>();

            public IEnumerable<Entry> Entries { get { return entries; } }

            public LazyOrderedEntries(TValuta valuta_)
            {
                valuta = valuta_;
            }

            public IEnumerable<Entry> EntriesOrdered
            {
                get
                {
                    if (!entriesAreOrdered)
                    {
                        entries.Sort();
                        entriesAreOrdered = true;
                    }
                    return entries;
                }
            }


            public void Add(Entry entry)
            {
                Trace.Assert(entry.Valuta == valuta);
                string referenz = entry.Referenz;
                bool added = references.Add(referenz);
                if (!added)
                {
                    entry.MessagesErrors.Add($"'{referenz}' wird bereits verwendet!");
                }
                entries.Add(entry);
                entriesAreOrdered = false;
            }

            public string GetNextFreeReferenz()
            {
                for (int i = 0; ; i++)
                {
                    string referenz = ReferenzHelper.FormatReferenz(valuta, i);
                    if (!references.Contains(referenz))
                    {
                        return referenz;
                    }
                }
            }
        }

        public TValuta Valuta { get; private set; }
        public Journal Journal { get; private set; }
        public LazyStringList MessagesErrors = new LazyStringList();

        private List<KontoDay> kontoDays = new List<KontoDay>();
        public IEnumerable<KontoDay> KontoDays { get { return kontoDays; } }

        public IEnumerable<Entry> EntriesOrdered { get { return entries.EntriesOrdered; } }
        private LazyOrderedEntries entries;

        public JournalDay(Journal journal, TValuta valuta)
        {
            Journal = journal;
            Valuta = valuta;
            entries = new LazyOrderedEntries(valuta);
        }

        public void AddEntry(Entry entry)
        {
            entries.Add(entry);
        }

        public string GetNextFreeReferenz()
        {
            return entries.GetNextFreeReferenz();
        }

        public void AddKontoDay(KontoDay kontoDay)
        {
            kontoDays.Add(kontoDay);
        }

        public Entry FindEntry(string referenz)
        {
            foreach (Entry entry in entries.Entries)
            {
                if (entry.Referenz == referenz)
                {
                    return entry;
                }
            }
            return null;
        }

        public void Write(TextWriter fs)
        {
            foreach (KontoDay kontoDay in kontoDays)
            {
                foreach (string error in kontoDay.MessagesErrors)
                {
                    fs.WriteLine($"{RegexBeginBasic.VERB_FEHLER} Konto {kontoDay.Konto.KontoNr} ({kontoDay.Konto.Text}): {error}");
                }
            }
            foreach (string error in MessagesErrors)
            {
                fs.WriteLine($"{RegexBeginBasic.VERB_FEHLER} {error}");
            }
        }
    }

    public class JournalDays : AbstractDays<JournalDay>
    {
        private Journal journal;
        public JournalDays(Configuration config, Journal journal_) : base(config)
        {
            journal = journal_;
        }
        protected override JournalDay CreateDay(TValuta valuta)
        {
            return new JournalDay(journal, valuta);
        }
    }

    /// <summary>
    /// The 'muh2'-file.
    /// </summary>
    public class Journal
    {
        public class Statistics
        {
            public Dictionary<Buchungsvorlage, int> VorlagenUsed = new Dictionary<Buchungsvorlage, int>();
            public Statistics(Journal journal)
            {
                journal.Loop(processEntry: e =>
                {
                    VorlagenUsed.TryGetValue(e.Buchungsvorlage, out int count);
                    count++;
                    VorlagenUsed[e.Buchungsvorlage] = count;
                });
            }
        }
        public Statistics GetStatistics() { return new Statistics(this); }

        public void WriteBuchungsvorlagen(HtmlStreamWriter html)
        {
            html.Body.Append("a", $"name='{EnumAnchors.BUCHUNGSVORLAGEN}'");
            html.Body.Append("h2", text: "Buchungsvorlagen");
            HtmlTag table = html.Body.Append("table", args: "class='buchungsvorlagen' xmlns=''");
            {
                HtmlTag tr = table.Append("tr");
                tr.Append("th", text: "Anzahl");
                tr.Append("th", text: "Vorlage");
                tr.Append("th", text: "Soll");
                tr.Append("th", text: "Haben");
                tr.Append("th", text: "Text");
            }
            Journal.Statistics statistics = GetStatistics();
            foreach (string strVorlage in config.Buchungsvorlagen.Keys.OrderBy(s => s))
            {
                Buchungsvorlage objVorlage = config.Buchungsvorlagen[strVorlage];
                HtmlTag tr = table.Append("tr");
                int used;
                if (!statistics.VorlagenUsed.TryGetValue(objVorlage, out used))
                {
                    used = 0;
                }
                tr.Append("td", text: used.ToString());
                tr.Append("td", text: objVorlage.VorlageText);
                tr.Append("td", text: objVorlage.KontoSoll.KontoNr.ToString());
                tr.Append("td", text: objVorlage.KontoHaben.KontoNr.ToString());
                tr.Append("td", text: objVorlage.BuchungsText);
            }
        }
        public void Loop(Action<JournalDay> processDay = null, Action<Entry> processEntry = null)
        {
            foreach (JournalDay journalDay in JournalDays.DaysOrdered)
            {
                if (processEntry != null)
                {
                    foreach (Entry entry in journalDay.EntriesOrdered)
                    {
                        processEntry(entry);
                    }
                }
                if (processDay != null)
                {
                    processDay(journalDay);
                }
            }
        }

        public JournalDays JournalDays { get; private set; }
        public TValuta LastEntryValuta
        {
            get
            {
                try
                {
                    return JournalDays.DaysOrdered.Last().Valuta;
                }
                catch (InvalidOperationException)
                {
                    return config.DateStartValuta;
                }
            }
        }

        public Entry GewinnBuchungEntry { get; private set; }

        private Configuration config;
        private List<IError> Errors = new List<IError>();

        public Journal(Configuration config)
        {
            this.config = config;
            JournalDays = new JournalDays(config, this);
            GewinnBuchungEntry = null;

            foreach (Konto konto in this.config.Kontenplan.Values)
            {
                konto.UpdateByJournalDays(this.config, JournalDays);
            }
        }

        public void AddEntry(Entry entry)
        {
            if (entry.Buchungsvorlage == config.Gewinnbuchung)
            {
                GewinnBuchungEntry = entry;
            }
            JournalDay journalDay = JournalDays.GetDay(entry.Valuta);
            journalDay.AddEntry(entry);
        }

        /// <summary>
        /// Searches for existing references on this given 'valuta' and returns the first free reference.
        /// </summary>
        public string GetNextFreeReferenz(TValuta valuta)
        {
            JournalDay journalDay = JournalDays.GetDay(valuta);
            return journalDay.GetNextFreeReferenz();
        }

        public void WriteTags()
        {
            string filename = config.ProgramArguments.CreateTxtOrDelete("out_tags.csv");
            if (filename != null)
            {
                TagFile tagFile = new TagFile();
                tagFile.Push(this);
                tagFile.Write(filename);
            }
        }

        public string GuiErrors()
        {
            StringWriter sw = new StringWriter();
            foreach (IError error in Errors)
            {
                error.Write(sw);
            }
            Action<Entry> processEntry = entry =>
               {
                   foreach (string error in entry.MessagesErrors)
                   {
                       sw.WriteLine($"{entry.Referenz}: {error}");
                   }
               };
            Action<JournalDay> processDay = day =>
            {
                foreach (KontoDay kontoDay in day.KontoDays)
                {
                    foreach (string error in kontoDay.MessagesErrors)
                    {
                        sw.WriteLine($"{day.Valuta}: {kontoDay.Konto.KontoNr}: {error}");
                    }
                }
                foreach (string error in day.MessagesErrors)
                {
                    sw.WriteLine($"{day.Valuta}: {error}");
                }
            };
            Loop(processDay, processEntry);
            return sw.ToString();
        }

        public void AddErrorLine(string line, Exception exception, bool commentLine)
        {
            Errors.Add(new ErrorException(line, exception, commentLine));
        }

        public void AddErrorLine(string msg)
        {
            Errors.Add(new ErrorMessage(msg));
        }

        public BookkeepingBook CreateBook()
        {
            BookkeepingBook book = new BookkeepingBook(config);
            Loop(processEntry: entry => book.Update(entry));
            book.UpdateAccounts();
            return book;
        }

        #region Muh2-File
        private MuhFile muhfile = null;
        public void Muh2FileRead()
        {
            muhfile = new MuhFile(config);
            BuchungsanweisungenCollector buchungsanweisungen = new BuchungsanweisungenCollector();

            int lineNo = 0;
            foreach (string line in muhfile.Readlines())
            {
                lineNo += 1;
                try
                {
                    if (RegexBeginBasic.TryMatch(line, out RegexBeginBasic regexA))
                    {
                        if (regexA.IsTodoOrFehlerOrComment)
                        {
                            continue;
                        }
                    }

                    if (RegexBeginReferenzVerbRest.TryMatch(line, out RegexBeginReferenzVerbRest regexB))
                    {
                        switch (regexB.Verb)
                        {
                            case Entry.EnumVerb.BUCHUNG:
                            case Entry.EnumVerb.FILE_BUCHUNG:
                                buchungsanweisungen.Add(regexB.Buchungsanweisung);
                                break;
                        }
                        switch (regexB.Verb)
                        {
                            case Entry.EnumVerb.BUCHUNG:
                                AddEntry(new Entry(config, regexB));
                                continue;
                            case Entry.EnumVerb.FILE_BUCHUNG:
                            case Entry.EnumVerb.BUCHUNGSVORSCHLAG:
                                continue;
                        }
                    }
                    throw new LineFehlerException("Ungültig!");
                }
                catch (CashboxException ex)
                {
                    AddErrorLine(line, ex, commentLine: false);
                }
            }

            buchungsanweisungen.WriteToJson();
        }

        public void Muh2FileWrite()
        {
            StringWriter sw = new StringWriter();
            Write(sw);
            string filecontents = sw.ToString();
            muhfile.Write(filecontents);
        }

        private void Write(TextWriter tw)
        {
            // fs.WriteLine($"# {CashboxNetVersion.ProgrammNameFull}");
            foreach (IError error in Errors)
            {
                error.Write(tw);
            }

            Loop(day => day.Write(tw), entry => entry.Write(tw));
        }

        public void ConstraintAbschlussBuchungen()
        {
            if (GewinnBuchungEntry == null)
            {
                if (LastEntryValuta == config.DateEndValuta)
                {
                    JournalDays[LastEntryValuta].EntriesOrdered.Last().MessagesErrors.Add($"Am Ende des Jahres wird eine Gewinnbuchung '{config.Gewinnbuchung.VorlageText}' erwartet!");
                }
                return;
            }

            HashSet<Buchungsvorlage> abschlussbuchungen = new HashSet<Buchungsvorlage>(config.Abschlussbuchungen);
            Loop(processEntry: entry => abschlussbuchungen.Remove(entry.Buchungsvorlage));
            foreach (Buchungsvorlage vorlage in abschlussbuchungen.OrderBy(v => v.VorlageText))
            {
                GewinnBuchungEntry.MessagesErrors.Add($"Es wurde eine Gewinnbuchung '{config.Gewinnbuchung.VorlageText}' gefunden. Aber die Abschlussbuchung '{vorlage.BuchungsText}' fehlt!");
            }
        }
        #endregion
    }
}
