using NUnit.Framework;
using System;

namespace cashboxNet.Tests
{
  #region Nunit-Tests
  class TestRegexBeginBasic
  {
    [TestCase("todo xy", ExpectedResult = true)]
    [TestCase("todoxy", ExpectedResult = false)]
    [TestCase("fehler xy", ExpectedResult = true)]
    [TestCase("fehlerxy", ExpectedResult = false)]
    [TestCase("# Dies ist Kommentar", ExpectedResult = true)]
    [TestCase("#Dies ist Kommentar", ExpectedResult = true)]
    [TestCase("2017-09-05a Hallo", ExpectedResult = true)]
    public bool TestMuhFileBeginBasic(string line)
    {
      bool success = RegexBeginBasic.TryMatch(line, out RegexBeginBasic regex);
      if (success)
      {
        regex.VerifyAll();
      }
      return success;
    }
  }

  class TestRegexBeginReferenzVerbRest
  {
    [TestCase("2017-08-12ab b 12.30 auto-bar-SPESEN_HUBA Benzin ", ExpectedResult = true)]
    [TestCase("2017-08-12ab vorschlag 10.00 Blablaba", ExpectedResult = true)]
    [TestCase("2017-08-12a b 12.30 privat", ExpectedResult = true)]
    [TestCase("2017-08-12a f 12.30 privat", ExpectedResult = true)]
    [TestCase("2017-08-12a f 12.30 privat.pdf", ExpectedResult = true)]
    public bool TestMuhFileBeginReferenzVerbRest(string line)
    {
      bool success = RegexBeginReferenzVerbRest.TryMatch(line, out RegexBeginReferenzVerbRest regex);
      if (success)
      {
        regex.VerifyAll();
        Console.WriteLine($"Kommentar: '{regex.Kommentar}'");
        Console.WriteLine($"Buchungsanweisung: '{regex.Buchungsanweisung}'");
        Console.WriteLine($"Betrag: '{regex.Betrag}'");
        Console.WriteLine($"Verb: '{regex.Verb}'");
        Console.WriteLine($"Valuta: '{regex.Valuta}'");
      }
      return success;
    }
  }

  class TestRegexSubBetragAnweisungKommentar
  {
    [TestCase("12.30 auto-bar-SPESEN_HUBA Benzin ", ExpectedResult = true)]
    [TestCase("12.30 auto-bar-SPESEN_HUBA", ExpectedResult = true)]
    [TestCase("12.30", ExpectedResult = false)]
    [TestCase("12.30 ", ExpectedResult = false)]
    public bool TestMuhFileSubBetragAnweisungKommentar(string line)
    {
      bool success = RegexSubBetragAnweisungKommentar.TryMatch(line, out RegexSubBetragAnweisungKommentar regex);
      if (success)
      {
        regex.VerifyAll();
        Console.WriteLine($"Kommentar: '{regex.Kommentar}'");
        Console.WriteLine($"Buchungsanweisung: '{regex.Buchungsanweisung}'");
        Console.WriteLine($"Betrag: '{regex.Betrag}'");
      }
      return success;
    }
  }

  class TestRegexSubReferenz
  {
    [TestCase("2017-08-12", ExpectedResult = false)]
    [TestCase("2017-08-12a", ExpectedResult = true)]
    [TestCase("2017-08-12ab", ExpectedResult = true)]
    [TestCase("2017-08-12a0", ExpectedResult = false)]
    public bool TestMuhFileSubReferenz(string line)
    {
      bool success = RegexSubReferenz.TryMatch(line, out RegexSubReferenz regex);
      if (success)
      {
        regex.VerifyAll();
        Console.WriteLine($"Valuta: '{regex.Valuta}'");
      }
      return success;
    }
  }

  class TestRegexSubBetrag
  {
    [TestCase("12.50", ExpectedResult = true)]
    [TestCase("-12.50", ExpectedResult = true)]
    [TestCase("12.5", ExpectedResult = false)]
    [TestCase("-12.5", ExpectedResult = false)]
    [TestCase("12.500", ExpectedResult = false)]
    [TestCase("-12.500", ExpectedResult = false)]
    [TestCase("a12.50", ExpectedResult = false)]
    [TestCase("12.50a", ExpectedResult = false)]
    [TestCase("12.50", ExpectedResult = true)]
    [TestCase("-12.50", ExpectedResult = true)]
    [TestCase("12.5", ExpectedResult = false)]
    [TestCase("-12.5", ExpectedResult = false)]
    [TestCase("12.500", ExpectedResult = false)]
    [TestCase("-12.500", ExpectedResult = false)]
    [TestCase("a12.50", ExpectedResult = false)]
    [TestCase("12.50a", ExpectedResult = false)]
    [TestCase("12.50", ExpectedResult = true)]
    [TestCase("-12.50", ExpectedResult = true)]
    [TestCase("12.5", ExpectedResult = false)]
    [TestCase("-12.5", ExpectedResult = false)]
    [TestCase("12.500", ExpectedResult = false)]
    [TestCase("-12.500", ExpectedResult = false)]
    [TestCase("a12.50", ExpectedResult = false)]
    [TestCase("12.50a", ExpectedResult = false)]
    [TestCase("12.50", ExpectedResult = true)]
    [TestCase("-12.50", ExpectedResult = true)]
    [TestCase("12.5", ExpectedResult = false)]
    [TestCase("-12.5", ExpectedResult = false)]
    [TestCase("12.500", ExpectedResult = false)]
    [TestCase("-12.500", ExpectedResult = false)]
    [TestCase("a12.50", ExpectedResult = false)]
    [TestCase("12.50a", ExpectedResult = false)]
    public bool TestMuhFileSubBetrag(string line)
    {
      bool success = RegexSubBetrag.TryMatch(line, out RegexSubBetrag regex);
      if (success)
      {
        regex.VerifyAll();
        Console.WriteLine($"Betrag: '{regex.Betrag}'");
      }
      try
      {
        if (success)
        {
          regex.VerifyAll();
          Console.WriteLine($"Betrag: '{regex.Betrag}'");
        }
        return success;
      }
      catch (LineFehlerException)
      {
        return false;
      }
    }
  }
  #endregion
}