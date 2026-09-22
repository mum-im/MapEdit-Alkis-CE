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
using static MapEdit.Alkis.Services.PostNasExecuterWindows.InsertAndReplaceErrorCollector;

namespace MapEdit.Alkis.Services
{
    public enum DBEngine { Disposed=1, PG=2 };
    public class AlkisNumbers
    {
        public int Read { get; set; } = 0;
        public int Written { get; set; } = 0;
        public int Ignored { get; set; } = 0;
    }
    public class ScanResult // own scan results
    {
        // not yet scanned
        public Int32 Inserts { get; set; } = 0;
        public Int32 Replaces { get; set; } = 0;
        public Int32 Deletes { get; set; } = 0;
        public Int32 Updates { get; set; } = 0; // not yet scanned

    }
    public class InsertFailure
    {
        public string Table { get; set; } = string.Empty;
        public string Gml_ID { get; set; } = string.Empty;
        public string Beginnt { get; set; } = string.Empty;
        public bool ReplaceIgnored { get; set; } = false;
        public bool IncompleteMatch { get; set; } = false; // no match in beginnt
    }
    public class InsertFailureList
    {
        public List<InsertFailure> Failures { get; set; } = new();
        public void MarkIgnored(string typename, string gml_id, string beginnt )
        {
            foreach (var f in Failures)
            {
                if (f.Table.Equals(typename, StringComparison.OrdinalIgnoreCase)
                    && f.Gml_ID.Equals(gml_id, StringComparison.OrdinalIgnoreCase))
                {
                    if (f.Beginnt.Equals(beginnt, StringComparison.OrdinalIgnoreCase))
                    {
                        f.ReplaceIgnored = true;
                    }
                    else
                    {
                        f.IncompleteMatch = true;
                    }
                }
            }
        }

    }
    public class NumbersAndInsertFailures
    {
        public Dictionary<string, AlkisNumbers> Numbers { get; set; } = new();
        public InsertFailureList InsertFailures { get; set; } = new();
    }   
    public interface IDatabaseManager
    {
       DBEngine Engine();
       bool StructureExists(string ForceOverwrite);
       int DatabaseSRID();
       void ProcessFiles(List<ImportEntity> files, Func<string, string, NumbersAndInsertFailures>  ogr2ogrRun);
       void PreRun(); // Not used yet
       void PostRun();
    }
}