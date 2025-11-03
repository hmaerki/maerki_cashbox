using NUnit.Framework;
using Raptorious.SharpMt940Lib;
using Raptorious.SharpMt940Lib.Mt940Format;
using System;
using System.Globalization;
using System.IO;

namespace cashboxNet
{
  class TestISO2022
  {
    [Ignore("Not maintained anymore...")]
    [Test]
    public void TestBuildReferenceFiles()
    {
      ValutaFactory.SingletonReset();
      ValutaFactory f = ValutaFactory.SingletonInit("2000-01-01");

      dumpBankTestfile(new BankRaiffeisen(1020, "Raiffeisen", "journal_raiffeisen_hans_2018.xml"));
      dumpBankTestfile(new BankRaiffeisen(1020, "Raiffeisen", "journal_raiffeisen_peter_2017.mt940"));
      dumpBankTestfile(new BankRaiffeisen(1020, "Raiffeisen", "journal_raiffeisen_peter_2018.mt940"));
      dumpBankTestfile(new BankRaiffeisen(1020, "Raiffeisen", "journal_raiffeisen_peter_2017.xml"));
      dumpBankTestfile(new BankRaiffeisen(1020, "Raiffeisen", "journal_raiffeisen_peter_2018.xml"));
    }

    private static void dumpBankTestfile(IBankFactory factory)
    {
      Configuration config = null;
      string directory = Directories.expect_directory_project_root("tests/TestsBankreaderISO2022");
      IBankReader br = factory.Factory(config, directory);
      // string filename_ = Path.Combine(directory, $"{Path.GetFileName(factory.Filename)}_legacy_out.txt");
      string filename_ = Path.Combine(directory, $"{Path.GetFileName(factory.Filename)}_out.txt");
      using (TextWriter tw = new StreamWriter(filename_))
      {
        if (br.TryGetInitialBalance(out decimal initialBalance))
        {
          tw.WriteLine($"InitialBalance {initialBalance:C2}");
        }
        foreach (BankEntry entry in br.ReadBankEntries())
        {
          // tw.WriteLine($"{entry.Valuta} {entry.Reference} {entry.Statement} '{entry.VESR}' {entry.Betrag:N2} {entry.BankBuchungstext} /// {entry.Comment}");
          tw.WriteLine($"{entry.Valuta} {entry.Reference} {entry.Statement} {entry.Betrag:N2} '{entry.VESR}' {entry.BankBuchungstext}");
          tw.Flush();
        }
      }
    }

    [Ignore("Not maintained anymore...")]
    [TestCase("UTF-8", @"D:\cashbox\trunk\hikizi\2016\journal_MT940.sta", ExpectedResult = true)]
    [TestCase("ISO-8859-1", @"D:\cashbox\branches\cashboxNet\bosshard\Raiffeisen Andre\SWIFT Jahresauszug mit Details - Konto_20170902183453.mt940", ExpectedResult = true)]
    public bool TestMt940Samples(string encoding, string filename)
    {
      Parameters mt940Parameters = new Parameters { };
      // mt940Parameters.Encoding = Encoding.GetEncoding(encoding);
      var header = new Separator("");
      var footer = new Separator("");
      var result = Mt940Parser.Parse(new GenericFormat(header, footer), filename, CultureInfo.InvariantCulture, mt940Parameters);

      Console.WriteLine($"Count: {result.Count}");
      foreach (CustomerStatementMessage statement in result)
      {
        Console.WriteLine($"Description OpeningBalance: {statement.Description} {statement.OpeningBalance}");
        int i = 0;
        foreach (Transaction transaction in statement.Transactions)
        {
          if (i++ > 5)
          {
            break;
          }
          Console.WriteLine($"{transaction.ValueDate} {transaction.DebitCredit} {transaction.Amount} '{transaction.Description}'");
        }
      }


      return true;
    }
  }
}
