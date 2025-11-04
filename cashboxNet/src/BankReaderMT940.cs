using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Raptorious.SharpMt940Lib;
using Raptorious.SharpMt940Lib.Mt940Format;

namespace cashboxNet
{
    class BankReaderMT940 : IBankReader
    {
        private readonly Configuration Config;
        private readonly string Directory;
        private string Fileencoding;
        private bool LeadingLineNumber;

        // "000288482914810000000007946 CHF         2'516.40 / 000288482914810000000007962 CHF         2'106.00 / Gutschrift VESR"
        //  000000000000000000000       Dummy
        //                       00794  Vesr
        //                            6 Checksum
        private static Regex reVesr = new Regex(@"^(?<dummy>000\d{18})(?<VESR>\d{5})(?<CHECKSUM>\d) CHF +(?<CHF>[\d\']+\.\d\d)$");
        public IBankFactory BankFactory { get; private set; }

        public BankReaderMT940(Configuration config, string directory, IBankFactory bankFactory, string fileEncoding = "ISO-8859-1", bool leadingLineNumber = true)
        {
            Config = config;
            Directory = directory;
            BankFactory = bankFactory;
            Fileencoding = fileEncoding;
            LeadingLineNumber = leadingLineNumber;
        }

        public bool TryGetInitialBalance(out decimal initialBalance)
        {
            initialBalance = 0;
            return false;
        }

        public IEnumerable<BankEntry> ReadBankEntries()
        {
            // Bei der Raiffeisenbank sind die Einträge NICHT immer aufsteigend!
            return ReadBankEntriesPrivate().OrderBy(e => e.Valuta).ThenBy(e => e.LineNr);
        }

        private IEnumerable<BankEntry> ReadBankEntriesPrivate()
        {
            string filename = Path.Combine(Directory, BankFactory.Filename);
            Parameters mt940Parameters = new Parameters();
            // mt940Parameters.Encoding = Encoding.GetEncoding(Fileencoding);
            mt940Parameters.Encoding = Encoding.GetEncoding("ISO-8859-1");
            mt940Parameters.LeadingLineNumber = LeadingLineNumber;

            var header = new Separator("");
            var footer = new Separator("");
            var result = Mt940Parser.Parse(new GenericFormat(header, footer), filename, CultureInfo.InvariantCulture, mt940Parameters);

            int i = 1;
            foreach (CustomerStatementMessage customer in result)
            {
                foreach (Transaction transaction in customer.Transactions)
                {
                    Trace.Assert(transaction.Amount.Currency.Code == "CHF");
                    {
                        // Eine Buchung kann mehrere Vesr-Buchung umfassen...

                        // Normales Statement:
                        // "Strassenverkehrsamt Kanton Zürich / Strassenverkehrsamt Kanton Zürich / Uetlibergstr. 301des Kantons Zürich / 8036 Zürich / E-Banking Auftrag (E-Rechnung)"

                        // Mehrere Buchung:
                        // "000288482914810000000007946 CHF         2'516.40 / 000288482914810000000007962 CHF         2'106.00 / Gutschrift VESR"
                        string[] list = transaction.Description.Split('/');
                        if (list[list.Length - 1].Contains(" Gutschrift VESR"))
                        {
                            Trace.Assert(transaction.DebitCredit == DebitCredit.Credit);
                            decimal control_sum = 0m;
                            for (int v = 0; v < list.Length - 1; v++)
                            {
                                string buchung = list[v].Trim();
                                // buchung: "000288482914810000000007946 CHF         2'516.40"
                                Match m = reVesr.Match(buchung);
                                string vesr = m.Groups["VESR"].Value;
                                string amount_ = m.Groups["CHF"].Value;
                                // Decimal amount = Decimal.Parse(amount_, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                                // Decimal amount = Decimal.Parse(amount_, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands);
                                amount_ = amount_.Replace("'", "");
                                Decimal amount = Decimal.Parse(amount_, CultureInfo.InvariantCulture);
                                BankEntry bankEntry_ = new BankEntry(BankFactory, lineNr: i++, valuta: new TValuta(transaction.ValueDate), buchungstext: $"Gutschrift VESR {vesr}", betrag: amount, statement: BankStatement.Credit, vesr: vesr);
                                yield return bankEntry_;
                                control_sum += amount;
                            }
                            Trace.Assert(control_sum == transaction.Amount.Value);
                            continue;
                        }
                    }

                    BankStatement statement = BankStatement.Debit;
                    switch (transaction.DebitCredit)
                    {
                        case DebitCredit.Credit:
                        case DebitCredit.RD:
                            statement = BankStatement.Credit;
                            break;
                        case DebitCredit.Debit:
                        case DebitCredit.RC:
                            statement = BankStatement.Debit;
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    BankEntry bankEntry = new BankEntry(BankFactory, lineNr: i++, valuta: new TValuta(transaction.ValueDate), buchungstext: transaction.Description, betrag: transaction.Amount.Value, statement: statement);
                    yield return bankEntry;
                }
            }
        }
    }
}
