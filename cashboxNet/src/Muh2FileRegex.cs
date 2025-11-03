using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace cashboxNet
{
  #region Regular Expressions
  /// <summary>
  /// Example: verb rest a b c
  /// </summary>
  public class RegexBeginBasic : RegexBase
  {
    public const string VERB_COMMENT = "#";
    public const string VERB_TODO = "todo";
    public const string VERB_FEHLER = "fehler";

    public static Regex regexStatic = new Regex($@"^(?<verb>.*?) (?<rest>.*)$", RegexOptions.Compiled);
    public bool IsTodoOrFehlerOrComment { get { return (Verb == VERB_TODO) || (Verb == VERB_FEHLER) || (Verb == VERB_COMMENT); } }

    public string Verb { get { return GetValueString("verb"); } }
    public string Rest { get { return GetValueString("rest", optional: true); } }

    public static bool TryMatch(string line, out RegexBeginBasic regex)
    {
      return RegexBase.TryMatch(regexStatic, line, out regex);
    }
  }

  /// <summary>
  /// Example: yyyy-mm-ddab verb rest a b c
  /// </summary>
  public class RegexBeginReferenzVerbRest : RegexBase
  {
    public static Regex regexStatic = new Regex($@"^(?<referenz>.*?)(\s+)(?<verb>[a-z]+)(?<rest>.*)$", RegexOptions.Compiled);

    public const string VERB_FILEBUCHUNG = "f";
    public const string VERB_BUCHUNG = "b";
    public const string VERB_BUCHUNGSVORSCHLAG = "vorschlag";

    public string Referenz { get { return GetValueString("referenz"); } }
    public string Rest { get { return GetValueString("rest").Trim(); } }
    public Entry.EnumVerb Verb { get; private set; }
    public string VerbText { get; private set; }

    #region Sub-Regular-Expressions
    private RegexSubBetragAnweisungKommentar regexRest = null;
    private RegexSubReferenz regexReferenz = null;
    public TValuta Valuta { get { return regexReferenz.Valuta; } }
    [DoNotTestAttribute]
    public Decimal Betrag { get { return regexRest.Betrag; } }
    [DoNotTestAttribute]
    public string Kommentar { get { return regexRest.Kommentar; } }
    [DoNotTestAttribute]
    public string Buchungsanweisung = null;
    [DoNotTestAttribute]
    public List<string> Buchungsanweisungen = null;
    #endregion

    public static bool TryMatch(string line, out RegexBeginReferenzVerbRest regex)
    {
      bool success = RegexBase.TryMatch(regexStatic, line, out regex);

      if (!success)
      {
        return false; // No match
      }

      if (!RegexSubReferenz.TryMatch(regex.Referenz, out regex.regexReferenz))
      {
        throw new LineFehlerException($"Ungültige Referenz: '{regex.Referenz}'!");
      }

      string verb = regex.GetValueString("verb");
      regex.VerbText = verb;
      switch (verb)
      {
        case VERB_FILEBUCHUNG:
          regex.Verb = Entry.EnumVerb.FILE_BUCHUNG;
          break;
        case VERB_BUCHUNG:
          regex.Verb = Entry.EnumVerb.BUCHUNG;
          break;
        case VERB_BUCHUNGSVORSCHLAG:
          regex.Verb = Entry.EnumVerb.BUCHUNGSVORSCHLAG;
          break;
        default:
          throw new LineFehlerException($"'{verb}' ist ungültig. Es wurde '{VERB_FILEBUCHUNG}' (Filebuchung) oder '{VERB_BUCHUNG}' (Buchung) erwartet!");
      };

      switch (verb)
      {
        case VERB_BUCHUNG:
        case VERB_FILEBUCHUNG:
        case VERB_BUCHUNGSVORSCHLAG:
          if (!RegexSubBetragAnweisungKommentar.TryMatch(regex.Rest, out regex.regexRest))
          {
            throw new LineFehlerException($"Ungültig '{regex.Rest}'!");
          }

          Action<string> errorHandler = (msg) =>
          {
            throw new LineFehlerException(msg);
          };
          RegexSubBuchungsanweisungen regexBuchungsanweisungen = new RegexSubBuchungsanweisungen(regex.regexRest.Buchungsanweisung, errorHandler);
          regex.Buchungsanweisung = regex.regexRest.Buchungsanweisung;
          regex.Buchungsanweisungen = regexBuchungsanweisungen.Buchungsanweisungen;
          break;
      }

      return true; // Match
    }
  }

  /// <summary>
  /// Example: 139.00 auto-FAHRZEUG Strassenverkehrsabgabe
  /// Example: 139.00 auto-FAHRZEUG Strassenverkehrsabgabe.pdf
  /// Example: 139.00 auto.pdf
  /// </summary>
  public class RegexSubBetragAnweisungKommentar : RegexBase
  {
    public static Regex regexStatic = new Regex($@"^(?<betrag>.+?)(\s+)(?<buchungsanweisung>.+?)((\s+)(?<kommentar>.+?))?(?<ext>\.([a-z]+))?$", RegexOptions.Compiled);

    public decimal Betrag
    {
      get
      {
        string s = GetValueString("betrag");
        if (decimal.TryParse(s, out decimal betrag))
        {
          return betrag;
        }
        throw new LineFehlerException($"'{s}' ist kein Betrag!");
      }
    }
    public string Buchungsanweisung { get { return GetValueString("buchungsanweisung"); } }
    public string Kommentar { get { return GetValueString("kommentar", optional: true); } }
    public string Fileextension { get { return GetValueString("ext", optional: true); } }

    public static bool TryMatch(string line, out RegexSubBetragAnweisungKommentar regex)
    {
      bool success = RegexBase.TryMatch(regexStatic, line, out regex);

      if (!success)
      {
        return false; // No match
      }

      return true; // Match
    }
  }

  /// <summary>
  /// Example: 2017-08-12ab
  /// </summary>
  public class RegexSubReferenz : RegexBase
  {
    public static Regex regexStatic = new Regex($@"^(?<year>\d\d\d\d)-(?<month>\d\d)-(?<day>\d\d)(?<tagesreferenz>[a-z]+)$", RegexOptions.Compiled);
    public int Year { get { return GetValueInt("year"); } }
    public int Month { get { return GetValueInt("month"); } }
    public int Day { get { return GetValueInt("day"); } }
    public TValuta Valuta { get { return new TValuta(new DateTime(year: Year, month: Month, day: Day)); } }
    public string Tagesreferenz { get { return GetValueString("tagesreferenz"); } }

    public static bool TryMatch(string line, out RegexSubReferenz regex)
    {
      return RegexBase.TryMatch(regexStatic, line, out regex);
    }
  }

  /// <summary>
  /// Example: -12.30
  /// </summary>
  class RegexSubBetrag : RegexBase
  {
    public static Regex regexStatic = new Regex($@"^(-?)\d+\.\d\d$", RegexOptions.Compiled);
    public Decimal Betrag { get { return Decimal.Parse(Match.Value); } }

    public static bool TryMatch(string line, out RegexSubBetrag regex)
    {
      return RegexBase.TryMatch(regexStatic, line, out regex);
    }
  }

  /// <summary>
  /// Example: auto-FAHRZEUG
  /// </summary>
  public class RegexSubBuchungsanweisungen
  {
    public const char BUCHUNG_SEPARATOR = '-';

    private static char[] separator = { BUCHUNG_SEPARATOR };
    private static Regex regexStatic = new Regex($@"^([a-zA-Z])([a-zA-Z0-9_])*$", RegexOptions.Compiled);

    // auto-bar-SPESEN_HUBA
    public List<string> Buchungsanweisungen { get; private set; }

    public RegexSubBuchungsanweisungen(string anweisungen, Action<string> errorHandler)
    {
      Buchungsanweisungen = anweisungen.Split(separator).ToList();
      foreach (string anweisung in Buchungsanweisungen)
      {
        Match match = regexStatic.Match(anweisung);
        if (!match.Success)
        {
          errorHandler($"'{anweisung}' in '{anweisungen}' falsch. Gültige Bespiele: 'hallo-Velo-test_5'!");
          return;
        }
      }
    }
  }
  #endregion

}
