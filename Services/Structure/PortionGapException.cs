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

using MapEdit.Alkis.Services;

namespace MapEdit.Alkis.Services.Structure
{
    /// <summary>
    /// Wird geworfen, wenn ImportEntitySequenceValidator.IsContinuous eine
    /// Portionsluecke oder ein Sequenzproblem erkennt und die Konfigurationsoption
    /// AllowGaps=No (Default) gesetzt ist. Wird in Program.Main zentral (als
    /// ExitCodeException) abgefangen und setzt den Prozess-Exit-Code auf
    /// ExitCodes.PortionGap, damit der Prozess sichtbar (Exit-Code != 0) statt still
    /// (Exit-Code 0, 0 importierte Dateien) fehlschlaegt.
    /// </summary>
    public class PortionGapException : ExitCodeException
    {
        public PortionGapException(string message) : base(ExitCodes.PortionGap, message) { }
    }
}
