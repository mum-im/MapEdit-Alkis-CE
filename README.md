# MapEdit Alkis – Community Edition

MapEdit Alkis Community Edition ist ein kommandozeilenbasiertes Werkzeug, das amtliche
ALKIS-NAS-Daten (Normbasierte Austauschschnittstelle, `.xml`/`.xml.gz`) in eine
PostgreSQL/PostGIS-Datenbank importiert. Es baut dabei auf Import- und
Ableitungslogik auf, die ursprünglich aus dem Open-Source-Projekt
[norBIT ALKIS Import](https://github.com/norBIT/alkisimport) (Jürgen E. Fischer, NorBIT)
stammt und für MapEdit angepasst wurde – siehe `Version101/` und `LICENSE`
(GPLv2) für Details zur Herkunft.

Der Import besteht aus drei Schritten, die das Programm nacheinander ausführt:

1. **Einlesen** des Datenordners: alle `.xml`- und `.xml.gz`-Dateien werden als
   NAS-Portionen erkannt, auf Vollständigkeit/Reihenfolge geprüft und
   chronologisch sortiert (`Services/DirectoryScanner.cs`).
2. **Import** jeder Datei per `ogr2ogr` (GDAL) in das ALKIS-Datenmodell der
   Zieldatenbank.
3. **Postprocessing**: eine Kaskade von SQL-Ableitungsregeln
   (`Version101/postprocessing.d/`) erzeugt daraus die Präsentationsobjekte
   (Punkte, Linien, Flächen, Beschriftungen), die z. B. für die Kartendarstellung
   benötigt werden.

Voraussetzungen, CLI-Aufruf, vollständige Konfigurationsreferenz, Ablauf im
Detail, Exit-Codes, Build-Befehle und Lizenzhinweise: siehe
[`Beschreibung.md`](Beschreibung.md).
