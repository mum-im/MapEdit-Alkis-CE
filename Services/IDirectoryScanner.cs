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

using System;
using MapEdit.Alkis.Services.Structure;

namespace MapEdit.Alkis.Services
{
    public interface IDirectoryScanner
    {
        /// <summary>
        /// Durchsucht <paramref name="Path"/> nach NAS-Dateien (.xml/.gz).
        /// </summary>
        /// <param name="recurseSubDirectories">
        /// Bei <c>true</c> (Default) werden Unterordner rekursiv mit durchsucht.
        /// </param>
        /// <param name="requireProfilkennung">
        /// Bei <c>true</c> (Default) wird eine fehlende Profilkennung als Fehler
        /// gewertet (Multiprofilmodus) statt stillschweigend "-" anzunehmen.
        /// </param>
        /// <param name="allowGaps">
        /// Bei <c>false</c> (Default) wirft der Scan eine <see cref="PortionGapException"/>,
        /// wenn <see cref="ImportEntitySequenceValidator.IsContinuous"/> eine Portionsluecke
        /// oder ein Sequenzproblem erkennt. Bei <c>true</c> wird nur eine Warnung geloggt
        /// und trotzdem mit der vollen Dateiliste importiert (AllowGaps-Konfigurationsoption).
        /// </param>
        List<ImportEntity> Scan(string Path, bool recurseSubDirectories = true, bool requireProfilkennung = true, bool allowGaps = false);
    }
}