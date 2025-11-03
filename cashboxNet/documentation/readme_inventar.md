# Inventar

## Problemstellung

Peter möchte sein Fahrrad ins Inventar aufnehmen und jährlich abschreiben.
Diese Seite beschreibt einerseits einerseits die nötigen Einträge im Kontenplan und Vorlagen, andererseits die nötigen Buchungen.

## Kontenplan

* Wir benötigen ein Aktivkonto: `1530 Fahrzeuge`.
* Wir benötigen ein Abschreibungskonto: `6900 Abschreibungen`.

Beispiel Maerki Informatik

```cs
maerki_informatik\2020\cashbox_config_kontenplan.cs

h.GroupBilanzAktiven();
...
h.GroupBilanz_("Anlagevermögen");
...
h.Add(1530, "Fahrzeuge");   // <---------
...
h.GroupErfolgsrechnungAusgaben();
...
h.GroupErfolgsrechnung_("Sonstiger Betriebsaufwand");
...
h.Add(6900, "Abschreibungen");   // <---------
```

## Vorlagebuchungen

Wir benötigen Vorlagebuchungen
* zum Kaufen des Fahrzeugs.
* für die Abschreibung Ende Jahr oder beim Verkauf.

```cs
maerki_informatik\2020\cashbox_config_vorlagebuchungen.cs

h.Add(1020, 1530, "invfahrzeug", "VSB77", "Fahrzeuge");   // <---------
...
h.Add(1530, 6900, "abschlussAbschreibungFahrzeuge", "", "Abschreibung Fahrzeuge");   // <---------
```

## Buchungen

### Fahrzeug kaufen

```
2020-08-28a b 5000.00 invfahrzeug Fahrrad
```

Bei dieser Buchung wird
* 5000.00 der Bank belastet
* 357.47 der MWST belastet
* 4642.53 erscheint im Inventarkonto 1530

### Abschreibung am Ende des Jahres

```
2020-12-31a b 1650.00 abschlussAbschreibungFahrzeuge Fahrrad 30%
```

Bei dieser Buchung wird
* Der Wert im Inventarkonto 1530 um 1650.00 reduziert.
* Der Gewinn um 1650.00 reduziert.

# Kommentare Hans - zum Löschen

## Letzte Inventarbuchung Märki Informatik

2013 belege 26
  Fahrzeuge Suzuki Wagon R. VSB80 1171 2500.00->185.19
  buchung 26 2013-01-01 2500.00 invFahrzeuge 'Suzuki Wagon R.' <bank_zkb_2013-01-01_0a>
  Soll 1530, Gegenkonto 1021   CHF2'314.81
2013 beleg 2005
  Abschreibung Fahrzeuge
  buchung 2005 2013-12-31 1200.00 abschlussAbschreibungFahrzeuge
  Solll 1530, Gegenkonto 6900  CHF 1200.00


### Akutelle Zustand Märki Informatik

abschlussAbschreibungFahrzeuge	6900	1530	Abschreibung Fahrzeuge

file:///C:/Projekte/hans/cashbox_git/maerki_informatik/2013/cashbox_kontenblaetter.html
1530	Fahrzeuge	1'737.04

h.Add(1020, 1530, "invfahrzeug", "VSB77", "Fahrzeuge");

2020-08-28b b 5000.00 invfahrzeug-bar Fahrrad


### Aktueller Zustand Positron
2020-08-28b b 5000.00 invfahrzeug Fahrrad

