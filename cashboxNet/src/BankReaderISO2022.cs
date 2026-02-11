using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace cashboxNet
{
    class BankReaderISO20200 : IBankReader
    {
        private readonly Configuration Config;
        private readonly string Directory;
        public IBankFactory BankFactory { get; private set; }

        private decimal initialBalance;
        private bool hasInitialBalance;

        public BankReaderISO20200(Configuration config, string directory, IBankFactory bankFactory)
        {
            Config = config;
            Directory = directory;
            BankFactory = bankFactory;
        }

        public bool TryGetInitialBalance(out decimal initialBalance_)
        {
            initialBalance_ = initialBalance;
            return hasInitialBalance;
        }
        public IEnumerable<BankEntry> ReadBankEntries()
        {
            // Bei der Raiffeisenbank sind die Einträge NICHT immer aufsteigend!
            return ReadBankEntriesPrivate().OrderBy(e => e.Valuta).ThenBy(e => e.LineNr);
        }

        class Node
        {
            public class NodeContext
            {
                private string filename;
                public XmlNamespaceManager nsMgr;
                public XmlDocument xmlDoc;
                public bool xml_version2019;

                public NodeContext(string filename_)
                {
                    filename = filename_;
                    xmlDoc = new XmlDocument();
                    xmlDoc.Load(filename);
                    string tns = xmlDoc.DocumentElement.Attributes["xmlns"].Value;
                    xml_version2019 = tns == "urn:iso:std:iso:20022:tech:xsd:camt.053.001.08";
                    nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
                    nsMgr.AddNamespace("tns", tns);
                }
            }

            public NodeContext context;
            private XmlNode xmlNode;

            public string InnerText { get { return xmlNode.InnerText; } }

            public Node(string filename)
            {
                context = new NodeContext(filename);
                xmlNode = context.xmlDoc.DocumentElement;
            }

            private Node(NodeContext context_, XmlNode xmlNode_)
            {
                context = context_;
                xmlNode = xmlNode_;
            }
            public Node Select(string xpath)
            {
                XmlNode xmlNodeNew = xmlNode.SelectSingleNode(xpath, context.nsMgr);
                if (xmlNodeNew == null)
                {
                    return null;
                }
                return new Node(context, xmlNodeNew);
            }
            public IEnumerable<Node> SelectNodes(string xpath)
            {
                foreach (XmlNode xmlNodeNew in xmlNode.SelectNodes("./tns:Ntry", context.nsMgr))
                {
                    yield return new Node(context, xmlNodeNew);
                }
            }

            public decimal GetAmt(out BankStatement bankStatement)
            {
                XmlNode nodeAmt = xmlNode.SelectSingleNode("./tns:Amt", context.nsMgr);
                string amt_ = nodeAmt.InnerText;
                decimal amt = decimal.Parse(amt_);
                XmlNode nodeCredit = xmlNode.SelectSingleNode("./tns:CdtDbtInd", context.nsMgr);
                string credit = nodeCredit.InnerText;
                Trace.Assert((credit == "CRDT") || (credit == "DBIT"));
                bankStatement = credit == "DBIT" ? BankStatement.Debit : BankStatement.Credit;

                return amt;
            }

            public decimal GetAmt()
            {
                XmlNode nodeAmt = xmlNode.SelectSingleNode("./tns:Amt", context.nsMgr);
                string amt_ = nodeAmt.InnerText;
                decimal amt = decimal.Parse(amt_);
                XmlNode nodeCredit = xmlNode.SelectSingleNode("./tns:CdtDbtInd", context.nsMgr);
                string credit = nodeCredit.InnerText;
                Trace.Assert((credit == "CRDT") || (credit == "DBIT"));
                if (credit == "DBIT")
                {
                    amt = -amt;
                }
                return amt;
            }
        }
        private IEnumerable<BankEntry> ReadBankEntriesPrivate()
        {
            string filename = Path.Combine(Directory, BankFactory.Filename);
            Node doc = new Node(filename);

            Node nodeStatement = doc.Select("/tns:Document/tns:BkToCstmrStmt/tns:Stmt");
            Node nodeStartingBalance = nodeStatement.Select("./tns:Bal");
            decimal amt = nodeStartingBalance.GetAmt();
            Node nodeCredit = nodeStartingBalance.Select("./tns:CdtDbtInd");
            Node nodeAmt = nodeStartingBalance.Select("./tns:Amt");
            initialBalance = amt;
            hasInitialBalance = true;

            foreach (Node nodeNtry in nodeStatement.SelectNodes("./tns:Ntry"))
            {
                decimal ntryAmt = nodeNtry.GetAmt(out BankStatement bankStatement);
                string date = nodeNtry.Select("./tns:ValDt/tns:Dt").InnerText;

                string buchungstext = "";
                string xpath = "./tns:NtryDtls/tns:TxDtls/tns:RltdPties/tns:Cdtr/tns:Nm";
                if (doc.context.xml_version2019)
                {
                    xpath = "./tns:NtryDtls/tns:TxDtls/tns:RltdPties/tns:Cdtr/tns:Pty/tns:Nm";
                }
                Node a = nodeNtry.Select(xpath);
                if (a != null)
                {
                    buchungstext = a.InnerText;
                }

                Node b = nodeNtry.Select("./tns:NtryDtls/tns:TxDtls/tns:RmtInf/tns:Ustrd");
                if (b != null)
                {
                    if (buchungstext == "")
                    {
                        buchungstext = b.InnerText;
                    }
                    else
                    {
                        buchungstext += ": " + b.InnerText;
                    }
                }

                string comment = nodeNtry.Select("./tns:AddtlNtryInf").InnerText.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");

                if (buchungstext == "")
                {
                    if (buchungstext == "")
                    {
                        buchungstext = comment;
                    }
                    else
                    {
                        buchungstext += ": " + comment;
                    }
                }

                BankEntry be = new BankEntry(BankFactory, 0, new TValuta(date), buchungstext, ntryAmt, bankStatement);
                yield return be;
            }
        }
    }
}
