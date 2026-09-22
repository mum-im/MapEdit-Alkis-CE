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

namespace MapEdit.Alkis.Services
{
    /// <summary>
    /// Basisklasse fuer alle bekannten Start-/Konfigurationsfehler, die MapEdit.Alkis mit
    /// einem spezifischen, von 0 verschiedenen Exit-Code beenden sollen — statt wie bisher
    /// still mit Exit-Code 0 durchzulaufen, obwohl nichts ausgefuehrt wurde. Wird zentral in
    /// Program.Main abgefangen: Message auf stderr, ExitCode als Prozess-Exit-Code.
    /// </summary>
    public class ExitCodeException : Exception
    {
        public int ExitCode { get; }

        public ExitCodeException(int exitCode, string message) : base(message)
        {
            ExitCode = exitCode;
        }
    }
}
