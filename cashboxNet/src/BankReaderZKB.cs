using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

/*
  Dieses Modul liest eine CSV-Datei der ZKB ein.
  Die Methode iterator() liefert
            yield {
                 'strLine': dictLine['strLine'],
                 'strValuta': strValuta,
                 'strBankBuchungstext': dictLine['Buchungstext'],
                 'fBetrag': fBetrag,
                 'iKontoSoll': iKontoSoll,
                 'iKontoHaben': iKontoHaben,
          }
*/
namespace cashboxNet
{
    #region internal implementation
    class LineBase
    {
        public readonly string Line;
        public readonly int LineNr;
        public readonly string[] Fields;
        protected string GetField(EnumFields field) { return Fields[(int)field]; }
        private string[] stringSeparators = new string[] { "\";\"" };

        public string Buchungstext { get { return GetField(EnumFields.BUCHUNGSTEXT) + " " + GetField(EnumFields.ZAHLUNGSZWECK); } }
        public string Datum { get { return GetField(EnumFields.DATUM); } }
        // "03.07.2017"
        private static string[] DATETIME_FORMATS = { "dd.MM.yyyy", };
        public TValuta Valuta
        {
            get
            {
                if (DateTime.TryParseExact(Datum, DATETIME_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                {
                    return new TValuta(date);
                }
                throw new Exception($"'{Datum}' ist kein Datum!");
            }
        }
        public string SaldoCHF { get { return GetField(EnumFields.SALDO_CHF); } }

        protected static bool TryGetDecimal(LineBase lineBase, EnumFields field, out Decimal value)
        {
            string betrag = lineBase.GetField(field);
            // 3'406.68 -> 3406.68
            betrag = betrag.Replace("'", "");
            if (betrag == "")
            {
                value = default(Decimal);
                return false;
            }
            value = Decimal.Parse(betrag);
            return true;
        }

        public bool TryGetDecimal(EnumFields field, out Decimal value)
        {
            return TryGetDecimal(this, field, out value);
        }

        // Ab Nov. 2014: "Datum";"Buchungstext";"Whg";"Betrag Detail";"ZKB-Referenz";"Referenznummer";"Belastung CHF";"Gutschrift CHF";"Valuta";"Saldo CHF"; "Zahlungszweck"
        public enum EnumFields { DATUM, BUCHUNGSTEXT, WHG, BETRAG_DETAIL, ZKBREFERENZ, REFERENZNUMMER, BELASTUNG_CHF, GUTSCHRIFT_CHF, VALUTA, SALDO_CHF, ZAHLUNGSZWECK };


        public LineBase(int lineNr, string line)
        {
            LineNr = lineNr;
            Line = line;

            // line:
            // "26.06.2017";"Belastung eBanking: Digitec, 8005 Zuerich";"";"";"Z171777574344";"";"24.30";"";"26.06.2017";"49'956.58";""
            Trace.Assert(line[0] == '"');
            Trace.Assert(line[line.Length - 1] == '"');
            string line2 = line.Substring(1, line.Length - 2);
            Fields = line2.Split(stringSeparators, StringSplitOptions.None);
        }

        public void AssertEmpty(EnumFields[] listKeys)
        {
            foreach (EnumFields field in listKeys)
            {
                string value = GetField(field);
                if (value != "")
                {
                    throw new Exception($"Expected '{field}={value}' to be empty! Line: {Line}");
                }
            }
        }

        public void AssertFilled(EnumFields[] listKeys)
        {
            foreach (EnumFields field in listKeys)
            {
                string value = GetField(field);
                if (value == "")
                {
                    throw new Exception($"Expected '{field}={value}' not to be empty! Line: {Line}");
                }
            }
        }

        public void AssertOr(EnumFields fieldA, EnumFields fieldB)
        {
            bool a = GetField(fieldA) == "";
            bool b = GetField(fieldB) == "";
            if (b == a)
            {
                throw new Exception($"'{fieldA}/{fieldB}': Expected one of both fields to be empty.! Line: {Line}");
            }
        }
    }

    class LineMaster : LineBase
    {
        protected List<LineBase> LinesSub = new List<LineBase>();
        private IBankFactory BankFactory;
        public LineMaster(IBankFactory factory, int lineNr, string line) : base(lineNr, line)
        {
            BankFactory = factory;
        }

        public void AddLineSub(LineBase lineSub)
        {
            LinesSub.Add(lineSub);
        }

        /*
        def getKonto(dictLine):
          iKontoSoll = iKontoHaben = 9999
          strBelastung = dictLine['Belastung CHF']
          strGutschrift = dictLine['Gutschrift CHF']
          # 3'406.68 -> 3406.68
          strBelastung = strBelastung.replace("'", "")
          strGutschrift = strGutschrift.replace("'", "")
          if strGutschrift != '':
            iKontoHaben = self.iKontoBank
            strBetrag = strGutschrift
          else:
            iKontoSoll = self.iKontoBank
            strBetrag = strBelastung
          fBetrag = float (strBetrag)
          return fBetrag, iKontoSoll, iKontoHaben
        */

        public class KontoResult
        {
            public readonly Decimal Betrag;
            public readonly BankStatement Statement = BankStatement.Debit;
            private readonly Configuration Config;
            private readonly LineMaster Master;
            private readonly Konto KontoBank;

            private bool TryGetDecimal(EnumFields field, out Decimal value)
            {
                return Master.TryGetDecimal(field, out value);
            }
            public KontoResult(Configuration config, LineMaster master, Konto kontoBank)
            {
                Config = config;
                Master = master;
                KontoBank = kontoBank;

                if (TryGetDecimal(EnumFields.GUTSCHRIFT_CHF, out Decimal gutschrift))
                {
                    Statement = BankStatement.Credit;
                    Betrag = gutschrift;
                    return;
                }
                if (TryGetDecimal(EnumFields.BELASTUNG_CHF, out Decimal belastung))
                {
                    Statement = BankStatement.Debit;
                    Betrag = belastung;
                    return;
                }
            }
        }

        public IEnumerable<BankEntry> Update(Configuration config, Konto kontoBank)
        {
            if (LinesSub.Count == 0)
            {
                // Beispiel:
                // "15.12.2007","DEPOTGEBUEHR",,"95.60","","31.12.2007","505.69"
                AssertFilled(new EnumFields[] { EnumFields.DATUM, EnumFields.BUCHUNGSTEXT, EnumFields.VALUTA, EnumFields.SALDO_CHF });
                AssertOr(EnumFields.BELASTUNG_CHF, EnumFields.GUTSCHRIFT_CHF);
                AssertEmpty(new EnumFields[] { EnumFields.BETRAG_DETAIL });
                KontoResult result = new KontoResult(config, this, kontoBank);
                Trace.Assert(result.Betrag > 0M);
                /*
                if fBetrag < 0.0:
                  # Ende 2010 gabs im Journal das erste Mal einen negativen Betrag - eine Storno-Buchung.
                  # Diese Baenkler sind so was von inkonsequent...
                  fBetrag, iKontoSoll, iKontoHaben = -fBetrag, iKontoHaben, iKontoSoll
                 */
                /*
                fBetrag, iKontoSoll, iKontoHaben = getKonto(dictMasterLine)
                yield {
                       'strValuta': strValuta,
                       'strBankBuchungstext': dictMasterLine['Buchungstext'],
                       'fBetrag': fBetrag,
                       'iKontoSoll': iKontoSoll,
                       'iKontoHaben': iKontoHaben,
                }
                */
                yield return new BankEntry(BankFactory, LineNr, Valuta, Buchungstext, result.Betrag, result.Statement);
                yield break;
            }
            if (LinesSub.Count == 1)
            {
                // Beispiel:
                // "13.12.2007","ONLINEBANK Auftrags-Nr. 7419-1213-7030-0001",,"2657.00","","13.12.2007","601.29"
                // ,"OEL-HAUSER AG 8820 WAEDENSWIL","",,,,
                LineBase lineSub = LinesSub[0];
                AssertFilled(new EnumFields[] { EnumFields.DATUM, EnumFields.BUCHUNGSTEXT, EnumFields.VALUTA, EnumFields.SALDO_CHF });
                AssertOr(EnumFields.BELASTUNG_CHF, EnumFields.GUTSCHRIFT_CHF);
                lineSub.AssertEmpty(new EnumFields[] { EnumFields.DATUM, EnumFields.ZKBREFERENZ, EnumFields.REFERENZNUMMER, EnumFields.BELASTUNG_CHF, EnumFields.GUTSCHRIFT_CHF });
                /*
                dictMasterLine['Buchungstext'] = dictLine['Buchungstext']
                fBetrag, iKontoSoll, iKontoHaben = getKonto(dictMasterLine)
                yield {
                  'strValuta': strValuta,
                       'strBankBuchungstext': dictLine['Buchungstext'],
                       'fBetrag': fBetrag,
                       'iKontoSoll': iKontoSoll,
                       'iKontoHaben': iKontoHaben,
                }
                */
                KontoResult result = new KontoResult(config, this, kontoBank);
                yield return new BankEntry(BankFactory, lineSub.LineNr, Valuta, lineSub.Buchungstext, result.Betrag, result.Statement);
                yield break;
            }

            Trace.Assert(LinesSub.Count > 1);
            // Beispiel:
            // "10.12.2007","ONLINEBANK Auftrags-Nr. 7416-1210-7510-0001",,"279.80","","10.12.2007","4489.44"
            // ,"NOTARIAT UND GRUNDBUCHAMT","75.00",,,,
            // ,"NOTARIAT UND GRUNDBUCHAMT","75.00",,,,
            // ,"ERNST TOBLER GARAGE ZINKEREISTR. 16 8633 WOLFHAUSEN","53.80",,,,
            // ,"SACHA ZALA SCHILLINGSTRASSE 30 CH-3005 BERN","30.00",,,,
            // ,"PETER HOFER TIERARZT 8633 WOLFHAUSEN","46.00",,,,

            /*
            fBetrag, iKontoSoll, iKontoHaben = getKonto(dictMasterLine)
            */
            foreach (LineBase lineSub in LinesSub)
            {
                AssertFilled(new EnumFields[] { EnumFields.DATUM, EnumFields.BUCHUNGSTEXT, EnumFields.VALUTA, EnumFields.SALDO_CHF });
                AssertOr(EnumFields.BELASTUNG_CHF, EnumFields.GUTSCHRIFT_CHF);
                lineSub.AssertFilled(new EnumFields[] { EnumFields.BUCHUNGSTEXT, EnumFields.BETRAG_DETAIL });
                lineSub.AssertEmpty(new EnumFields[] { EnumFields.DATUM, EnumFields.ZKBREFERENZ, EnumFields.BELASTUNG_CHF, EnumFields.GUTSCHRIFT_CHF, EnumFields.VALUTA, EnumFields.SALDO_CHF });

                /*
                 fBetrag, iKontoSoll, iKontoHaben = getKonto(dictMasterLine)
                 strBetragDetail = dictLine['Betrag Detail']
                 # 3'406.68 -> 3406.68
                 strBetragDetail = strBetragDetail.replace("'", "")
                 fBetrag = float(strBetragDetail)
                 yield {
               'strValuta': strValuta,
                        'strBankBuchungstext': dictLine['Buchungstext'],
                        'fBetrag': fBetrag,
                        'iKontoSoll': iKontoSoll,
                        'iKontoHaben': iKontoHaben,
                 }
                 */
                if (!lineSub.TryGetDecimal(EnumFields.BETRAG_DETAIL, out Decimal betrag))
                {
                    throw new Exception($"BETRAG_DETAIL in line {lineSub.LineNr} is empty! Line: {lineSub.Line}");
                }
                KontoResult result = new KontoResult(config, this, kontoBank);
                yield return new BankEntry(BankFactory, lineSub.LineNr, Valuta, lineSub.Buchungstext, betrag, result.Statement);
            }
            yield break;
        }
    }
    #endregion

    public class BankZKB : IBankFactory
    {
        public string Filename { get; private set; }
        public string Name { get; private set; }
        private readonly int kontoNr;
        public Konto KontoBank { get; private set; }
        public bool KontostandUeberpruefen { get; private set; }
        public bool AddBuchungsvorschlaege { get { return KontostandUeberpruefen; } }

        public BankZKB(int kontoNr_, string name, string filename = "journal_zkb.csv", bool kontostandUeberpruefen = true)
        {
            kontoNr = kontoNr_;
            Name = name;
            Filename = filename;
            KontostandUeberpruefen = kontostandUeberpruefen;
        }
        public IBankReader Factory(Configuration config, string directory)
        {
            return new BankReaderZKB(config, directory, this);
        }

        public void UpdateByConfig(Configuration config)
        {
            KontoBank = config.FindKonto(kontoNr);
        }
    }

    class BankReaderZKB : IBankReader
    {
        private readonly Configuration Config;
        private readonly string Directory;
        public IBankFactory BankFactory { get; private set; }

        public BankReaderZKB(Configuration config, string directory, BankZKB bankFactory)
        {
            Config = config;
            Directory = directory;
            BankFactory = bankFactory;
        }

        public bool TryGetInitialBalance(out decimal initialBalance)
        {
            initialBalance = 0;
            return false;
        }

        public IEnumerable<BankEntry> ReadBankEntries()
        {
            // Bei der ZKB sind die Einträge NICHT immer aufsteigend!
            return ReadBankEntriesPrivate().OrderBy(e => e.Valuta).ThenBy(e => e.LineNr);
        }

        private IEnumerable<BankEntry> ReadBankEntriesPrivate()
        {
            List<LineMaster> linesMaster = new List<LineMaster>(ReadMasterlines());
            linesMaster.Reverse();
            foreach (LineMaster lineMaster in linesMaster)
            {
                foreach (BankEntry bankEntry in lineMaster.Update(Config, BankFactory.KontoBank))
                {
                    yield return bankEntry;
                }
            }
        }

        #region internal implementation
        private IEnumerable<LineMaster> ReadMasterlines()
        {
            LineMaster lineMaster = null;
            foreach (LineBase line in ReadFile())
            {
                if (line.LineNr == 1)
                {
                    Trace.Assert(line.Datum == "Datum");
                    Trace.Assert(line.SaldoCHF == "Saldo CHF");
                    continue;
                }

                if (line.LineNr == 2)
                {
                    // Die zweite Linie ist IMMER LineMaster (mit Datum).
                    lineMaster = (LineMaster)line;
                    continue;
                }

                LineMaster masterNew = line as LineMaster;
                if (masterNew == null)
                {
                    lineMaster.AddLineSub((LineBase)line);
                    continue;
                }
                yield return lineMaster;
                lineMaster = masterNew;
            }
            yield return lineMaster;
        }

        private IEnumerable<LineBase> ReadFile()
        {
            int lineNr = 0;
            foreach (string line in File.ReadLines(Path.Combine(Directory, BankFactory.Filename)))
            {
                lineNr++;
                if (line.StartsWith("\"\";\""))
                {
                    yield return new LineBase(lineNr, line);
                }
                else
                {
                    yield return new LineMaster(BankFactory, lineNr, line);
                }
            }
        }
        #endregion
    }
}

