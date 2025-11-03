using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace cashboxNet
{
  public class BankRevolut : IBankFactory
  {
    public const string CURRENCY_CHF = "CHF";
    public string Filename { get; private set; }
    public string Directory { get { return Filename; } }
    public string Name { get; private set; }
    public readonly string Currency;
    public Konto KontoBank { get; private set; }
    private readonly int kontoNr;

    public bool KontostandUeberpruefen { get; private set; }
    public bool AddBuchungsvorschlaege { get { return true; } }

    public BankRevolut(int kontoNr_, string name, string directory = "revolut", string currency = CURRENCY_CHF, bool kontostandUeberpruefen = true)
    {
      kontoNr = kontoNr_;
      Name = name;
      Filename = directory;
      Currency = currency;
      KontostandUeberpruefen = kontostandUeberpruefen;
    }

    public IBankReader Factory(Configuration config, string directory)
    {
      return new BankReaderRevolut(config, directory, this);
    }

    public void UpdateByConfig(Configuration config)
    {
      KontoBank = config.FindKonto(kontoNr);
    }
  }

  class BankReaderRevolut : IBankReader
  {
    private readonly Configuration Config;
    private readonly string Directory;
    public IBankFactory BankFactory { get { return BankRevolut; } }
    public BankRevolut BankRevolut { get; private set; }

    public HashSet<string> LinesProcessed = new HashSet<string>();

    public BankReaderRevolut(Configuration config, string directory, BankRevolut bankRevolut)
    {
      Config = config;
      Directory = Path.Combine(directory, bankRevolut.Directory);
      BankRevolut = bankRevolut;
    }

    public bool TryGetInitialBalance(out decimal initialBalance)
    {
      initialBalance = 0.0M;
      return false;
    }
    public IEnumerable<BankEntry> ReadBankEntries()
    {
      return ReadBankEntriesPrivate().OrderBy(e => e.Valuta).ThenBy(e => e.LineNr);
    }

    /// <summary>
    /// Read all revolut-report files of the required currency.
    /// </summary>
    /// <returns></returns>
    private IEnumerable<BankEntry> ReadBankEntriesPrivate()
    {
      string pattern = $"account-statement_*.csv";

      foreach (string filename in System.IO.Directory.GetFiles(Directory, pattern))
      {
        RevolutFileReader rfr = new RevolutFileReader(this, filename);
        foreach (BankEntry bankEntry in rfr.GetBankEntries())
        {
          yield return bankEntry;
        }
      }
    }

    public class RevolutFileReader
    {
      /// <summary>
      /// Value correspoids to order of column in file!
      /// </summary>
      public enum Columns
      {
        ///   Type,Product,Started Date,Completed Date,Description,Amount,Fee,Currency,State,Balance
        TYPE,
        PRODUCT,
        STARTED_DATE,
        COMPLETED_DATE,
        DESCRIPTION,
        AMOUNT,
        FEED,
        CURRENCY,
        STATE,
        BALANCE
      }

      private string filename;
      public BankReaderRevolut bankReaderRevolut;

      public RevolutFileReader(BankReaderRevolut bankReaderRevolut, string filename)
      {
        // Revolut-CHF-Statement-März – Dez. 2019.csv
        this.bankReaderRevolut = bankReaderRevolut;
        this.filename = filename;
      }
      private string GetTopLine()
      {
        return "Type,Product,Started Date,Completed Date,Description,Amount,Fee,Currency,State,Balance";
      }
      public IEnumerable<BankEntry> GetBankEntries()
      {
        bool firstline = true;
        int lineNr = 0;
        foreach (string line in File.ReadLines(filename, encoding: System.Text.Encoding.UTF8))
        {
          lineNr++;
          if (firstline)
          {
            string topline = GetTopLine();
            Debug.Assert(line == topline);
            firstline = false;
            continue;
          }
          bool added = bankReaderRevolut.LinesProcessed.Add(line);
          if (!added)
          {
            // A line may be included in different files.
            // Skip duplicate lines.
            // Note about equality: The line also contains a saldo.
            continue;
          }
          BankEntry entry = GetBankEntry(lineNr, line);
          if (entry == null)
          {
            continue;
          }
          yield return entry;
        }
      }

      private IEnumerable<string> SplitLine(int lineNr, string line)
      {
        // CARD_PAYMENT,Current,2019-03-18 16:08:55,2019-03-19 14:07:26,"Sphères, bar, buch, bühne",-11.70,0.00,CHF,COMPLETED,393.80
        int begin = 0;
        while (true)
        {
          int end = line.IndexOf(',', begin);
          if (end == -1)
          {
            yield return line.Substring(begin);
            break;
          }
          // yield return line.Substring(begin, end);
          // begin = end+1;
          if (begin >= line.Length)
          {
            throw new Exception($"{this.filename}: Line '{lineNr}'. Unexected ',' at the end of the line!");
          }
          if (line[begin] == '"')
          {
            // A quoted field
            end = line.IndexOf('"', begin+1);
            if (end == -1)
            {
              throw new Exception($"{this.filename}: Line '{lineNr}'. Quoted string was not terminated!");
            }
            yield return line.Substring(begin + 1, end-begin-1);
            begin = end + 2;
            continue;
          }
          // A unquoted field
          yield return line.Substring(begin, end-begin);
          begin = end + 1;
        }
      }
      private BankEntry GetBankEntry(int lineNr, string line)
      {
        // string[] cols = line.Split(new char[] { ',', });
        string[] cols = SplitLine(lineNr, line).ToArray();
        cols = cols.Select(l => l.Trim()).ToArray();
        if (cols.Length != 10) {
          throw new Exception($"{this.filename}: Line '{lineNr}'. Expected 10 columns but got {cols.Length}!");
        }

        string state = cols[(int)Columns.STATE];
        string fee = cols[(int)Columns.FEED];
        string started_date = cols[(int)Columns.STARTED_DATE];
        string completed_date = cols[(int)Columns.COMPLETED_DATE];
        string amountForeign = cols[(int)Columns.AMOUNT];
        string description = cols[(int)Columns.DESCRIPTION];
        string currency = cols[(int)Columns.CURRENCY];
        string balanceForeign = cols[(int)Columns.BALANCE];
        string type = cols[(int)Columns.TYPE];

        if (currency != bankReaderRevolut.BankRevolut.Currency)
        {
          return null;
        }

        if (state == "PENDING")
        {
          return null;
        }
        if (state == "REVERTED")
        {
          return null;
        }
        if (state != "COMPLETED")
        {
          throw new Exception($"{this.filename}: Line '{lineNr}'. Expected 'COMPLETED' but got '{state}'!");
        }

        // "2021-01-04 07:27:37"
        // "2019-04-03 8:00:30"  <== Wrong formatted!
        const string DATETIME_FORMAT = "yyyy-MM-dd H:mm:ss";
        DateTime dt = DateTime.ParseExact(completed_date, DATETIME_FORMAT, CultureInfo.InvariantCulture);
        TValuta Valuta = new TValuta(dt);

        decimal FeeForeign = decimal.Parse(fee, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        decimal AmountForeign = decimal.Parse(amountForeign, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        decimal BalanceForeign = 0.0M;
        if (balanceForeign != "")
        {
          decimal.Parse(balanceForeign, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }
        bool is_foreign = currency != BankRevolut.CURRENCY_CHF;
        if (is_foreign)
        {
          description = $"{type} {currency}{amountForeign.Replace("-", "")} {description}";
        }

        double rate = 1.0;
        if (!bankReaderRevolut.Config.ExchangeRate.TryGetValue(currency, out rate))
        {
          throw new Exception($"Unknown currency '{currency}'");
        }
        decimal Betrag = N.Round1Rappen((decimal)(rate * ((float)(AmountForeign - FeeForeign))));
        // decimal Betrag = N.Round1Rappen((decimal)(rate * ((float)AmountForeign)));
        decimal Balance = N.Round1Rappen((decimal)(rate * ((float)BalanceForeign)));

        string fee2 = (fee == "0.00") ? "" : $"/Fee {fee}";
        //string foreign = is_foreign ? $"/{currency} {AmountForeign}" : "";
        string foreign = $"/{currency} {AmountForeign}";
        string Buchungstext = $"{Path.GetFileName(filename)}({lineNr}):{type}/{state}{foreign}{fee2}: {description}";
        /*
        BankStatement HandlePaymentType()
        {
          if (type == "FEE")
          {
            return BankStatement.Debit;
          }
          if (type == "ATM")
          {
            return BankStatement.Debit;
          }
          if (type == "CARD_PAYMENT")
          {
            return BankStatement.Debit;
          }
          if (type == "CARD_REFUND")
          {
            return BankStatement.Debit;
          }
          if (type == "CARD_CHARGEBACK")
          {
            return BankStatement.Credit;
          }
          if (type == "CASHBACK")
          {
            return BankStatement.Credit;
          }
          if (type == "CHARGEBACK_REVERSAL")
          {
            return BankStatement.Debit;
          }
          if (type == "TOPUP")
          {
            return BankStatement.Debit;
          }
          if (type == "EXCHANGE")
          {
            return BankStatement.Credit;
          }
          if (type == "TRANSFER")
          {
            return BankStatement.Debit;
          }
          throw new Exception($"{this.filename}({lineNr}): Unknown type '{type}' ({line})");
        }
        BankStatement bankStatement = HandlePaymentType();
        */

        BankStatement bankStatement = (Betrag < 0.00M) ? BankStatement.Debit : BankStatement.Credit;
        if (bankStatement == BankStatement.Debit)
        {
          Betrag = -Betrag;
        }
        /*
        BankStatement bankStatement =  BankStatement.Debit;
        Betrag = -Betrag;
        */

        return new BankEntry(bankReaderRevolut.BankFactory, lineNr, Valuta, description, Betrag, bankStatement, comment: Buchungstext);
      }
    }
  }
}

