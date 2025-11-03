using System;
using System.Collections.Generic;
using System.IO;
using cashboxNet.MWST;

namespace cashboxNet
{
    public class MwstHelperEffektiv : MwstHelperBase
    {
        /// <summary>
        /// cashbox_config_kontenplan.cs
        /// </summary>
        public MwstHelperEffektiv(Configuration config) : base(config)
        {
            Steuersatz = -1.0;
        }

        public double Steuersatz { get; set; }

        /// <summary>1170</summary>
        private KontoWrapper kontoVorsteuerMaterial = new KontoWrapper("KontoVorsteuerMaterial");
        public void KontoVorsteuerMaterial(int kontoNr)
        {
            kontoVorsteuerMaterial.SetKontoNr(config, kontoNr);
        }

        /// <summary>1171</summary>
        private KontoWrapper kontoVorsteuerBetrieb = new KontoWrapper("KontoVorsteuerBetrieb");
        public void KontoVorsteuerBetrieb(int kontoNr)
        {
            kontoVorsteuerBetrieb.SetKontoNr(config, kontoNr);
        }

        /// <summary>2200</summary>
        private KontoWrapper kontoGeschuldeteMwst = new KontoWrapper("KontoGeschuldeteMwst");
        public void KontoGeschuldeteMwst(int kontoNr)
        {
            kontoGeschuldeteMwst.SetKontoNr(config, kontoNr);
        }

        /// <summary>1302</summary>
        KontoWrapper kontoGeschuldeteMwstZahlung = new KontoWrapper("KontoGeschuldeteMwstZahlung");
        public void KontoGeschuldeteMwstZahlung(int kontoNr)
        {
            kontoGeschuldeteMwstZahlung.SetKontoNr(config, kontoNr);
        }

        /// <summary>3402</summary>
        KontoWrapper kontoLeisungenAusland = new KontoWrapper("KontoLeisungenAusland");
        public void KontoLeisungenAusland(int kontoNr)
        {
            kontoLeisungenAusland.SetKontoNr(config, kontoNr);
        }

        private VorlageWrapper vorlageAbrechnungMaterial = new VorlageWrapper("VorlageAbrechnungMaterial");
        public void VorlageAbrechnungMaterial(string vorlageText, string buchungsText)
        {
            vorlageAbrechnungMaterial.Add(config, kontoVorsteuerMaterial, kontoGeschuldeteMwstZahlung, vorlageText, buchungsText);
        }

        private VorlageWrapper vorlageAbrechnungBetrieb = new VorlageWrapper("VorlageAbrechnungBetrieb");
        public void VorlageAbrechnungBetrieb(string vorlageText, string buchungsText)
        {
            vorlageAbrechnungBetrieb.Add(config, kontoVorsteuerBetrieb, kontoGeschuldeteMwstZahlung, vorlageText, buchungsText);
        }

        private VorlageWrapper vorlageAbrechnung = new VorlageWrapper("VorlageAbrechnung");
        public void VorlageAbrechnung(string vorlageText, string buchungsText)
        {
            vorlageAbrechnung.Add(config, kontoGeschuldeteMwstZahlung, kontoGeschuldeteMwst, vorlageText, buchungsText);
        }

        private VorlageWrapper vorlageMwstZahlung = new VorlageWrapper("VorlageMwstZahlung");
        public void VorlageMwstZahlung(int kontoBankNr, string vorlageText, string buchungsText)
        {
            vorlageMwstZahlung.Add(config, kontoBankNr, kontoGeschuldeteMwstZahlung, vorlageText, buchungsText);

            Validate();
        }

        private void Validate()
        {
            vorlageAbrechnungMaterial.ValidateIfDefined();
            vorlageAbrechnungBetrieb.ValidateIfDefined();
            vorlageAbrechnung.ValidateIfDefined();
            vorlageMwstZahlung.ValidateIfDefined();
            kontoLeisungenAusland.ValidateIfDefined();
            if (Steuersatz < 0.0)
            {
                throw new CashboxException($"MWST-Konfiguration nicht vollst�ndig: 'm.Steuersatz = 8.0;' fehlt.");
            }
        }

        protected override AbrechnungImplementationBase AbrechnungFactory(BookkeepingBook book, AbrechnungsDatum abrechnungsDatum)
        {
            return new AbrechnungImplementation(this, book, abrechnungsDatum, vorlageMwstZahlung);
        }

        public override decimal CalculateBetragMwst(Entry entry)
        {
            // Effektiv
            // Betrag = 216.0
            // MWST-Satz = 8%
            // BetragMwst = 16.00
            // BetragOhneMwst = 200.00
            return entry.Betrag * entry.MWST.Value / (100M + entry.MWST.Value);
        }

        protected override IEnumerable<AbrechnungsDatum> AbrechnungsDaten
        {
            get
            {
                AbrechnungsDatum.Duration duration = AbrechnungsDatum.Duration.Quartal;
                yield return new AbrechnungsDatum(config, "Quartal 1", duration, 3, 31);
                yield return new AbrechnungsDatum(config, "Quartal 2", duration, 6, 30);
                yield return new AbrechnungsDatum(config, "Quartal 3", duration, 9, 30);
                yield return new AbrechnungsDatum(config, "Quartal 4", duration, 12, 31);
            }
        }

        private class AbrechnungImplementation : AbrechnungImplementationBase
        {
            private MwstHelperEffektiv m;

            public AbrechnungImplementation(MwstHelperEffektiv m_, BookkeepingBook book, AbrechnungsDatum abrechnungsDatum, VorlageWrapper vorlageMwstZahlung) : base(book, abrechnungsDatum, vorlageMwstZahlung)
            {
                m = m_;
            }

            private string BuchungenLoeschen()
            {
                StringWriter sw = new StringWriter();
                BuchungLoeschen(sw, m.vorlageAbrechnungMaterial.Vorlage);
                BuchungLoeschen(sw, m.vorlageAbrechnungBetrieb.Vorlage);
                BuchungLoeschen(sw, m.vorlageAbrechnung.Vorlage);
                return sw.ToString();
            }
            private void BuchungLoeschen(StringWriter sw, Buchungsvorlage vorlage)
            {
                KontoDay kontoDay = vorlage.KontoHaben.KontoDays[abrechnungsDatum.Date];
                if (kontoDay == null)
                {
                    return;
                }
                foreach (Entry entry in kontoDay.JournalDay.EntriesOrdered)
                {
                    if (entry.Buchungsvorlage.VorlageText == vorlage.VorlageText)
                    {
                        sw.WriteLine(entry.Line);
                        return;
                    }
                }
            }

            public override void Abrechnen(string filename)
            {
                // Eventuell bereits eingetragene MWST-Abrechnungsbuchungen l�schen
                /*
                abrechnungsDatum.Date
                  m.vorlageAbrechnungMaterial.Vorlage.VorlageText
                  m.vorlageAbrechnungBetrieb.Vorlage.VorlageText
                  m.vorlageAbrechnung.Vorlage.VorlageText
                  */
                string buchungenZuLoeschen = BuchungenLoeschen();
                if (buchungenZuLoeschen != "")
                {
                    using (StreamWriter sw = new StreamWriter(filename))
                    {
                        sw.WriteLine("Um die Abrechnung durchzuf�hren bitte dieses File UND nachfolgende Buchungen l�schen:");
                        sw.Write(buchungenZuLoeschen);
                        return;
                    }
                }

                // Abrechnen
                UpdateSaldo(m.kontoVorsteuerMaterial);
                UpdateSaldo(m.kontoVorsteuerBetrieb);
                UpdateSaldo(m.kontoGeschuldeteMwst);
                UpdateSaldoEndeMinusAnfang(m.kontoLeisungenAusland);

                // Total Umsatz
                decimal field399rechts = N.Round10Rappen(m.kontoGeschuldeteMwst.Saldo);
                // Abz�ge
                decimal field400 = N.Round10Rappen(m.kontoVorsteuerMaterial.Saldo);
                decimal field405 = N.Round10Rappen(m.kontoVorsteuerBetrieb.Saldo);
                decimal field479 = field400 + field405;
                // zu bezahlender Betrag
                decimal field500 = field399rechts - field479;
                decimal mwst_zahlung = field500;
                // guthaben
                decimal field510 = 0;
                if (field500 < 0)
                {
                    // R�ckverg�tung von MWST
                    field510 = -field500;
                    field500 = 0;
                }
                double field299_ = ((double)field399rechts) / (m.Steuersatz / 100.0);
                decimal field299 = N.Round5Rappen(new Decimal(field299_));

                // Umsatz Ausland (Abzug)
                decimal field221 = N.Round10Rappen(m.kontoLeisungenAusland.Saldo);

                decimal field200 = field299 + field221;
                const string Kontrolle = "(Kontrolle)";

                using (StreamWriter sw = new StreamWriter(filename))
                {
                    sw.WriteLine($@"{abrechnungsDatum.Date}: MWST Abrechnung {abrechnungsDatum.YearQuartalSemester}

Um die Abrechnung neu zu berechnen muss dieses File gel�scht werden!

A) MWST-Formular (Online) ausf�llen und abschicken
  Beachte: Alle nachfolgend nicht aufgef�hrten Felder werden nicht berechnet: Bitte 0 einsetzen!

  Alle Umsatzangaben sind: NETTO
  Umsatz - Entgelte
    200: {field200:N0}
  Umsatz - Abz�ge
    221: {field221:N0}
  Umsatz - Kontrolle
    299: {field299:N0} {Kontrolle}
  Steuerberechnung - Satz
    303: {field299:N0}
    299: {field299:N0} {Kontrolle}
  Steuerberechnung - Total geschuldet
    399: {N.F(field399rechts)} {Kontrolle}
  Steuerberechnung - Vorsteuer
    400: {N.F(field400)}
    405: {N.F(field405)}
  Kontrolle:
    479: {N.F(field479)} {Kontrolle}
    500: {N.F(field500)} {Kontrolle}
    510: {N.F(field510)} {Kontrolle}


B) Die MWST-Abrechnung mit dem gleichen Filenamen wie dieses File ablegen.

C) Diese Buchungen ins Muh2-File kopieren
{abrechnungsDatum.Date}r b {N.Muh2(field400)} {m.vorlageAbrechnungMaterial.Vorlage.VorlageText} Abrechnung {abrechnungsDatum.YearQuartalSemester}
{abrechnungsDatum.Date}s b {N.Muh2(field405)} {m.vorlageAbrechnungBetrieb.Vorlage.VorlageText} Abrechnung {abrechnungsDatum.YearQuartalSemester}
{abrechnungsDatum.Date}t b {N.Muh2(field399rechts)} {m.vorlageAbrechnung.Vorlage.VorlageText} Abrechnung {abrechnungsDatum.YearQuartalSemester}
20xx-xx-xxa b {N.Muh2(mwst_zahlung)} {m.vorlageMwstZahlung.Vorlage.VorlageText} Zahlung {abrechnungsDatum.YearQuartalSemester}
");
                }
            }
        }
    }
}
