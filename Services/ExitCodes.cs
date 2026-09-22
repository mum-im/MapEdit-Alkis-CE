/***************************************************************************
 *                                                                         *
 * Project:  MapEdit Alkis Community Edition                               *
 * Author:   Mensch und Maschine Infrastruktur GmbH                        *
 *                                                                         *
 ***************************************************************************
 * Copyright (c) 2026, Mensch und Maschine Infrastruktur GmbH              *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 ***************************************************************************/

namespace MapEdit.Alkis.Services
{
    /// <summary>
    /// Zentrale Exit-Code-Konstanten fuer MapEdit.Alkis. Jeder Code steht fuer einen
    /// bekannten, unterscheidbaren Fehlerfall, der frueher (in Startup.ConfigureServices
    /// bzw. EntryPoint.Run) still mit Exit-Code 0 durchgelaufen ist, obwohl nichts
    /// ausgefuehrt wurde. Siehe Source/Documentation/GAP_Exception.md Abschnitt 5/9.
    /// </summary>
    public static class ExitCodes
    {
        public const int Success = 0;

        /// <summary>Portionsluecke erkannt und AllowGaps=No (Default).</summary>
        public const int PortionGap = 3;

        /// <summary>appsettings.json konnte nicht gelesen/geparst werden.</summary>
        public const int ConfigFileError = 4;

        /// <summary>Konfiguration (--configuration) oder deren Folder nicht gefunden.</summary>
        public const int ConfigOrFolderNotFound = 5;

        /// <summary>Log-Unterordner im Datenordner konnte nicht angelegt werden.</summary>
        public const int LogDirectoryUnavailable = 6;

        /// <summary>Weder Windows noch Linux erkannt.</summary>
        public const int UnsupportedOperatingSystem = 7;

        /// <summary>ogr2ogr.exe unter dem konfigurierten Pfad nicht gefunden.</summary>
        public const int Ogr2OgrNotFound = 8;

        /// <summary>Datenordner (--folder) existiert nicht.</summary>
        public const int DataFolderNotFound = 9;

        /// <summary>Zieldatenbank ist keine MapEdit-Datenbank.</summary>
        public const int NotAMapEditDatabase = 10;
    }
}
