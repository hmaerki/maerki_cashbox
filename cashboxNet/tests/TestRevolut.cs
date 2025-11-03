using NUnit.Framework;
using System.IO;

namespace cashboxNet
{
  [TestFixture]
  class TestRevolut
  {
    [Ignore("Not maintained anymore...")]
    [Test]
    public void TestBuildReferenceFiles()
    {
      ValutaFactory.SingletonReset();
      ValutaFactory f = ValutaFactory.SingletonInit("2000-01-01");

      dumpBankTestfile(new BankRevolut(1085, "Revolut", "."));
      dumpBankTestfile(new BankRevolut(1085, "Revolut", "."));
    }

    private static void dumpBankTestfile(BankRevolut bankRevolut)
    {
      Configuration config = null;

      string directory = Directories.expect_directory_project_root("tests/TestBankreaderRevolut");

      IBankReader br = bankRevolut.Factory(config, directory);
      string filename_out = Path.Combine(directory, "revolut_out.txt");
      // filename: C:\Projekte\hans\cashbox-revolut\cashboxNet\tests\TestBankreaderRevolut\revolut\Revolut-EUR-Statement-März – Dez. 2019.csv
      using (TextWriter tw = new StreamWriter(filename_out))
      {
        if (br.TryGetInitialBalance(out decimal initialBalance))
        {
          tw.WriteLine($"InitialBalance {initialBalance:C2}");
        }
        foreach (BankEntry entry in br.ReadBankEntries())
        {
          // tw.WriteLine($"{entry.Valuta} {entry.Reference} {entry.Statement} '{entry.VESR}' {entry.Betrag:N2} {entry.BankBuchungstext} /// {entry.Comment}");
          tw.WriteLine($"{entry.LineNr} {entry.Valuta} {entry.Reference} {entry.Statement} {entry.Betrag:N2} {entry.BankBuchungstext}");
          tw.Flush();
        }
      }
    }
  }

}
