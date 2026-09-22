# MapEdit Alkis – Community Edition

| | | |
|---|-|---|
| **Version:**|&nbsp; &nbsp; &nbsp; | 1.0.7 |
| **Stand:** | | 22.09.2026 |
| **Author:** | | Mensch und Maschine Infrastruktur GmbH |
| **Kontakt:** | | MapEdit@MuM.de |

## Was ist das?

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

## Voraussetzungen

- **.NET 8 Runtime** (bzw. der self-contained Single-File-Build, der keine
  separate Runtime-Installation braucht).
- **PostgreSQL mit PostGIS-Erweiterung.**
- **GDAL/`ogr2ogr`** – Pfad zur `ogr2ogr.exe`/`ogr2ogr` wird in der Konfiguration
  angegeben.
- **Eine bereits bestehende "MapEdit"-Datenbank**. Diese Grundstruktur wird von 
  MapEdit Alkis **nicht** neu angelegt, sondern muss vorher vorhanden sein (z. B. durch
  den Import einer der beiden DB-Templates). MapEdit Alkis erweitert diese Datenbank um das
  ALKIS-Schema und die Präsentationstabellen.
- Die im Ordner `Template/` mitgelieferten Dateien `EPSG25832.dump` und `EPSG25833.dump`
  enthalten die zugehörigen Koordinatensystem-/Projektionsdaten und können bei
  Bedarf per `pg_restore` in die Zieldatenbank eingespielt werden, falls dort
  noch nicht vorhanden.

## Aufruf

```
MapEdit Alkis.exe --folder <Konfigurationsordner> --configuration <Name> [--stdout-log <Datei>]
```

| Parameter | Kurzform | Bedeutung |
|---|---|---|
| `--folder` | `-f` | Verzeichnis, in dem die `appsettings.json` liegt (**nicht** der Ordner mit den NAS-Daten – der steht separat je Konfiguration in `appsettings.json` unter `Folder`, siehe unten). |
| `--configuration` | `-c` | Name des zu verwendenden Eintrags aus `Configurations` in `appsettings.json`. |
| `--stdout-log` | – | Optional: Datei, in die zusätzlich alle Konsolenausgaben (u. a. der `ogr2ogr`-Fortschritt) gespiegelt werden. |

Beide Pflichtparameter (`--folder`, `--configuration`) müssen angegeben werden,
sonst bricht das Programm mit einer Fehlermeldung auf der Konsole ab.

## Konfiguration (`appsettings.json`)

Beispiel (Werte sind Platzhalter):

```json
{
  "Serilog": {
    "MinimumLevel": "Information"
  },
  "Configurations": [
    {
      "Name": "meinprojekt",
      "Driver": "PG",
      "Host": "localhost",
      "Port": "5432",
      "Username": "postgres",
      "Password": "geheim",
      "Database": "alkis_meinprojekt",
      "Ogr2Ogr": "C:\\Pfad\\zu\\ogr2ogr.exe",
      "Folder": "C:\\Pfad\\zu\\den\\NAS-Daten",
      "GDAL_DRIVER_PATH": "C:\\Pfad\\zu\\gdalplugins",
      "PROJ_LIB": "C:\\Pfad\\zu\\proj",
      "ForceOverwrite": "No",
      "OnlyPostRun": "No",
      "NoPostRun": "No",
      "AllowGaps": "No",
      "WarnForReplaceWithSameBeginnt": "No",
      "KeepHistory": "No",
      "IncludeErrorDetail": "No",
      "DefaultCommandTimeout": 300
    }
  ]
}
```

Ein Eintrag kann per `Extends` von einem anderen erben (gemeinsame Werte wie
Zugangsdaten oder Pfade müssen dann nicht wiederholt werden); nicht gesetzte
Felder werden vom übergeordneten Eintrag übernommen.

### Felder

| Feld | Bedeutung | Default |
|---|---|---|
| `Name` | Eindeutiger Bezeichner, mit `--configuration` auszuwählen. | – |
| `Extends` | Name eines anderen Eintrags, von dem nicht gesetzte Werte geerbt werden. | leer |
| `Host`, `Port`, `Username`, `Password`, `Database` | PostgreSQL-Verbindungsdaten. | Port: `5432` |
| `Ogr2Ogr` | Vollständiger Pfad zur `ogr2ogr`-Programmdatei. | – |
| `Folder` | Verzeichnis mit den ALKIS-NAS-Dateien (`.xml`/`.xml.gz`), rekursiv durchsucht. | – |
| `GDAL_DRIVER_PATH`, `GDAL_LIBRARY_PATH`, `GDAL_DATA`, `PROJ_LIB` | GDAL-Umgebungsvariablen, werden dem `ogr2ogr`-Prozess mitgegeben. | leer |
| `ForceOverwrite` | `Yes`: vorhandene ALKIS-Struktur wird immer neu angelegt/überschrieben, auch wenn sie schon existiert. | `No` |
| `OnlyPostRun` | `Yes`: überspringt den NAS-Import, führt nur die Postprocessing-Kaskade erneut aus. | `No` |
| `NoPostRun` | `Yes`: überspringt die Postprocessing-Kaskade nach dem Import. | `No` |
| `AllowGaps` | `No` (Default): eine erkannte Portionslücke/Sequenzstörung bricht den Lauf ab (Exit-Code 3). `Yes`: es wird nur geloggt, der Import läuft mit der vollen Dateiliste weiter. | `No` |
| `WarnForReplaceWithSameBeginnt` | Steuert eine zusätzliche Warnung bei bestimmten Replace-Datensätzen mit gleichem `beginnt`-Zeitstempel. | `No` |
| `KeepHistory` | `Yes`: historisierte (fortgeführte) Datensätze werden nicht gelöscht. | `No` |
| `IncludeErrorDetail` | `Yes`: PostgreSQL-Fehlerdetails (`DETAIL`-Feld, ggf. sensible Daten) werden in Exceptions/Logs mit ausgegeben – nur für Diagnosezwecke. | `No` |
| `PG_USE_COPY`, `OGR_PG_RETRIEVE_FID`, `OGR_PG_SKIP_CONFLICTS` | Fortgeschrittene `ogr2ogr`-PostgreSQL-Treiberoptionen (siehe [GDAL PG-Treiber-Doku](https://gdal.org/drivers/vector/pg.html)) – nur ändern, wenn die Auswirkungen bekannt sind. | `No`/`No`/`Yes` |
| `DefaultCommandTimeout` | SQL-Befehlstimeout in Sekunden. | `300` |

## Ablauf im Detail

1. Vor dem Import prüft das Programm, ob `Ogr2Ogr` und `Folder` (aus der
   gewählten Konfiguration) existieren – andernfalls Abbruch.
2. `StructureExists` verbindet sich mit der Datenbank und prüft, ob bereits
   eine passende ALKIS-Struktur vorhanden ist. Ist das nicht der Fall (oder ist
   `ForceOverwrite=Yes` gesetzt), wird sie über die in `Version101/` enthaltenen
   SQL-Skripte neu angelegt. Ist die Zieldatenbank keine "MapEdit"-Datenbank
   (Tabelle `me_parameter` fehlt), bricht das Programm mit einer Fehlermeldung
   ab.
3. Der `DirectoryScanner` sammelt alle NAS-Dateien im konfigurierten `Folder`
   und prüft die Portionsfolge (siehe `AllowGaps`).
4. Sofern `OnlyPostRun` nicht gesetzt ist, wird jede Datei per `ogr2ogr` in die
   Datenbank importiert.
5. Sofern `NoPostRun` nicht gesetzt ist, läuft anschließend die
   Postprocessing-Kaskade aus `Version101/postprocessing.d/`, die die
   Präsentationsobjekte ableitet.

## Exit-Codes

| Code | Bedeutung |
|---|---|
| `0` | Erfolgreich. |
| `3` | Portionslücke/Sequenzproblem erkannt (`AllowGaps=No`). |
| `4` | `appsettings.json` konnte nicht gelesen/geparst werden. |
| `5` | Konfiguration (`--configuration`) oder deren `Folder` nicht gefunden. |
| `6` | Log-Unterordner im Datenordner konnte nicht angelegt werden. |
| `7` | Weder Windows noch Linux erkannt. |
| `8` | `ogr2ogr` unter dem konfigurierten Pfad nicht gefunden. |
| `9` | Datenordner (`--folder`) existiert nicht. |
| `10` | Zieldatenbank ist keine MapEdit-Datenbank. |

## Build

```
dotnet build "MapEdit Alkis.csproj" -c Release
```

Für ein eigenständiges, self-contained Single-File-Executable:

```
dotnet publish "MapEdit Alkis.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Lizenz

MapEdit Alkis Community Edition steht unter der **GNU General Public License,
Version 2 (oder nach eigener Wahl einer späteren Version)** – siehe
[`LICENSE`](LICENSE). Das ALKIS-Datenmodell und die Ableitungsregeln in `Version101/` 
basieren auf dem Projekt norGIS ALKIS Import
von Jürgen E. Fischer (NorBIT) und wurden für MapEdit Alkis angepasst; die
jeweiligen Copyright-Hinweise sind in den betroffenen Dateien erhalten
geblieben.
