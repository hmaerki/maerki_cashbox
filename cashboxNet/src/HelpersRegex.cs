using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace cashboxNet
{
  class ReferenzHelper
  {
    public static string DATETIME_FORMAT = "yyyy-MM-dd";

    /// <summary>
    /// 0 -> "a"
    /// 1 -> "b"
    /// 26 -> "aa"
    /// 27 -> "ab"
    /// </summary>
    public static string NumberToAlpha(int number)
    {
      string s = "";
      while (number >= 0)
      {
        s = (char)('a' + number % 26) + s;
        number /= 26;
        number--;
      }

      return s;
    }

    public static int AlphaToNumber(string alpha)
    {
      int number = -1;
      foreach (char c in alpha)
      {
        number++;
        number *= 26;
        number += ((int)c) - ((int)'a');
      }
      return number;
    }

    public static string FormatReferenz(TValuta valuta, int iBelegNr)
    {
      return valuta + NumberToAlpha(iBelegNr);
    }
  }

  public class DoNotTestAttribute : Attribute { }

  /// <summary>
  /// This abstract class allows for the derived classes the capsuling of the regular expression with named attributes and properties to access these named attributes.
  /// </summary>
  public abstract class RegexBase
  {
    public string Line { get; protected set; }
    public Match Match { get; protected set; }
    public Regex Regex { get; protected set; }

    protected RegexBase() { }

    public static bool TryMatch<T>(Regex regex, string line, out T regexOut) where T : RegexBase, new()
    {
      Match match = regex.Match(line);
      if (match.Success)
      {
        regexOut = new T { };
        regexOut.Line = line;
        regexOut.Regex = regex;
        regexOut.Match = match;
        return true;
      }
      regexOut = null;
      return false;
    }

    protected Entry.EnumVerb GetValueVerb(string name, bool optional = false)
    {
      string text = GetValueString(name, optional);
      if (Enum.TryParse(text.ToUpper(), out Entry.EnumVerb verb))
      {
        return verb;
      }
      throw new Exception($"'{text}' falsch!");
    }

    protected int GetValueInt(string name, bool optional = false)
    {
      string text = GetValueString(name, optional);
      if (int.TryParse(text, out int i))
      {
        return i;
      }
      throw new Exception($"'{text}' ist keine Ganzzahl!");
    }

    protected Decimal GetValueDecimal(string name, bool optional = false)
    {
      string text = GetValueString(name, optional);
      if (Decimal.TryParse(text, out Decimal d))
      {
        return d;
      }
      throw new Exception($"'{text}' ist keine Dezimal-Zahl!");
    }

    protected string GetValueString(string name, bool optional = false)
    {
      Group group = Match.Groups[name];
      if (group.Success)
      {
        return group.Value;
      }
      if (optional)
      {
        return null;
      }
      throw new NotImplementedException($"In regular expression '{Regex}': No named group '{name}'!");
    }

    /// <summary>
    /// Verify, if it is possible to access all properties, but not the ones with 'DoNotTestAttribute'.
    /// </summary>
    public void VerifyAll()
    {
      foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty))
      {
        DoNotTestAttribute attribute = property.GetCustomAttribute<DoNotTestAttribute>();
        if (attribute == null)
        {
          object value = property.GetValue(this);
        }
      }
    }
  }
}
