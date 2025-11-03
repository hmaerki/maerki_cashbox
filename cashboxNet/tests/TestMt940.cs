using NUnit.Framework;
using Raptorious.SharpMt940Lib;
using Raptorious.SharpMt940Lib.Mt940Format;
using System;
using System.Globalization;
using System.Text;

namespace cashboxNet
{
  class TestMt940
  {
    [Ignore("Will fail with System.IO.InvalidDataException(): 'Can not find trailer!'")]
    [TestCase("tests/TestBankreaderMt940/journal_MT940.sta", ExpectedResult = true)]
    public bool TestMt940Samples(string filename)
    {
      string filename_absolute = Directories.expect_filename_project_root(filename);
      Parameters mt940Parameters = new Parameters { };
      // mt940Parameters.Encoding = Encoding.GetEncoding(encoding);
      var header = new Separator("");
      var footer = new Separator("");

      var result = Mt940Parser.Parse(new GenericFormat(header, footer), filename_absolute, CultureInfo.InvariantCulture, mt940Parameters);

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
