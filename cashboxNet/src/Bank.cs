using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace cashboxNet
{
  /// <summary>
  /// Wird im Konfigurationsfile instanziert und representiert die Anbindung eines Bankimport-Files an ein Bankkonto.
  /// </summary>
  public interface IBankFactory
  {
    IBankReader Factory(Configuration config, string directory);

    /// <summary>
    /// The file with the bank-statements to be read
    /// </summary>
    string Filename { get; }

    /// <summary>
    /// The name of this bank-account. For example "ZKB Sparkonto", "ZKB Privatkonto"
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The account to attach.
    /// </summary>
    Konto KontoBank { get; }

    /// <summary>
    /// Soll der Kontostand überprüft werden oder dürfen Buchungen fehlen.
    /// </summary>
    bool KontostandUeberpruefen { get; }

    /// <summary>
    /// Sollen Buchungsvorschlaege eingefügt werden.
    /// </summary>
    bool AddBuchungsvorschlaege { get;  }

    void UpdateByConfig(Configuration config);
  }

  /// <summary>
  /// This reads the file with the bank-statements.
  /// </summary>
  public interface IBankReader
  {
    IBankFactory BankFactory { get; }
    IEnumerable<BankEntry> ReadBankEntries();
    bool TryGetInitialBalance(out decimal initialBalance);
  }

  public enum BankStatement
  {
    /// <summary>Einzahlen</summary>
    Credit,

    /// <summary>Auszahlen</summary>
    Debit,
  }

  /// <summary>
  /// One Bank-Statement.
  /// This generic class will be used by all Bank-Readers (MT940, csv, etc).
  /// </summary>
  public class BankEntry
  {
    /// <summary>
    /// 2017-03-02
    /// </summary>
    public readonly TValuta Valuta;

    /// <summary>
    /// Strassenverkehrsamt Kanton Zürich / Uetlibergstr. 301 / 8036 Zürich
    /// </summary>
    public readonly string BankBuchungstext;

    /// <summary>
    /// 103.20
    /// </summary>
    public readonly Decimal Betrag;

    /// <summary>
    /// 25
    /// Line or Record Number
    /// </summary>
    public readonly int LineNr;

    /// <summary>
    /// This comment will be printed as comment to the muh2-file
    /// journal_raiffeisen.mt940(1): Strassenverkehrsamt Kanton Zürich /  Uetlibergstr. 301 / 8036 Zürich
    /// </summary>
    public readonly string Comment;

    /// <summary>
    /// Debit or Credit
    /// </summary>
    public readonly BankStatement Statement = BankStatement.Debit;

    /// <summary>
    /// Uniquely identifies the bank statement
    /// </summary>
    public int BelegNr { get; set; }

    /// <summary>
    /// VESR, 22504
    /// </summary>
    public readonly string VESR = null;

    public string Reference { get { return $"{Valuta}_{BelegNr:000}"; } }

    /// <summary>
    /// The Journal-Reference mapped to this entry.
    /// </summary>
    public string MappingToJournalReference { get; set; }

    public BankEntry(IBankFactory factory, int lineNr, TValuta valuta, string buchungstext, decimal betrag, BankStatement statement, string vesr = null, string comment = null)
    {
      LineNr = lineNr;
      Valuta = valuta;
      Trace.Assert(buchungstext != null);
      BankBuchungstext = buchungstext.Replace("\n\r", " ").Replace("\n", " ").Replace("  ", " ").Trim();
      Betrag = betrag;
      Statement = statement;
      if (comment is null) {
        // Comment = $"{factory.Filename}({LineNr}): {BankBuchungstext}";
        Comment = $"{factory.Filename}: {BankBuchungstext}";
      } else {
        Comment = comment;
      }
      VESR = vesr;
    }

    /// <summary>
    /// In einer Buchung darf der negative Betrag stehen.
    /// Das entspricht einem Vertauschen von Soll und Haben.
    /// </summary>
    public bool BetragAbsoluteEquals(decimal betrag)
    {
      return (betrag == Betrag) || (-betrag == Betrag);
    }
  }

  /// <summary>
  /// All results returned from a Bankreader.
  /// The Bankreader has various implementations (MT940, csv, etc.).
  /// However, this class is generic.
  /// </summary>
  public class BankreaderResult
  {
    /// <summary>
    /// All records found in the bank-import file.
    /// </summary>
    public List<BankEntry> BankEntries;

    /// <summary>
    /// The reader which provided this result.
    /// </summary>
    public IBankFactory BankFactory { get; private set; }

    private List<BankEntry> bankEntryVorschlaege = new List<BankEntry>();

    public Konto KontoBank { get { return BankFactory.KontoBank; } }
    private readonly Configuration Config;
    private readonly string MappingFilename;

    public BankreaderResult(Configuration config, IBankReader bankreader)
    {
      Config = config;
      BankFactory = bankreader.BankFactory;
      MappingFilename = $@"{Directories.DIRECTORY_TRACE}/cashbox_mapping_{BankFactory.Name}.txt";
      BankEntries = FilterEntries(bankreader).ToList();

      Init();

      ReadMappingFile();

      CheckEroeffnungKontoStand(bankreader);
    }

    private void CheckEroeffnungKontoStand(IBankReader bankreader)
    {
      if (bankreader.TryGetInitialBalance(out decimal initialBalance))
      {
        if (KontoBank.EroeffnungsKontostand != initialBalance)
        {
          throw new Exception($"Konto '{KontoBank.KontoNr}': Eröffnungs Kontostand in '{bankreader.BankFactory.Filename}' ist {initialBalance}, hingegen in '{Core.CONFIGUARTION_FILE_JAHR}' {KontoBank.EroeffnungsKontostand}!");
        }
      }
    }

    public class RegexLine : RegexBase
    {
      public static Regex regexStatic = new Regex($@"^(?<bankentry>\S+?)\s+(?<reference>\S+)$", RegexOptions.Compiled);
      public string BankEntryReference { get { return GetValueString("bankentry"); } }
      public string Reference { get { return GetValueString("reference"); } }

      public static bool TryMatch(string line, out RegexLine regex)
      {
        return RegexBase.TryMatch(regexStatic, line, out regex);
      }
    }

    private void ReadMappingFile()
    {
      string filename = MappingFilename;
      if (!File.Exists(filename))
      {
        return;
      }

      Dictionary<string, BankEntry> bankEntryDict = BankEntries.ToDictionary(k => k.Reference, v => v);

      foreach (string line in File.ReadAllLines(filename))
      {
        if (!RegexLine.TryMatch(line, out RegexLine lineMatch))
        {
          throw new Exception($"Error parsing '{filename}'!");
        }
        if (bankEntryDict.TryGetValue(lineMatch.BankEntryReference, out BankEntry bankEntry))
        {
          bankEntry.MappingToJournalReference = lineMatch.Reference;
        }
      }
    }

    public void WriteMappingFile()
    {
      using (StreamWriter fout = new StreamWriter(MappingFilename))
      {
        HashSet<string> journalReferences = new HashSet<string>();

        // Es werden nur die BankEntries in's File geschrieben, bei denen am gleichen Tag derselbe Betrag vorkommt.
        // Gruppiern nach Tag
        var groupByDay = BankEntries.GroupBy(e => e.Valuta);
        foreach (var entriesPerDay in groupByDay)
        {
          // Gruppieren nach Datum
          foreach (var entriesPerDayAndBetrag in entriesPerDay.GroupBy(e => e.Betrag))
          {
            if (entriesPerDayAndBetrag.Count() > 1)
            {
              // Es gibt mehr als ein BankEntry an diesem Tag für diesen Betrag
              foreach (BankEntry bankEntry in entriesPerDayAndBetrag)
              {
                // BankEntry in's File schreiben
                string referenz = bankEntry.MappingToJournalReference;
                if (referenz != null)
                {
                  if (!journalReferences.Add(referenz))
                  {
                    // This entry was already added.
                    // We mustn't add it twice!
                    continue;
                  }
                  fout.WriteLine($"{bankEntry.Reference} {referenz}");
                }
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Fast selecting the entries between 'Config.DateStart' and 'Config.DateEnd'
    /// </summary>
    /// <returns></returns>
    private IEnumerable<BankEntry> FilterEntries(IBankReader bankreader)
    {
      var it = bankreader.ReadBankEntries().GetEnumerator();
      // We skip all entries before 'Config.DateStart'
      while (true)
      {
        if (!it.MoveNext())
        {
          yield break;
        }
        BankEntry entry = it.Current;
        if (entry.Valuta >= Config.DateStartValuta)
        {
          // DateStartValuta: Start
          break;
        }
      }
      // Now we return all entries in the required timerange
      TValuta lastValuta = new TValuta(0);
      while (true)
      {
        BankEntry entry = it.Current;
        if (entry.Valuta > Config.DateEndValuta)
        {
          // DateEndValuta: Stop
          yield break;
        }
        // Verify that the valuta is ascending
        Trace.Assert(lastValuta <= entry.Valuta);
        lastValuta = entry.Valuta;

        yield return entry;

        if (!it.MoveNext())
        {
          yield break;
        }
      }
    }

    public void WriteSaldo(Journal journal)
    {
      string filename = Config.ProgramArguments.CreateTxtOrDelete($"{Directories.DIRECTORY_TRACE}/EndOfTheDaySaldo_{KontoBank.KontoNr}.txt");
      if (filename != null)
      {
        using (StreamWriter fs = new StreamWriter(filename))
        {
          fs.WriteLine($"Konto {KontoBank.KontoNr}: {KontoBank.Text}");
          foreach (KontoDay kontoDay in KontoBank.KontoDays.DaysOrdered)
          {
            fs.WriteLine($"{kontoDay.Valuta}: {kontoDay.SaldoExpected}");
          }
        }
      }
    }

    private void Init()
    {
      int iBelegNumber = 1;
      TValuta UNDEFINED = ValutaFactory.Singleton().UNDEFINED;
      TValuta lastValuta = UNDEFINED;

      Decimal saldo = KontoBank.EroeffnungsKontostand;
      foreach (BankEntry entry in BankEntries)
      {
        if (lastValuta == UNDEFINED)
        {
          lastValuta = entry.Valuta;
        }
        if (lastValuta != entry.Valuta)
        {
          // The valuta must be ascending!
          Trace.Assert(lastValuta < entry.Valuta);

          // A new day started.
          // We store the saldo for the last day
          KontoDay kontoDay = KontoBank.KontoDays.GetDay(lastValuta);
          kontoDay.SetSaldoExpected(saldo);
          lastValuta = entry.Valuta;
          iBelegNumber = 1;
        }
        entry.BelegNr = iBelegNumber++;
        if (entry.Statement == BankStatement.Debit)
        {
          // Debit
          saldo -= entry.Betrag;
        }
        else
        {
          // Credit
          saldo += entry.Betrag;
        }
      }
      if (lastValuta != UNDEFINED)
      {
        KontoDay kontoDay = KontoBank.KontoDays.GetDay(lastValuta);
        kontoDay.SetSaldoExpected(saldo);
      }
    }

    public void ErrorIfWrongEndOfTheDaySaldo(Journal journal)
    {
      if (!BankFactory.KontostandUeberpruefen)
      {
        return;
      }

      foreach (KontoDay kontoDay in KontoBank.KontoDays.DaysOrdered)
      {
        // We now verify, if the saldo is correct.
        if (kontoDay.SaldoExpected == Decimal.MinValue)
        {
          // TODO: Verify that never gets here...
          continue;
        }
        if (kontoDay.Saldo != kontoDay.SaldoExpected)
        {
          decimal fehlbetrag = kontoDay.SaldoExpected - kontoDay.Saldo;
          kontoDay.MessagesErrors.Add($"Saldo sollte {kontoDay.SaldoExpected} sein, aber ist {kontoDay.Saldo}! Fehlbetrag {fehlbetrag} ({fehlbetrag/2})!");
        }
      }
    }

    public void AddBankreaderResult(Journal journal)
    {
      // Locate all entries in 'cashbox_mapping_<bank>.txt'.
      // If the entry is not found: remove reference.
      foreach (BankEntry bankEntry in BankEntries)
      {
        if (bankEntry.MappingToJournalReference == null)
        {
          continue;
        }

        // There should be a reference. Try to locate it.
        JournalDay journalDay = journal.JournalDays.GetDay(bankEntry.Valuta);
        IEnumerable<Entry> bankAccountEntries = KontoBank.WhereForThisKonto(journalDay.EntriesOrdered);
        Entry entry = null;
        try
        {
          entry = bankAccountEntries.First(e => (e.Referenz == bankEntry.MappingToJournalReference));
        }
        catch (InvalidOperationException) { }
        if (entry != null)
        {
          // We found it!
          entry.BankEntry = bankEntry;
          if (!bankEntry.BetragAbsoluteEquals(entry.Betrag))
          {
            entry.MessagesErrors.Add($"Der Betrag {N.F(entry.Betrag)} ist falsch. Im Bankauszug '{BankFactory.Name}', Konto {KontoBank.KontoNr}, steht {N.F(bankEntry.Betrag)}! Bitte Betrag korrigieren!");
          }
          if (entry.Valuta != bankEntry.Valuta)
          {
            entry.MessagesErrors.Add($"Das Datum {entry.Valuta} ist falsch. Im Bankauszug '{BankFactory.Name}', Konto {KontoBank.KontoNr}, steht {bankEntry.Valuta}! Bitte Datum korrigieren!");
          }
          continue;
        }

        // Not found: Reset
        bankEntry.MappingToJournalReference = null;
      }

      // Loop for the remaining bank-journal-entries (the ones with 'bankEntry.MappingToJournalReference == null') and try to find matching entries
      foreach (BankEntry bankEntry in BankEntries)
      {
        if (bankEntry.MappingToJournalReference != null)
        {
          // We already found this entry
          continue;
        }

        JournalDay journalDay = journal.JournalDays.GetDay(bankEntry.Valuta);
        IEnumerable<Entry> bankAccountEntries = KontoBank.WhereForThisKonto(journalDay.EntriesOrdered);
        Entry entryFound = null;
        foreach (Entry e in bankAccountEntries)
        {
          Trace.Assert(e.Valuta == bankEntry.Valuta);
          if (e.BankEntry == null)
          {
            // This Journal-Entry hasn't been assigned to a Bank-Entry yet.
            if (bankEntry.BetragAbsoluteEquals(e.Betrag))
            {
              // The Amount matches: We found it!
              entryFound = e;
              break;
            }
          }
        }
        if (entryFound != null)
        {
          entryFound.MessagesComments.Add(bankEntry.Comment);
          entryFound.BankEntry = bankEntry;
          bankEntry.MappingToJournalReference = entryFound.Referenz;
          continue;
        }
        bankEntryVorschlaege.Add(bankEntry);
      }
    }

    public void AddBuchungsvorschlaege(Journal journal)
    {
      if (BankFactory.AddBuchungsvorschlaege)
      {
        // Loop for the remaining bank-journal-entries and try to find matching entries
        foreach (BankEntry bankEntry in bankEntryVorschlaege)
        {
          JournalDay journalDay = journal.JournalDays.GetDay(bankEntry.Valuta);
          string referenz = journalDay.GetNextFreeReferenz();
          journalDay.AddEntry(new Entry(Config, KontoBank, bankEntry, referenz));
        }
      }

      // For all entries remaining in the journal which don't have a BankEntry assigned: Add Error
      foreach (JournalDay journalDay in journal.JournalDays.DaysOrdered)
      {
        foreach (Entry entry in KontoBank.WhereForThisKonto(journalDay.EntriesOrdered))
        {
          if (entry.BankEntry == null)
          {
            entry.MessagesErrors.Add($"Es wurde kein entsprechender Eintrag im Bankjournal '{BankFactory.Name}', Konto {KontoBank.KontoNr}, gefunden!");
          }
        }
      }
    }
  }

}
