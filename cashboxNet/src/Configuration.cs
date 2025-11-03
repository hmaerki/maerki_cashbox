using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace cashboxNet
{
  /// <summary>
  /// This class will be instantiated by the CSScript-configuration-file 'cashbox_config_vorschlaege.cs'.
  ///
  /// "STRASSENVERKEHRSAMT DES KT.ZUERICH", "auto", "Strassenverkehrsabgabe"
  /// 
  /// When 'STRASSENVERKEHRSAMT DES KT.ZUERICH' is found in the Bank-statements, the Buchungsvorschlag 'auto' is used.
  /// The text 'Strassenverkehrsabgabe' will decorated the 'Entry'.
  /// </summary>
  public class Buchungsvorschlag
  {
    /// <summary>
    /// STRASSENVERKEHRSAMT DES KT.ZUERICH
    /// </summary>
    public readonly string Suchtext;

    /// <summary>
    /// auto
    /// </summary>
    public Buchungsvorlage Vorlage { get; private set; }

    /// <summary>
    /// auto-FAHRZEUG
    /// </summary>
    public string VorlageTextAll { get; private set; }

    /// <summary>
    /// Strassenverkehrsabgabe
    /// </summary>
    public readonly string Buchungstext;

    /// <summary>
    /// If set to false, a 'vorschlag' will be written in the muh2-file. The is manual interaction needed to change 'vorschlag' to 'buchung'.
    /// If set to true, a 'buchung' will be written n the muh2-file. No manual interaction is needed anymore.
    /// </summary>
    public readonly bool DirektBuchen;

    public Buchungsvorschlag(Configuration config, string suchtext, string vorlageTextAll, string buchungstext, bool direktBuchen)
    {
      Suchtext = suchtext;
      VorlageTextAll = vorlageTextAll;
      Buchungstext = buchungstext;
      DirektBuchen = direktBuchen;

      Action<string> actionError = (error) =>
      {
        throw new CashboxException(error);
      };
      RegexSubBuchungsanweisungen anweisung = new RegexSubBuchungsanweisungen(vorlageTextAll, actionError);
      Action<Buchungsvorlage> actionVorlage = (vorlage) => { Vorlage = vorlage; };
      Action<string> actionTag = (tag) => { };
      Action<string, KontoErsatz> actionKontoErsatz = (einzelanweisung, kontoErsatz) => { };
      Action<string, MwstSatz> actionMwstSatz = (einzelanweisung, mwstSatz) => { };
      // auto-ohneMwst-FAHRZEUG
      config.HandleBuchungsanweisungen(anweisung.Buchungsanweisungen, actionError, actionVorlage, actionTag, actionKontoErsatz, actionMwstSatz);

      Trace.Assert(Vorlage != null);
    }
  }

  /// <summary>
  /// This class will be instantiated by the CSScript-configuration-file 'cashbox_config_vorlagebuchungen.cs'.
  /// 
  /// 1021, 6500, "verbrauchhw", "VSB80", "Verbrauchsmaterial Hardware");
  /// </summary>
  public class Buchungsvorlage
  {
    /// <summary>
    /// 1021
    /// </summary>
    public readonly Konto KontoHaben;

    /// <summary>
    /// 6500
    /// </summary>
    public readonly Konto KontoSoll;

    /// <summary>
    /// Z. B. privat-revolut_hans
    /// </summary>
    public KontoErsatz KontoErsatz = null;

    /// <summary>
    /// VSB80
    /// </summary>
    public readonly MwstSatz MWST;

    /// <summary>
    /// verbrauchhw
    /// </summary>
    private string VorlageTextPrivate;

    /// <summary>
    /// Verbrauchsmaterial Hardware
    /// </summary>
    public readonly string BuchungsText;

    public Buchungsvorlage(Configuration config, string vorlageText, int kontoHaben, int kontoSoll, string mwst = "", string buchungsText = "")
    {
      KontoHaben = config.FindKonto(kontoHaben);
      KontoSoll = config.FindKonto(kontoSoll);

      ExceptionIfInvalidCombination(config, vorlageText);

      config.FindMwstSatz(mwst, out MWST, (error) => { throw new CashboxException(error); });
      VorlageTextPrivate = vorlageText;
      BuchungsText = buchungsText;
    }

    private Buchungsvorlage(Buchungsvorlage vorlage, KontoErsatz kontoErsatz)
    {
      KontoHaben = vorlage.KontoHaben;
      KontoSoll = vorlage.KontoSoll;
      MWST = vorlage.MWST;
      VorlageTextPrivate = vorlage.VorlageTextPrivate;
      BuchungsText = vorlage.BuchungsText;
      KontoErsatz = kontoErsatz;
    }

    public Buchungsvorlage WithKontoErsatz(KontoErsatz kontoErsatz)
    {
      return new Buchungsvorlage(this, kontoErsatz);
    }

    public string VorlageText
    {
      get
      {
        if (KontoErsatz != null)
        {
          return VorlageTextPrivate + RegexSubBuchungsanweisungen.BUCHUNG_SEPARATOR + KontoErsatz.Tag;
        }
        return VorlageTextPrivate;
      }
    }

    private void ExceptionIfInvalidCombination(Configuration config, string vorlageText)
    {
      if (KontoHaben.IsAktivenPassiven & KontoSoll.IsAktivenPassiven)
      {
        // Ein Übertrag von einem Bankkonto auf das andere ist erlaubt.
        // 'bancomat': Von der Bank (Aktiven) auf Privat (Passiven)
        return;
      }

      if (KontoHaben.IsAktivenPassiven == KontoSoll.IsEinnahmenAusgaben)
      {
        // Vorlage 'dienstleistungenDritter': Konto 1021 ist AKTIVEN. Konto 4400 ist AUSGABEN.
        // Vorlage 'einnahmen': Konto 3400 ist EINNAHMEN. Konto 1021 ist AKTIVEN.
        return;
      }

      string msg = $"Vorlage '{vorlageText}': Es sind nur gewisse Konto-Kombinationen erlaubt: Konto {KontoHaben.KontoNr} ist {KontoHaben.KontoType}. Konto {KontoSoll.KontoNr} ist {KontoSoll.KontoType}.";
      throw new CashboxException(msg);
    }
  }

  /// <summary>
  /// Reduce typing in 'cashbox_config_vorlagebuchungen.cs'.
  /// </summary>
  public class BuchungsvorlageHelper
  {
    private readonly Configuration Config;

    public BuchungsvorlageHelper(Configuration config)
    {
      Config = config;
    }
    public void Add(int kontoHaben, int kontoSoll, string vorlageText, string mwst, string buchungsText)
    {
      Config.AddBuchungsvorlage(new Buchungsvorlage(Config, vorlageText, kontoHaben, kontoSoll, mwst: mwst, buchungsText: buchungsText));
    }
  }

  /// <summary>
  /// Reduce typing in 'cashbox_config_kontenplan.cs'.
  /// </summary>
  public class KontenplanHelper
  {
    private readonly Configuration Config;

    public KontenplanHelper(Configuration config)
    {
      Config = config;
    }

    public void Add(int kontoNr, string text)
    {
      Konto konto = new Konto(Config, actualKontoType, kontoNr, text);
      try
      {
        Config.Kontenplan.Add(kontoNr, konto);
      }
      catch (System.ArgumentException)
      {
        throw new CashboxException($"Das Konto '{kontoNr}' wurde bereits zugefügt.");
      }

      if (actualStructureSub != null)
      {
        actualStructureSub.Add(konto);
      }
    }

    #region Grouping-Helpers
    private enum GroupSequence { START, BILANZ_AKTIVEN, BILANZ_PASSIVEN, BILANZ_END, ERFOLG_EINNAHMEN, ERFOLG_AUSGABEN, ERFOLG_END };
    private GroupSequence actualGroupSequenceStatus = GroupSequence.START;

    private void AssertAndIncrement(GroupSequence seq)
    {
      if (actualGroupSequenceStatus != seq)
      {
        throw new CashboxException("Falsche Reihenfolge der Gruppierung!");
      }
      int tmp = (int)actualGroupSequenceStatus;
      tmp++;
      actualGroupSequenceStatus = (GroupSequence)tmp;
    }

    private void Assert(GroupSequence seqA, GroupSequence seqB)
    {
      if ((actualGroupSequenceStatus != seqA) && (actualGroupSequenceStatus != seqB))
      {
        throw new CashboxException("Falsche Reihenfolge der Gruppierung!");
      }
    }
    #endregion

    #region Grouping
    private ConfigurationClosingStructureSection actualStructureSub = null;
    private KontoType actualKontoType = KontoType.AKTIVEN;

    public void GroupBilanzAktiven()
    {
      AssertAndIncrement(GroupSequence.START);
      Trace.Assert(actualStructureSub == null);
      actualStructureSub = Config.ClosingStructureBilanz.A;
      actualKontoType = KontoType.AKTIVEN;
    }

    public void GroupBilanzPassiven()
    {
      AssertAndIncrement(GroupSequence.BILANZ_AKTIVEN);
      Trace.Assert(actualStructureSub != null);
      actualStructureSub = Config.ClosingStructureBilanz.B;
      actualKontoType = KontoType.PASSIVEN;
    }

    public void GroupBilanzEnd()
    {
      AssertAndIncrement(GroupSequence.BILANZ_PASSIVEN);
      Trace.Assert(actualStructureSub != null);
      actualStructureSub = null;
    }

    public void GroupBilanz_(string name)
    {
      Assert(GroupSequence.BILANZ_AKTIVEN, GroupSequence.BILANZ_PASSIVEN);
      Trace.Assert(actualStructureSub != null);
      actualStructureSub.Add(name);
    }

    public void GroupErfolgsrechnungEinnahmen()
    {
      AssertAndIncrement(GroupSequence.BILANZ_END);
      Trace.Assert(actualStructureSub == null);
      actualStructureSub = Config.ClosingStructureErfolgsrechnung.A;
      actualKontoType = KontoType.EINNAHMEN;
    }

    public void GroupErfolgsrechnungAusgaben()
    {
      AssertAndIncrement(GroupSequence.ERFOLG_EINNAHMEN);
      Trace.Assert(actualStructureSub != null);
      actualStructureSub = Config.ClosingStructureErfolgsrechnung.B;
      actualKontoType = KontoType.AUSGABEN;
    }

    public void GroupErfolgsrechnungEnd()
    {
      AssertAndIncrement(GroupSequence.ERFOLG_AUSGABEN);
      Trace.Assert(actualStructureSub != null);
      actualStructureSub = null;
    }

    public void GroupErfolgsrechnung_(string name)
    {
      Assert(GroupSequence.ERFOLG_EINNAHMEN, GroupSequence.ERFOLG_AUSGABEN);
      Trace.Assert(actualStructureSub != null);
      actualStructureSub.Add(name);
    }
    #endregion
  }

  /// <summary>
  /// Reduce typing in 'cashbox_config_vorschlaege.cs'.
  /// </summary>
  public class VorschlagHelper
  {
    private readonly Configuration Config;

    public VorschlagHelper(Configuration config)
    {
      Config = config;
    }
    public void Add(string suchtext, string vorlage, string buchungstext = "", bool direktBuchen = false)
    {
      Config.Buchungsvorschlaege.Add(new Buchungsvorschlag(Config, suchtext, vorlage, buchungstext, direktBuchen));
    }
  }

  public class ConfigurationProgramArguments
  {
    private CommandLineOptions options;

    public string Directory { get; private set; }

    public bool CreateTags { get { return options.CreateTags; } }
    public bool CreateTxt { get { return options.CreateTxt; } }
    public bool CreateHtml { get { return options.CreateHtml; } }
    public bool CreatePdf { get { return options.CreatePdf; } }
    public string FileVersionTimestamp { get { return options.FileVersionTimestamp; } }
    public bool KeepMuhFile { get { return options.KeepMuhFile; } }

    /// <summary>
    /// Returns the full filename of the artefact has to be created.
    /// If the user doesn't need the artefact. the artefact is removed and 'null' returned.
    /// </summary>
    public string CreateTagsOrDelete(string filename) { return CreateOrDelete(CreateTags, filename); }
    public string CreateTxtOrDelete(string filename) { return CreateOrDelete(CreateTxt, filename); }
    public string CreatePdfOrDelete(string filename) { return CreateOrDelete(CreatePdf, filename); }
    public string CreateHtmlOrDelete(string filename) { return CreateOrDelete(CreateHtml, filename); }

    public string CreateOrDelete(bool create, string filename)
    {
      string filenameFull = Path.Combine(Directory, filename);
      if (!create)
      {
        if (File.Exists(filenameFull))
        {
          File.Delete(filenameFull);
        }
        return null;
      }

      return filenameFull;
    }

    public ConfigurationProgramArguments(CommandLineOptions options_, string directory)
    {
      options = options_;
      Directory = directory;
    }
  }

  public class KontoErsatz
  {
    public readonly Konto Konto;
    public readonly string Tag;
    public readonly Konto[] KontoToReplace;
    public KontoErsatz(Konto konto, string tag, Konto[] kontoToReplace)
    {
      Konto = konto;
      Tag = tag;
      KontoToReplace = kontoToReplace;
    }

    public bool Contains(Konto kontoHaben)
    {
      return KontoToReplace.Contains(kontoHaben);
    }
  }

  public class Directories
  {
    public const string DIRECTORY_TRACE = "out_trace";

    public static string directory_project_root()
    {
      // Get the project root directory by going up from the assembly location
      // Assembly is in: cashboxNet/bin/Debug/net9.0/cashboxNet.dll
      // We need:        cashboxNet/tests/TestBankreaderRevolut
      string assemblyLocation = Assembly.GetExecutingAssembly().Location;
      string binDir = Path.GetDirectoryName(assemblyLocation); // .../bin/Debug/net9.0
      string projectRoot = Path.GetFullPath(Path.Combine(binDir, "..", "..", "..")); // Go up 3 levels to cashboxNet/
      string resources = Path.Combine(projectRoot, "resources");
      if (!Directory.Exists(resources))
      {
        throw new CashboxException($"Resources directory not found: {resources}");
      }
      return projectRoot;
    }

    public static string expect_directory_project_root(string subdirectory)
    {
      string directory = Path.Combine(directory_project_root(), subdirectory);

      if (!Directory.Exists(directory))
      {
        throw new DirectoryNotFoundException($"Directory not found: {directory}");
      }

      return directory;
    }
    public static string expect_filename_project_root(string filename)
    {
      string filename_absolute = Path.Combine(directory_project_root(), filename);

      if (!File.Exists(filename_absolute))
      {
        throw new DirectoryNotFoundException($"File not found: {filename_absolute}");
      }

      return filename_absolute;
    }
  }

  /// <summary>
  /// This class will be inflated by
  ///   'cashbox_config_jahr.cs'
  ///   'cashbox_config_kontenplan.cs'
  ///   'cashbox_config_vorlagebuchungen.cs'
  ///   'cashbox_config_vorschlaege.cs'
  /// </summary>
  public class Configuration
  {
    /// <summary>
    /// The arguments given at the command line
    /// </summary>
    public ConfigurationProgramArguments ProgramArguments { get; private set; }

    /// <summary>
    /// Beschreibt die Abfolge der Zeilen in der Erfolgsrechnung / Bilanz
    /// </summary>
    public ConfigurationClosingStructure ClosingStructureErfolgsrechnung = new ConfigurationClosingStructure();
    public ConfigurationClosingStructure ClosingStructureBilanz = new ConfigurationClosingStructure();

    /// <summary>
    /// 'cashbox_config_kontenplan.cs'
    /// </summary>
    public List<string> Tags = new List<string>();

    /// <summary>
    ///   'cashbox_config_vorlagebuchungen.cs'
    /// </summary>
    public Dictionary<string, MwstSatz> MwstSaetze = new Dictionary<string, MwstSatz>();

    /// <summary>
    ///   'cashbox_config_vorlagebuchungen.cs'
    /// </summary>
    public List<Buchungsvorlage> Abschlussbuchungen = new List<Buchungsvorlage>();

    /// <summary>
    ///   'cashbox_config_vorlagebuchungen.cs'
    /// </summary>
    public Buchungsvorlage Gewinnbuchung { get; private set; }

    /// <summary>
    ///   'cashbox_config_vorlagebuchungen.cs'
    /// </summary>
    public Dictionary<string, KontoErsatz> KontoErsaetze = new Dictionary<string, KontoErsatz>();

    /// <summary>
    ///   'cashbox_config_jahr.cs'
    /// </summary>
    public void SetEroeffnungKontostand(int kontoNr, decimal eroeffnungsKontostand)
    {
      Konto konto = FindKonto(kontoNr);
      konto.SetEroeffnungKontostand(eroeffnungsKontostand);
    }

    /// <summary>
    /// 'cashbox_config_kontenplan.cs'
    /// </summary>
    public Dictionary<int, Konto> Kontenplan = new Dictionary<int, Konto>();
    public IEnumerable<Konto> KontoOrdered { get { return Kontenplan.Values.OrderBy(k => k.KontoNr); ; } }

    /// <summary>
    /// 'cashbox_config_vorlagebuchungen.cs'
    /// </summary>
    public Dictionary<string, Buchungsvorlage> Buchungsvorlagen = new Dictionary<string, Buchungsvorlage>();

    /// <summary>
    /// 'cashbox_config_vorlagebuchungen.cs'
    /// </summary>
    public void SetBuchungsvorlageNichtGefunden(int kontoNr, string buchungsvorlageCredit, string buchungsvorlageDebit = null, string kontoErsatz = null)
    {
      Konto konto = FindKonto(kontoNr);
      KontoErsatz kontoErsatz_ = null;
      if (kontoErsatz != null)
      {
        if (!KontoErsaetze.TryGetValue(kontoErsatz, out kontoErsatz_))
        {
          string ersaetze = string.Join(",", KontoErsaetze.Keys);
          throw new CashboxException($"Nicht gefunden: konto_ersatz: '{kontoErsatz_}' nicht in [{ersaetze}]");
        }
      }

      if (buchungsvorlageDebit == null)
      {
        // string msg = $"SetBuchungsvorlageNichtGefunden() hat ein neues drittes Argument!";
        // throw new CashboxException(msg);
        buchungsvorlageDebit = buchungsvorlageCredit;
      }

      konto.BuchungsvorlageNichtGefundenCredit = FindBuchungsvorlageException(buchungsvorlageCredit).WithKontoErsatz(kontoErsatz_);
      konto.BuchungsvorlageNichtGefundenDebit = FindBuchungsvorlageException(buchungsvorlageDebit).WithKontoErsatz(kontoErsatz_);

      if (buchungsvorlageNichtGefundenCredit == null)
      {
        buchungsvorlageNichtGefundenCredit = konto.BuchungsvorlageNichtGefundenCredit;
      }
      if (buchungsvorlageNichtGefundenDebit == null)
      {
        buchungsvorlageNichtGefundenDebit = konto.BuchungsvorlageNichtGefundenDebit;
      }
    }

    /// <summary>
    /// 'cashbox_config_vorschlaege.cs'
    /// </summary>
    public List<Buchungsvorschlag> Buchungsvorschlaege = new List<Buchungsvorschlag>();

    /// <summary>
    /// The bankimport-files we are going to use for this 'Mandant'.
    /// 'cashbox_config_kontenplan.cs'
    /// </summary>
    private List<IBankFactory> bankFactories = new List<IBankFactory>();

    /// <summary>
    /// Maerki Informatik
    /// </summary>
    public string Mandant { get; set; }

    /// <summary>
    /// 2017 or 2016/2017
    /// </summary>
    public string Jahr { get; set; }

    /// <summary>
    /// 2016-07-01
    /// </summary>
    public DateTime DateStart
    {
      get { return dateStart; }
      set
      {
        dateStart = value;
        ValutaFactory.SingletonInit(DateStart);
        DateStartValuta = new TValuta(DateStart);
      }
    }
    private DateTime dateStart;
    public TValuta DateStartValuta { get; private set; }

    /// <summary>
    /// { {"EUR", 1.08}, {"USD", 0.89} }
    /// </summary>
    public Dictionary<string, double> ExchangeRate
    {
      get { return exchangeRate; }
      set
      {
        exchangeRate = value;
      }
    }
    private Dictionary<string, double> exchangeRate;

    /// <summary>
    /// These are the pluggable constraints which will be produce error-messages or warnings..
    /// 'cashbox_config_kontenplan.cs'
    /// </summary>
    private List<IConstraint> constraints = new List<IConstraint>();

    /// <summary>
    /// 2017-06-31
    /// </summary>
    public DateTime DateEnd
    {
      get { return dateEnd; }
      set
      {
        dateEnd = value;
        DateEndValuta = new TValuta(DateEnd);
      }
    }
    private DateTime dateEnd;
    public TValuta DateEndValuta { get; private set; }


    /// <summary>
    /// 2016-07-01 bis 2017-06-31
    /// </summary>
    public string DatePeriod { get { return $"{DateStartValuta} bis {DateEndValuta}"; } }

    public string Title { get { return $"Buchhaltung {Mandant} {Jahr}"; } }
    public string TitleShort { get { return $"{Jahr} {Mandant}"; } }

    private Buchungsvorlage buchungsvorlageNichtGefundenCredit = null;
    private Buchungsvorlage buchungsvorlageNichtGefundenDebit = null;


    public static string DATETIME_FULL_FORMAT = "yyyy-MM-dd HH:mm:ss";

    private string FormatTimeStamp(DateTime dateTime)
    {
      if (ProgramArguments.FileVersionTimestamp != null)
      {
        return ProgramArguments.FileVersionTimestamp;
      }
      return dateTime.ToString(DATETIME_FULL_FORMAT);
    }
    private DateTime muhFileTimeStamp;
    public string MuhFileTimeStamp { get { return FormatTimeStamp(muhFileTimeStamp); } }
    public void SetMuhFileTimeStamp(DateTime timeStamp)
    {
      muhFileTimeStamp = timeStamp;
    }
    private DateTime now = DateTime.Now;
    public string NowTimeStamp { get { return FormatTimeStamp(now); } }

    public Configuration(ConfigurationProgramArguments args)
    {
      ProgramArguments = args;

      if (!Directory.Exists(Directories.DIRECTORY_TRACE))
      {
        Directory.CreateDirectory(Directories.DIRECTORY_TRACE);
      }
    }

    public decimal Exchange(string currency, double amount)
    {
      return N.Round1Rappen((decimal)(amount * ExchangeRate[currency]));
    }

    public void AddBuchungsvorlage(Buchungsvorlage buchungsvorlage)
    {
      try
      {
        Buchungsvorlagen.Add(buchungsvorlage.VorlageText, buchungsvorlage);
      }
      catch (ArgumentException)
      {
        throw new CashboxException($"Die Buchungsvorlage '{buchungsvorlage.VorlageText}' wurde bereits definiert.");
      }
    }

    /// <summary>
    /// Vordefinierte Konten
    /// Z. B. 2017-01-04b 12.60 buch-bar Buchhaltung für Dummies
    /// </summary>
    public void AddErsatz(int kontoNr, string tag, params int[] kontoNrToReplace)
    {
      Konto konto = FindKonto(kontoNr);

      // The following line will throw an exception if the Konty may not be found!
      Konto[] kontoToReplace = kontoNrToReplace.Select(nr => FindKonto(nr)).ToArray();

      try
      {
        KontoErsaetze.Add(tag, new KontoErsatz(konto, tag, kontoToReplace));
      }
      catch (ArgumentException)
      {
        throw new CashboxException($"Für Konto {kontoNr} wurde bereits ein Definition vorgenommen!");
      }
    }

    public void AddAbschlussbuchung(string vorlage)
    {
      Abschlussbuchungen.Add(FindBuchungsvorlageException(vorlage));
    }

    public void AddGewinnbuchung(string vorlage)
    {
      Gewinnbuchung = FindBuchungsvorlageException(vorlage);
    }

    public Buchungsvorlage FindBuchungsvorlageException(string vorlage)
    {
      if (Buchungsvorlagen.TryGetValue(vorlage, out Buchungsvorlage vorlage_))
      {
        return vorlage_;
      }
      throw new CashboxException($"Buchungsvorlage '{vorlage}' nicht gefunden!");
    }

    public Konto FindKonto(int kontoNr)
    {
      if (Kontenplan.TryGetValue(kontoNr, out Konto konto))
      {
        return konto;
      }
      throw new CashboxException($"Konto '{kontoNr}' nicht gefunden!");
    }

    /// <summary>
    /// return true on success (if 'errorHandler' was NOT called)
    /// </summary>
    public bool FindMwstSatz(string mwst, out MwstSatz mwstSatz, Action<string> errorHandler)
    {
      mwstSatz = null;
      if (mwst == "")
      {
        return true;
      }

      if (MwstSaetze.TryGetValue(mwst, out MwstSatz mwstSatz_))
      {
        mwstSatz = mwstSatz_;
        return true;
      }

      errorHandler($"MWST Satz '{mwst}' ist nicht bekannt!");
      return false;
    }

    public void Add(IBankFactory bankFactory)
    {
      bankFactory.UpdateByConfig(this);
      bankFactories.Add(bankFactory);
    }

    public void Add(IConstraint constraint)
    {
      constraint.UpdateByConfig(this);
      constraints.Add(constraint);
    }

    public Buchungsvorschlag FindVorschlag(string bankBuchungstext)
    {
      foreach (Buchungsvorschlag vorschlag in Buchungsvorschlaege)
      {
        if (bankBuchungstext.Contains(vorschlag.Suchtext))
        {
          return vorschlag;
        }
      }
      return null;
    }

    public List<BankreaderResult> CreateBankReaderResults()
    {
      List<BankreaderResult> bankreaderResults = new List<BankreaderResult>();
      foreach (IBankFactory factory in bankFactories)
      {
        IBankReader bankreader = factory.Factory(this, ProgramArguments.Directory);
        bankreaderResults.Add(new BankreaderResult(this, bankreader));
      }
      return bankreaderResults;
    }

    public void ApplyBookkeepingConstraints(Journal journal, BookkeepingBook book)
    {
      foreach (IConstraint constraint in constraints)
      {
        constraint.ApplyBookkeepingConstraints(this, journal, book);
      }
    }

    /// <summary>
    /// For each 'Einzelanweisung' (Example 'auto-ohneMwst-FAHRZEUG'): Try to find the corresponding objects.
    /// If found, call 'actionVorlage()' etc.
    /// If not found, call 'actionError()'
    /// </summary>
    public void HandleBuchungsanweisungen(List<string> Buchungsanweisungen, Action<string> actionError, Action<Buchungsvorlage> actionVorlage, Action<string> actionTag, Action<string, KontoErsatz> actionKontoErsatz, Action<string, MwstSatz> actionMwstSatz)
    {
      // auto-ohneMwst-FAHRZEUG
      bool first = true;
      foreach (string einzelanweisung in Buchungsanweisungen)
      {
        if (first)
        {
          first = false;
          // The first 'Einzelanweisung' MUSS eine 'Buchungsvorlage' sein!
          if (Buchungsvorlagen.TryGetValue(einzelanweisung, out Buchungsvorlage vorlage))
          {
            // Do something with 'vorlage'
            actionVorlage(vorlage);
            continue;
          }
          actionError($"Buchungsvorlage '{einzelanweisung}' nicht gefunden!");
          actionVorlage(buchungsvorlageNichtGefundenCredit);
          actionVorlage(buchungsvorlageNichtGefundenDebit);
          continue;
        }
        if (Tags.Contains(einzelanweisung))
        {
          // Do something with 'Tag'
          actionTag(einzelanweisung);
          continue;
        }
        if (KontoErsaetze.TryGetValue(einzelanweisung, out KontoErsatz kontoErsatz))
        {
          actionKontoErsatz(einzelanweisung, kontoErsatz);
          continue;
        }
        if (FindMwstSatz(einzelanweisung, out MwstSatz mwstSatz, actionError))
        {
          actionMwstSatz(einzelanweisung, mwstSatz);
          continue;
        }
        actionError($"Vorlage '{einzelanweisung}' nicht gefunden!");
        continue;
      }
    }

    public void Validate()
    {
      if (buchungsvorlageNichtGefundenCredit == null)
      {
        throw new CashboxException($"In '{Core.CONFIGUARTION_FILE_VORLAGEBUCHUNGEN}' muss mindestens einmal 'SetBuchungsvorlageNichtGefunden()' aufgerufen werden!");
      }
      if (Gewinnbuchung == null)
      {
        throw new CashboxException($"In '{Core.CONFIGUARTION_FILE_VORLAGEBUCHUNGEN}' muss 'AddGewinnbuchung(\"...\")' aufgerufen werden!");
      }
      foreach (IBankFactory factory in bankFactories)
      {
        Konto kontoBank = factory.KontoBank;
        if (kontoBank.BuchungsvorlageNichtGefundenCredit == null)
        {
          throw new CashboxException($"In '{Core.CONFIGUARTION_FILE_VORLAGEBUCHUNGEN}' fehlt 'config.SetBuchungsvorlageNichtGefunden({kontoBank.KontoNr}, \"privat\");'!");
        }
      }
    }

    public IMwstAbrechnung MwstAbrechnung = null;
  }
}
