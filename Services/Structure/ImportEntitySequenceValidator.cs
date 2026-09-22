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
using System.Collections.Generic;
using Serilog;

namespace MapEdit.Alkis.Services.Structure
{
    /// <summary>
    /// Prueft eine sortierte Liste von ImportEntity-Objekten auf luecken- und
    /// reihenfolgelose Kontinuitaet (Datum/Portion bzw. Auftragsnummer/Portion).
    /// Operiert auf List&lt;ImportEntity&gt; als Ganzes, nicht auf einer einzelnen
    /// Instanz - deshalb als eigene Klasse statt als statische Methode auf
    /// ImportEntity selbst.
    /// </summary>
    internal static class ImportEntitySequenceValidator
    {
        static public Boolean IsWrongInputOrder(List<ImportEntity> files)
        {
            // Überprüfe, ob bereits importierte Dateien NACH neu gefundenen liegen -> Das Wäre eine Fehler
            if (1 == files.Count) return false;
            var cnt = files.Count;
            var i = 0;
            bool newFileFound = false;
            while (i < cnt)
            {
                if (!files[i].IsAlreadyProcessed)
                {
                    newFileFound = true;
                }
                else if (newFileFound)
                {
                    Log.Error("ImportEntitySequenceValidator.IsContinuous: New files were found that would have needed to be imported before already-imported files.");
                    return true;
                }
                i++;
            }
            return false;
        }

        static public Boolean IsContinuous(List<ImportEntity> files)
        {
            if (null == files || files.Count<1) return false;
            if (1 == files.Count) return true;
            var cnt = files.Count;
            if (files[0].IsBDA)
            {
                return true; // if we import BDA (though not allowed) we cannot decide wether the files are continuous
            }
            ImportEntity last;
            ImportEntity current = files[0];
            var i = 1;
            bool ret = true;
            // Überprüfe, ob bereits importierte Dateien NACH neu gefundenen liegen -> Das Wäre eine Fehler
            if (IsWrongInputOrder(files))
            {
                throw new Exception("New files were found "
                + System.Environment.NewLine +
                "that would have needed to be imported before already-imported files.");
            }
            while (i < cnt)
            {
                last = current;
                current = files[i];
                if (String.IsNullOrEmpty(last.Datum) || String.IsNullOrEmpty(current.Datum))
                {
                    Log.Error("ImportEntitySequenceValidator.IsContinuous: Predecessor: " + last.FullyQualifiedFileName);
                    Log.Error("ImportEntitySequenceValidator.IsContinuous: Current: " + current.FullyQualifiedFileName);
                    Log.Error("ImportEntitySequenceValidator.IsContinuous: A file without a date was found.");
                    ret = false;
                }
                if (last.Datum == current.Datum)
                {
                    if (last.Portion + 1 != current.Portion)
                    {
                        if (last.IsAlreadyProcessed && !current.IsAlreadyProcessed && current.Portion <= 1)
                        {
                            // die erste - kein echter Bruch, ret bleibt unangetastet (Hack: siehe ImportEntity::CompareTo)
                        }
                        else
                        {
                            Log.Error("ImportEntitySequenceValidator.IsContinuous: Predecessor: " + last.FullyQualifiedFileName);
                            Log.Error("ImportEntitySequenceValidator.IsContinuous: Current: " + current.FullyQualifiedFileName);
                            Log.Error("ImportEntitySequenceValidator.IsContinuous: " + current.Datum + "(" + current.Portion + ") follows " + last.Datum + "(" + last.Portion + ").");
                            ret = false;
                        }
                    }
                }
                else
                {
                    // kein test auf last.Protion==last.Gesamt, da auch von 0 an gezählt werden kann
                    // 0 ist die optionale Protokolldatei
                    if (last.Gesamt != current.Gesamt) // neue Folge
                    {
                        if (current.Portion > 1) // 0 und 1 zulässig
                        {
                            Log.Error("ImportEntitySequenceValidator.IsContinuous: Predecessor: " + last.FullyQualifiedFileName);
                            Log.Error("ImportEntitySequenceValidator.IsContinuous: Current: " + current.FullyQualifiedFileName);
                            Log.Error("ImportEntitySequenceValidator.IsContinuous: " + current.Datum + "(" + current.Portion + ") follows " + last.Datum + "(" + last.Portion + ").");
                            ret = false;
                        }
                    }
                    else
                    {
                        Log.Debug("ImportEntitySequenceValidator.IsContinuous: Predecessor: " + last.FullyQualifiedFileName);
                        Log.Debug("ImportEntitySequenceValidator.IsContinuous: Current: " + current.FullyQualifiedFileName);
                        Log.Debug("ImportEntitySequenceValidator.IsContinuous: " + current.Auftragsnummer + " follows " + last.Auftragsnummer + ".");
                        // müssen wir zulassen - keine weiteren infos, ret bleibt unangetastet
                    }
                }
                i++;
            }
            return ret;
        }
    }
}
