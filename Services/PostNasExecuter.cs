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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using static MapEdit.Alkis.Services.PostNasExecuterWindows.InsertAndReplaceErrorCollector;

namespace MapEdit.Alkis.Services
{
    public class PostNasExecuter : IPostNasExecuter
    {
        public class InsertAndReplaceErrorCollector
        {
            public List<InsertFailure> ErrorItems { get; private set; } = new List<InsertFailure>();
            /* catch errors sequence from ogr2ogr 
             * ERROR 1: FEHLER:  doppelter Schl├╝sselwert verletzt Unique-Constraint ┬╗ax_sonstigervermessungspunkt_gml┬½
             * DETAIL:  Schl├╝ssel ┬╗(gml_id, beginnt)=(DEBYvAAAAAB0opJ0, 2024-08-12T12:01:59Z)┬½ existiert bereits.
             * ERROR 1: INSERT command for new feature failed.
             * FEHLER:  doppelter Schl├╝sselwert verletzt Unique-Constraint ┬╗ax_sonstigervermessungspunkt_gml┬½
             * DETAIL:  Schl├╝ssel ┬╗(gml_id, beginnt)=(DEBYvAAAAAB0opJ0, 2024-08-12T12:01:59Z)┬½ existiert bereits.
             * Command: INSERT INTO "ax_sonstigervermessungspunkt" ("gml_id", "beginnt", "advstandardmodell", "punktkennung", "vermarkung_marke") VALUES ('DEBYvAAAAAB0opJ0', '2024-08-12T12:01:59Z', ARRAY['DLKM'], '30191922-1828', 1400)
             * GDALVectorTranslate: Unable to write feature 1 into layer ax_sonstigervermessungspunkt.
             */
            private readonly Action<string> writeOut;
            private string str1 = string.Empty;
            private string str2 = string.Empty;
            private string str3 = string.Empty;
            private string str4 = string.Empty;
            private string str5 = string.Empty;
            private string str6 = string.Empty;
            private int state = 0;
            private bool active = false;
            public InsertAndReplaceErrorCollector(Action<string> writeOut, bool active)
            {
                this.writeOut = writeOut;
                this.active = active;
            }
            private void LogToDB()
            {
                //string input2 = "DETAIL:  Schl├╝ssel ┬╗(gml_id, beginnt)=(DEBYvAAAAAB0opJ0, 2024-08-12T12:01:59Z)┬½ existiert bereits.";
                var regex2 = new Regex(@"\(gml_id, beginnt\)=\(([^,]+), ([^\)]+)\)");
                string input2 = str2;
                // ---
                //string input6 = "Command: INSERT INTO \"ax_sonstigervermessungspunkt\" (\"gml_id\", \"beginnt\", \"advstandardmodell\", \"punktkennung\", \"vermarkung_marke\") VALUES ('DEBYvAAAAAB0opJ0', '2024-08-12T12:01:59Z', ARRAY['DLKM'], '30191922-1828', 1400)";
                string input6 = str6;
                var regex6 = new Regex(@"INSERT INTO ""([^""]+)""");

                string gml_id = string.Empty;
                string beginnt = string.Empty;
                string tableName = string.Empty;

                var match2 = regex2.Match(input2);
                if (match2.Success)
                {
                    gml_id = match2.Groups[1].Value;
                    beginnt = match2.Groups[2].Value;

                    var match6 = regex6.Match(input6);
                    if (match6.Success)
                    {
                        tableName = match6.Groups[1].Value;
                        ErrorItems.Add(new InsertFailure { Table = tableName, Gml_ID = gml_id, Beginnt = beginnt });
                    }
                }
            }
            public void Reset()
            {
                str1 = string.Empty;
                str2 = string.Empty;
                str3 = string.Empty;
                str4 = string.Empty;
                str5 = string.Empty;
                str6 = string.Empty;
                state = 0;
            }
            public void WriteOutAndReset()
            {
                if (state > 0)
                {
                    if (!string.IsNullOrEmpty(str1))
                    {
                        writeOut(str1);
                        if (!string.IsNullOrEmpty(str2))
                        {
                            writeOut(str2);
                            if (!string.IsNullOrEmpty(str3))
                            {
                                writeOut(str3);
                                if (!string.IsNullOrEmpty(str4))
                                {
                                    writeOut(str4);
                                    if (!string.IsNullOrEmpty(str5))
                                    {
                                        writeOut(str5);
                                        if (!string.IsNullOrEmpty(str6))
                                        {
                                            writeOut(str6);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                Reset();
            }
            public bool Match(string s)
            {
                if (!active)
                    return false;
                switch (state)
                {
                    case 0:
                        string sk0 = "ERROR 1: FEHLER:  doppelter Schl";
                        if (s.Length >= sk0.Length && s[..sk0.Length].Equals(sk0))
                        {
                            str1 = s; state++; return true;
                        }
                        return false;
                    case 1:
                        string sk1 = "DETAIL:  Schl";
                        if (s.Length >= sk1.Length && s[..sk1.Length].Equals(sk1))
                        {
                            str2 = s; state++; return true;
                        }
                        WriteOutAndReset();
                        return false;
                    case 2:
                        string sk2 = "ERROR 1: INSERT command for new feature failed";
                        if (s.Length >= sk2.Length && s[..sk2.Length].Equals(sk2))
                        {
                            str3 = s; state++; return true;
                        }
                        WriteOutAndReset();
                        return false;
                    case 3:
                        string sk3 = "FEHLER:  doppelter Schl";
                        if (s.Length >= sk3.Length && s[..sk3.Length].Equals(sk3))
                        {
                            str4 = s; state++; return true;
                        }
                        WriteOutAndReset();
                        return false;
                    case 4:
                        string sk4 = "DETAIL:  Schl";
                        if (s.Length >= sk4.Length && s[..sk4.Length].Equals(sk4))
                        {
                            str5 = s; state++; return true;
                        }
                        WriteOutAndReset();
                        return false;
                    case 5:
                        string sk5 = "Command: INSERT INTO";
                        if (s.Length >= sk5.Length && s[..sk5.Length].Equals(sk5))
                        {
                            str6 = s; state++; return true;
                        }
                        WriteOutAndReset();
                        return false;
                    case 6:
                        string sk6 = "GDALVectorTranslate: Unable to write feature";
                        if (s.Length >= sk6.Length && s[..sk6.Length].Equals(sk6))
                        {
                            LogToDB();
                            Reset(); return true;
                        }
                        WriteOutAndReset();
                        break;
                }
                return false;
            }
        }
        protected readonly IConfiguration _configuration;
        protected readonly ILogger<IPostNasExecuter> _logger;
        protected readonly ExecutionArgs _args;
        protected readonly IDirectoryScanner _directoryScanner;
        protected readonly IDatabaseManager _databaseManager;
        //private string filename {get; set;} = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")+"-ogr2ogr";
        //protected string FileName 
        //{ 
        //    get => _args.Folder +  Path.DirectorySeparatorChar + "Log" +  Path.DirectorySeparatorChar + filename + ".log";
        //}
        protected string LogFileName(string FileName)
        {
            string filename = Path.GetFileNameWithoutExtension(FileName);
            return _args.Folder + Path.DirectorySeparatorChar + "Log" + Path.DirectorySeparatorChar + DateTime.Now.ToString("yyyyMMddHHmmss-") + filename + ".log";
        }
        public PostNasExecuter(
            IConfiguration configuration, ILogger<IPostNasExecuter> logger, 
            ExecutionArgs args, IDirectoryScanner directoryScanner, IDatabaseManager databaseManager)
        {
            this._configuration = configuration;
            this._logger = logger;
            this._args = args;
            this._directoryScanner = directoryScanner;
            this._databaseManager = databaseManager;
        }
        protected virtual NumbersAndInsertFailures RunOgr2gr(string filename, string output)
        {
            return new NumbersAndInsertFailures();
        }
        protected virtual void BeforeExecute(){}
        protected virtual void AfterExecute(){}
        public void Execute()
        {
            if (!this._args.OnlyPostRun.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            {
                this._logger.LogDebug("PostNasExecuter.Execute entered!");
                var files = _directoryScanner.Scan(_args.Folder, allowGaps: _args.AllowGaps.Equals("Yes", StringComparison.OrdinalIgnoreCase));
                BeforeExecute();
                _databaseManager.ProcessFiles(files,RunOgr2gr);
                AfterExecute();
            }
            if (!this._args.NoPostRun.Equals("Yes", StringComparison.OrdinalIgnoreCase))
            {
                _databaseManager.PostRun();
            }
        }
    }

}