using System;
using System.Collections.Generic;
using System.IO;
using cashboxNet.MWST;

/*
https://www.estv.admin.ch/estv/de/home/mehrwertsteuer/fachinformationen/saldo--und-pauschalsteuersaetze.html

Saldo- und Pauschalsteuers�tze sind Branchens�tze, welche die Abrechnung mit der ESTV wesentlich vereinfachen, weil die Vorsteuern nicht ermittelt werden m�ssen. Die geschuldete Steuer wird bei diesen Abrechnungsmethoden durch Multiplikation des Bruttoumsatzes, d.h. des Umsatzes einschliesslich Steuer, mit dem entsprechenden von der ESTV bewilligten Saldosteuersatz beziehungsweise Pauschalsteuersatz berechnet.
*/
namespace cashboxNet
{
    public class MwstHelperVereinfacht : MwstHelperBase
    {
        /// <summary>
        /// cashbox_config_kontenplan.cs
        /// </summary>
        public MwstHelperVereinfacht(Configuration config) : base(config)
        {
        }

        /// <summary>2250</summary>
        private KontoWrapper kontoSatz1 = new KontoWrapper("KontoSatz1");
        private decimal mwstSatz1;
        public void KontoSatz1(int kontoNr, double mwst, string tag)
        {
            mwstSatz1 = new Decimal(mwst / 100.0);
            AddMwstSatz(tag, mwst, kontoNr, "");
            kontoSatz1.SetKontoNr(config, kontoNr);
        }

        /// <summary>2251</summary>
        private KontoWrapper kontoSatz2 = new KontoWrapper("KontoSatz2");
        private decimal mwstSatz2;
        public void KontoSatz2(int kontoNr, double mwst, string tag)
        {
            mwstSatz2 = new Decimal(mwst / 100.0);
            AddMwstSatz(tag, mwst, kontoNr, "");
            kontoSatz2.SetKontoNr(config, kontoNr);
        }

        /// <summary>2251</summary>
        private KontoWrapper kontoEinnahmenExport = new KontoWrapper("KontoEinnahmenExport");
        public void KontoEinnahmenExport(int kontoNr)
        {
            kontoEinnahmenExport.SetKontoNr(config, kontoNr);
        }

        /// <summary>1302</summary>
        private KontoWrapper kontoGeschuldeteMwstZahlung = new KontoWrapper("KontoSatz2");
        public void KontoGeschuldeteMwstZahlung(int kontoNr)
        {
            kontoGeschuldeteMwstZahlung.SetKontoNr(config, kontoNr);
        }

        private VorlageWrapper vorlageSatz1 = new VorlageWrapper("VorlageSatz1");
        public void VorlageSatz1(string vorlageText, string buchungsText)
        {
            vorlageSatz1.Add(config, kontoGeschuldeteMwstZahlung, kontoSatz1, vorlageText, buchungsText);
        }

        private VorlageWrapper vorlageSatz2 = new VorlageWrapper("VorlageSatz2");
        public void VorlageSatz2(string vorlageText, string buchungsText)
        {
            vorlageSatz2.Add(config, kontoGeschuldeteMwstZahlung, kontoSatz2, vorlageText, buchungsText);
        }

        private VorlageWrapper vorlageMwstZahlung = new VorlageWrapper("VorlageMwstZahlung");
        public void VorlageMwstZahlung(int kontoBankNr, string vorlageText, string buchungsText)
        {
            vorlageMwstZahlung.Add(config, kontoBankNr, kontoGeschuldeteMwstZahlung, vorlageText, buchungsText);

            Validate();
        }

        private void Validate()
        {
            kontoEinnahmenExport.ValidateIfDefined();
            vorlageSatz2.ValidateIfDefined();
            vorlageSatz1.ValidateIfDefined();
            vorlageMwstZahlung.ValidateIfDefined();
        }

        protected override AbrechnungImplementationBase AbrechnungFactory(BookkeepingBook book, AbrechnungsDatum abrechnungsDatum)
        {
            return new AbrechnungImplementation(this, book, abrechnungsDatum, vorlageMwstZahlung);
        }

        public override decimal CalculateBetragMwst(Entry entry)
        {
            // Vereinfacht
            // Betrag = 300.0
            // MWST-Satz = 2.0%
            // BetragMwst = 6.00
            // BetragOhneMwst = 294.00
            return N.Round5Rappen(entry.Betrag * (entry.MWST.Value / 100M));
        }

        protected override IEnumerable<AbrechnungsDatum> AbrechnungsDaten
        {
            get
            {
                AbrechnungsDatum.Duration semester = AbrechnungsDatum.Duration.Semester;
                yield return new AbrechnungsDatum(config, "Semester 1", semester, 6, 30);
                yield return new AbrechnungsDatum(config, "Semester 2", semester, 12, 31);
            }
        }

        private class AbrechnungImplementation : AbrechnungImplementationBase
        {
            private MwstHelperVereinfacht m;

            public AbrechnungImplementation(MwstHelperVereinfacht m_, BookkeepingBook book, AbrechnungsDatum abrechnungsDatum, VorlageWrapper vorlageMwstZahlung) : base(book, abrechnungsDatum, vorlageMwstZahlung)
            {
                m = m_;
            }

            public override void Abrechnen(string filename)
            {
                // Abrechnen
                UpdateSaldo(m.kontoSatz1);
                UpdateSaldo(m.kontoSatz2);
                UpdateSaldo(m.kontoEinnahmenExport);
                UpdateSaldo(m.kontoGeschuldeteMwstZahlung);

                // TODO: Einnahmen in diesem Zeitraum summieren
                decimal field221 = m.kontoEinnahmenExport.Saldo;
                decimal field320 = N.Round10Rappen(m.kontoSatz1.Saldo);
                decimal field321 = N.Round1Franken(field320 / m.mwstSatz1);
                decimal field330 = N.Round10Rappen(m.kontoSatz2.Saldo);
                decimal field331 = N.Round1Franken(field330 / m.mwstSatz2);
                decimal field380 = field320 + field330;
                decimal field381 = field321 + field331;
                decimal field299 = field381;
                decimal field200 = field299 + field221;
                using (StreamWriter sw = new StreamWriter(filename))
                {
                    sw.WriteLine($@"{abrechnungsDatum.Date}: MWST Abrechnung {abrechnungsDatum.YearQuartalSemester}

Um die Abrechnung neu zu berechnen muss dieses File gel�scht werden!

A) MWST-Formular (Papier) ausf�llen und abschicken
200: {field200:N0}
221: {field221:N0}  // Achtung: Dieser Wert enth�lt den Betrag �ber das ganze Jahr, ist also bei im zweiten Semester zu gross. Was aber auf die MWST-Zahlung keinen Einfluss hat...
289: {field221:N0}
299: {field381:N0}
321: {field321:N0},  320: {N.F(field320)}
331: {field331:N0},  330: {N.F(field330)}
381: {field381:N0},  380: {N.F(field380)}
399: {N.F(field380)}
500: {N.F(field380)}

B) Die MWST-Abrechnung mit dem gleichen Filenamen wie dieses File ablegen.

C) Diese Buchungen ins Muh2-File kopieren
2017-06-30r b {N.Muh2(field320)} {m.vorlageSatz1.Vorlage.VorlageText} Abrechnung {abrechnungsDatum.YearQuartalSemester} Satz 1
2017-06-30s b {N.Muh2(field330)} {m.vorlageSatz2.Vorlage.VorlageText} Abrechnung {abrechnungsDatum.YearQuartalSemester} Satz 2

D) MWST-Rechnung bezahlen und diese Buchung ins Muh2-File kopieren
20xx-xx-xxa b {N.F(field380)} {m.vorlageMwstZahlung.Vorlage.VorlageText} Zahlung {abrechnungsDatum.YearQuartalSemester}

");
                    sw.WriteLine("");
                    sw.WriteLine("Debug");
                    sw.WriteLine(m.kontoSatz1.Text);
                    sw.WriteLine(m.kontoSatz2.Text);
                    sw.WriteLine(m.kontoGeschuldeteMwstZahlung.Text);
                    sw.WriteLine(m.kontoEinnahmenExport.Text);
                }
            }

        }
    }

}
