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
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;

namespace MapEdit.Alkis.Services
{
    public partial class DatabaseManagerPG : DatabaseManager
    {
        private const string FST_NUM_SKALIERUNG = "1";
        private const string FST_NUM_GROESZE = "2.6";
        private class DeleteItem
        {
            public int ogc_fid = 0;
            public string typename = string.Empty;
            public string featureid = string.Empty;
            public string context = string.Empty;
            public string safetoignore = string.Empty;
            public string replacedBy = string.Empty;
            public string[] anlass = Array.Empty<string>();
            public string endet = string.Empty; // must be given in context "update" else empty!
            // not in "delete"
            public bool valid = true; // set to false on error -> then ignore and log!
            // extracted from featureid
            public string Fid = string.Empty;
            // extracted from featureid if included
            // if not fetch the earliest beginnt from db where endet is null
            public string Beginnt = string.Empty; 
            public string Endet = string.Empty;
            // static
            public static string UtcNow=string.Empty;
            public DeleteItem()
            {
                if (string.IsNullOrEmpty(UtcNow))
                {
                    // Aktuelles Datum und Uhrzeit in UTC
                    DateTime utcNow = DateTime.UtcNow;
                    // Formatieren im ISO 8601 Format: "YYYY-MM-DDTHH:MM:SSZ"
                    UtcNow = utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                }
            }
        }
        private class DeleteItemList
        {
            public List<DeleteItem> updates = new List<DeleteItem>();
            public List<DeleteItem> replaces = new List<DeleteItem>();
            public List<DeleteItem> deletes = new List<DeleteItem>();
            public void Add(DeleteItem item)
            {
                // context = 'delete'  => "endet" auf aktuelle Zeit setzen
                // context = 'replace' => "endet" des ersetzten auf "beginnt" des neuen Objekts setzen
                // context = 'update'  => "endet" auf übergebene Zeit setzen und "anlass" festhalten

                switch (item.context)
                {
                    case "update" : 
                        if (string.IsNullOrEmpty(item.endet))
                        {
                            item.valid = false; // endet needed -> log error
                        }
                        else
                        {
                            item.Endet = item.endet;
                        }
                        updates.Add(item); 
                        break;
                    case "replace": 
                        // if beginnt is not given (to find the replace candidate), fetch it later
                        replaces.Add(item); 
                        break;
                    case "delete" :
                        // due to standard: use actual date, i.e. item.endet is empty
                        if (string.IsNullOrEmpty(item.endet))
                        {
                            item.Endet = DeleteItem.UtcNow;
                        }
                        else
                        {
                            item.Endet = item.endet;
                        }
                        deletes.Add(item); 
                        break;
                }
            }
        }
        public override DBEngine Engine() => DBEngine.PG;

        private readonly IPostRunExtension? _additionalPostRunExtension;

        public DatabaseManagerPG(IConfiguration configuration, ILogger<IPostNasExecuter> logger, ExecutionArgs Args, IPostRunExtension? additionalPostRunExtension = null) : base(configuration, logger, Args)
        {
            _additionalPostRunExtension = additionalPostRunExtension;
        }

        public override bool StructureExists(string ForceOverwrite) => ConnectAndRun((a) => StructureCheck(a, ForceOverwrite));

        private bool ConnectAndRun(Func<NpgsqlConnection, bool> connected)
        {
            this._logger.LogDebug("DatabaseManager.Connect entered!");
            // Altes Vorgehen
            //var connString = "Host=" + _args.Host + ":" + _args.Port + ";Username=" + _args.Username + ";Password=" + _args.Password + ";Database=" + _args.Database +
            //    ";Default Command Timeout=" + _args.DefaultCommandTimeout.ToString();
            //try
            //{
            //    using (var conn = new NpgsqlConnection(connString))
            //    {
            //        conn.Open();
            //        if (conn.State != System.Data.ConnectionState.Open)
            //            return false;
            //        return connected.Invoke(conn);
            //    }
            //}
            //catch (Exception e)
            //{
            //    this._logger.LogError("DatabaseManager.Connect:" + e.Message);
            //    return false;
            //}
            ////////////////////////
            try
            {
                var connString = "Host=" + _args.Host + ":" + _args.Port + ";Username=" + _args.Username + ";Password=" + _args.Password + ";Database=" + _args.Database;
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(connString);
                // Command Timeout über ConnectionStringBuilder setzen
                dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = _args.DefaultCommandTimeout;
                dataSourceBuilder.ConnectionStringBuilder.IncludeErrorDetail =
                    _args.IncludeErrorDetail.Equals("Yes", StringComparison.OrdinalIgnoreCase);
                using (var dataSource = dataSourceBuilder.Build())
                {
                    using (var connection = dataSource.OpenConnection())
                    {
                        if (connection.State != System.Data.ConnectionState.Open)
                        {
                            _logger.LogError("DatabaseManager.Connect: Could not connect to " + _args.Database);
                            return false;
                        }
                        return connected.Invoke(connection);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError("DatabaseManager.Connect:" + e.Message);
                return false;
            }
        }

        protected bool StructureCheck(NpgsqlConnection conn, string ForceOverwrite)
        {
            ReadParameters(conn);
            if (string.Compare(ForceOverwrite, "YES", true) == 0)
            {
                return CreateAlkisStructure(conn);
            }
            return HasStructure(conn) || CreateAlkisStructure(conn);
        }

        private bool HasStructure(NpgsqlConnection conn) => this.AlkisStructure && this.AlkisVersion == this._AlkisVersion;

        protected bool TableExists(NpgsqlConnection conn, string tablename, DbCommand? cmd = null)
        {
            if (cmd is null)
            {
                using (cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    return TableExists(cmd, tablename);
                }
            }
            return TableExists(cmd, tablename);
        }

        private string readEmbeddedResource(string name, string version = "101") => ExecutionArgs.readEmbeddedResource(name, version);
        public bool CreateAlkisStructure(NpgsqlConnection _nc)
        {
            bool executeResult = true;
            bool exceptionThrown = false;
            string theException = string.Empty;
            try
            {
                Action<DbCommand> Run = (cmd) =>
                {
                    executeResult = true;
                    exceptionThrown = false;
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        theException = e.Message;
                        executeResult = false;
                        exceptionThrown = true;
                    }
                };
                Action<DbCommand, string> RunCmd = (cmd, text) =>
                {
                    cmd.CommandText = text;
                    Run(cmd);
                };
                using (DbCommand cmd = new NpgsqlCommand())
                {
                    cmd.Connection = _nc;
                    {
                        Assembly assembly = Assembly.GetExecutingAssembly();
                        string[] resourceNames = assembly.GetManifestResourceNames();
                    }
                    if (!TableExists(cmd, "me_parameter")) // only MapEdit-Databases
                    {   // Hier könnte noch das Datenmodell (MapEdit Alkis) abgefragt werden.
                        this._logger.LogError("DatabaseManager.CreateAlkisStructure: No MapEdit structure found.");
                        return false;
                        //cmd.CommandText = "create table ME_PARAMETER (NAME varchar(255), VALUE varchar(255), primary key (NAME))";
                        //cmd.ExecuteNonQuery();
                    }
                    {
                        RunCmd(cmd, "drop table me_alkis_files");
                        RunCmd(cmd, "create table me_alkis_files (configuration varchar(255), filename varchar(4000), checksum varchar(255), primary key (configuration,filename))");
                    }
                    {
                        RunCmd(cmd, "drop table delete_hist");
                        RunCmd(cmd, "create table delete_hist (" +
                                    "ogc_fid         serial NOT NULL, " +
                                    "filename        varchar, " +
                                    "typename        varchar, " +
                                    "featureid       varchar, " +
                                    "context         varchar, " + // delete / replace / update
                                    "safetoignore    varchar, " + // replace.safetoignore 'true' / 'false'
                                    "replacedBy      varchar, " + // gmlid
                                    "anlass          varchar[], " + // update.anlass
                                    "endet           character(20), " + // update.endet
                                    "ignored         boolean DEFAULT false, " + // Satz wurde nicht verarbeitet
                                    "PRIMARY KEY(ogc_fid))");
                        RunCmd(cmd, "CREATE INDEX delete_hist_filename ON delete_hist USING btree(filename)");
                        RunCmd(cmd, "CREATE INDEX delete_hist_featureid ON delete_hist USING btree(featureid)");

                    }
                    {
                        RunCmd(cmd, "drop table insert_error");
                        RunCmd(cmd, "create table insert_error (" +
                                    "ogc_fid         serial NOT NULL, " +
                                    "filename        varchar, " +
                                    "typename        varchar, " +
                                    "featureid       varchar, " +
                                    "beginnt         varchar, " +
                                    "replace_ignored boolean DEFAULT false, " + // Satz wurde ignoriert IncompleteMatch
                                    "incomplete_match boolean DEFAULT false, " + // Satz wurde ignoriert IncompleteMatch
                                    "PRIMARY KEY(ogc_fid))");
                        RunCmd(cmd, "CREATE INDEX insert_error_filename ON insert_error USING btree(filename)");
                        RunCmd(cmd, "CREATE INDEX insert_error_featureid ON insert_error USING btree(featureid)");
                    }
                    {
                        string functions = readEmbeddedResource("alkis-functions.g.sql")
                            .Replace(":\"parent_schema\".", " ")
                            .Replace("pg_temp.", " ")
                            .Replace(":alkis_epsg", databaseSRID.ToString());
                        cmd.CommandText = functions;
                        Run(cmd);
                        if (exceptionThrown)
                            return executeResult;
                    }
                    {
                        //Alle Tabellen löschen
                        RunCmd(cmd, "SELECT alkis_drop()");
                        if (exceptionThrown)
                            return executeResult;
                    }
                    {   // alkis_version -> NorGis compatible
                        RunCmd(cmd, "DROP TABLE IF EXISTS alkis_version");
                        string alver = "CREATE TABLE alkis_version(version integer)";
                        cmd.CommandText = alver;
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e) { return false; }
                        alver = "INSERT INTO alkis_version(version) VALUES(" + _AlkisVersion.ToString() + ")";
                        cmd.CommandText = alver;
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e) { return false; }
                        alver = "COMMENT ON TABLE alkis_version IS 'ALKIS: Schemaversion'";
                        cmd.CommandText = alver;
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e) { return false; }
                    }
                    {
                        // BW / BY - Koordinatensystem anlegen
                        cmd.CommandText = "SELECT alkis_create_bsrs(" + databaseSRID.ToString() + ");";
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e) { return false; }
                    }
                    {
                        // 20250317: Trigger zur Verarbeitung der Tabelle Delete wird nicht mehr erstellt.
                        // durch: '.Replace("SELECT pg_temp.create_trigger(:alkis_hist);"," ")'
                        // Ab sofort wird die Verarbeitung in
                        // 'virtual protected void AfterOgr2Ogr(DbConnection nc, DbCommand cmd, string file) { }'
                        // durchgeführt
                        string functions = readEmbeddedResource("alkis-trigger.sql");
                        functions = functions
                            .Replace("SELECT pg_temp.create_trigger(:alkis_hist);"," ")
                            .Replace("SET search_path = :\"alkis_schema\", public;", " ; ") // auch am Ende der Prozeduren
                            .Replace(":\"parent_schema\".", " ")
                            .Replace(":alkis_epsg", databaseSRID.ToString())
                            .Replace(":alkis_hist", "false")
                            .Replace("pg_temp.", " ")
                            .Replace("CREATE FUNCTION  create_trigger", "CREATE OR REPLACE FUNCTION create_trigger ");
                        cmd.CommandText = functions;
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e) { return false; }
                    }
                    {
                        string content = readEmbeddedResource("alkis-schema.g.sql")
                            .Replace("SET search_path = :\"alkis_schema\", :\"postgis_schema\", public;", " ; ") // auch am Ende der Prozeduren
                            .Replace(":alkis_epsg", databaseSRID.ToString());
                        RunCmd(cmd, content);
                        if (exceptionThrown)
                            return executeResult;
                    }
                    //{// Könnte Standard ersetzen!!!
                    //    string content = readForkFile("alkis-compat.g.sql")
                    //        .Replace("SET search_path = :\"alkis_schema\", :\"postgis_schema\", public;", " ") // auch am Ende der Prozeduren
                    //        .Replace("SET search_path = :\"parent_schema\", :\"postgis_schema\", public;", " ") // auch am Ende der Prozeduren
                    //        .Replace("SET search_path = :\"postgis_schema\", :\"parent_schema\", public;", " ") // auch am Ende der Prozeduren
                    //        .Replace("\\unset ON_ERROR_STOP", " ")
                    //        .Replace("\\unset ECHO", " ")
                    //        .Replace("\\set ON_ERROR_STOP", " ")
                    //        .Replace("\\set ECHO errors", " ")
                    //        .Replace("SET search_path = public;", " ");
                    //    cmd.CommandText = content;
                    //    try
                    //    {
                    //        cmd.ExecuteNonQuery();
                    //    }
                    //    catch (Exception e) { return false; }
                    //}
                    {
                        /*
                         * In alkis-po-tables.sql wird das Ergebnis einer Abfrage mit \gexec ausgeführt
                         * -> die Datei muss aufgespalten werden: Später
                         */
                        string content = readEmbeddedResource("alkis-po-tables.g1.sql")
                            .Replace("SET search_path = :\"alkis_schema\", :\"postgis_schema\", public;", " ") // auch am Ende der Prozeduren
                            .Replace(":alkis_epsg", databaseSRID.ToString());
                        RunCmd(cmd, content);
                        if (exceptionThrown)
                            return executeResult;

                        //--Gesamtview der ALKIS-Objekte
                        //--
                        //
                        //SELECT alkis_dropobject('alkis_po_objekte');
                        //SELECT
                        //  E'CREATE VIEW alkis_po_objekte AS\n  ' ||
                        //  array_to_string(
                        //    array_agg(
                        //      format('SELECT gml_id,beginnt,endet,%L AS table_name FROM %I', table_name, table_name)
                        //    ),
                        //    E' UNION ALL\n  '
                        //  )
                        //    FROM(
                        //      SELECT
                        //        table_name
                        //      FROM information_schema.columns
                        //      WHERE table_schema =:'alkis_schema' AND column_name IN('gml_id', 'beginnt', 'endet')
                        //      GROUP BY table_name
                        //      HAVING count(*) = 3
                        //    ) AS t
                        ////\gexec
                        //
                        {
                            cmd.CommandText = "SELECT alkis_dropobject('alkis_po_objekte')";
                            RunCmd(cmd, content);
                            if (exceptionThrown)
                                return executeResult;
                            cmd.CommandText =
                                "SELECT  E'CREATE VIEW alkis_po_objekte AS\\n  ' ||  array_to_string(" +
                                "        array_agg(" +
                                "                  format('SELECT gml_id,beginnt,endet,%L AS table_name FROM %I', table_name, table_name)" +
                                "                  )," +
                                "                  E' UNION ALL\\n  '" +
                                "                 )" +
                                "FROM(" +
                                "  SELECT" +
                                "    table_name " +
                                "  FROM information_schema.columns  " +
                                "  WHERE column_name IN('gml_id', 'beginnt', 'endet') " +
                                "  GROUP BY table_name " +
                                "  HAVING count(*) = 3 " +
                                " ) AS t";
                            string res;
                            using (var reader = cmd.ExecuteReader())
                            {
                                reader.Read();
                                res = reader.GetString(0);
                            }
                            if (!string.IsNullOrEmpty(res))
                            {
                                cmd.CommandText = res;
                                cmd.ExecuteNonQuery();
                            }
                        }

                        content = readEmbeddedResource("alkis-po-tables.g2.sql")
                            .Replace("SET search_path = :\"alkis_schema\", :\"postgis_schema\", public;", " ") // auch am Ende der Prozeduren
                            .Replace(":alkis_epsg", databaseSRID.ToString());
                        RunCmd(cmd, content);
                        if (exceptionThrown)
                            return executeResult;
                    }
                    {
                        string content = readEmbeddedResource("0_alkis-signaturen.g.sql")
                            .Replace("SET search_path = :\"alkis_schema\", :\"postgis_schema\", public;", " ") // auch am Ende der Prozeduren
                            .Replace(":alkis_epsg", databaseSRID.ToString());
                        RunCmd(cmd, content);
                        if (exceptionThrown)
                            return executeResult;
                    }
                    {
                        string content = readEmbeddedResource("alkis-punktsignaturen.sql");
                        RunCmd(cmd, content);
                        if (exceptionThrown)
                            return executeResult;
                    }
                    // Abschluss
                    WriteParameters(cmd);
                    return true;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected void ReadParameters(NpgsqlConnection _nc)
        {
            try
            {
                if (_nc != null && _nc.State == System.Data.ConnectionState.Open)
                {
                    using var cmd = new NpgsqlCommand("select name, value from me_parameter", _nc);
                    ReadParameters(cmd);
                }
            }
            catch (Exception)
            {
                //throw;
            }
        }

        protected override bool ConnectAndRun(Action<DbConnection, DbCommand> connected)
        {
            this._logger.LogDebug("DatabaseManager.Connect entered!");
            var connString = "Host=" + _args.Host + ":" + _args.Port + ";Username=" + _args.Username + ";Password=" + _args.Password + ";Database=" + _args.Database;
            if (_args.IncludeErrorDetail.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                connString += ";Include Error Detail=true";
            try
            {
                using (var conn = new NpgsqlConnection(connString))
                {
                    conn.Open();
                    if (conn.State != System.Data.ConnectionState.Open)
                        return false;
                    connected.Invoke(conn, new NpgsqlCommand { Connection = conn });
                    return true;
                }
            }
            catch (Exception e)
            {
                this._logger.LogError("DatabaseManager.Connect:" + e.Message);
                return false;
            }
        }

        private static List<string> FetchBeginnt(DbCommand cmd, string tablename, string featureid)
        {
            List<string> res = new();
            cmd.CommandText = "SELECT beginnt FROM " + tablename + " WHERE gml_id='" + 
                featureid + "' AND endet IS NULL order by beginnt asc";
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    res.Add(reader.GetString(0));
                }
            }
            return res;
        }
        /* Historie: 
         * Es wird zunächst immer mit Historie gearbeitet. Dabei werden alle "angefassten Tabellen" gespeichert.
         * Sollte keine Historie gewünscht werden, so werden am Ende aus allen gespeicherten Tabellen die Einträge geläscht,
         * für die endet != null gilt.
         * 
         
         */
        protected override void AfterOgr2Ogr(DbConnection nc, DbCommand cmd, string file, NumbersAndInsertFailures ogr2ogrRes, ScanResult sr) 
        {
            Dictionary<string, AlkisNumbers> ao = ogr2ogrRes.Numbers;
            InsertFailureList ifl = ogr2ogrRes.InsertFailures;
            this._logger.LogInformation("...");
            this._logger.LogInformation("Nachbearbeitung fuer Datei:" + file);
            this._logger.LogInformation("...");
            if (sr.Inserts + sr.Replaces + sr.Deletes + sr.Updates > 0)
            {
                this._logger.LogInformation("Prescan: Insert: " + sr.Inserts +
                " Replace: " + sr.Replaces + " Delete: " + sr.Deletes + " Update: " + sr.Updates);
            }
            bool exceptionThrown = false;
            string theException = string.Empty;
            try
            {
                Action<DbCommand> Run = (cmd) =>
                {
                    exceptionThrown = false;
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        theException = e.Message;
                        exceptionThrown = true;
                    }
                };
                Action<DbCommand, string> RunCmd = (cmd, text) =>
                {
                    cmd.CommandText = text;
                    Run(cmd);
                };
                List<string> typenames = new();
                List<Tuple<string, DeleteItemList>> deleteItems = new();
                {
                    cmd.CommandText = 
                        "select ogc_fid, typename, featureid, context, safetoignore, replacedBy, anlass, endet from \"delete\"" +
                        " order by typename, context";
                    string currentTypename = "-";
                    DeleteItemList cdil = new();
                    //
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var deleteItem = new DeleteItem
                        {
                            ogc_fid = reader["ogc_fid"] as int? ?? 0,
                            typename = reader["typename"] as string ?? string.Empty,
                            featureid = reader["featureid"] as string ?? string.Empty,
                            context = reader["context"] as string ?? "delete",
                            safetoignore = reader["safetoignore"] as string ?? "false",
                            replacedBy = reader["replacedby"] as string ?? string.Empty,
                            anlass = reader["anlass"] as string[] ?? Array.Empty<string>(),
                            endet = reader["endet"] as string ?? string.Empty
                        }; // if context is null => context ="delete"
                        deleteItem.typename = deleteItem.typename.ToLower();
                        if (!currentTypename.Equals(deleteItem.typename, StringComparison.OrdinalIgnoreCase))
                        {
                            currentTypename = deleteItem.typename;
                            typenames.Add(currentTypename);
                            cdil = new DeleteItemList();
                            deleteItems.Add(new Tuple<string, DeleteItemList>(deleteItem.typename, cdil));
                        }
                        deleteItem.context = deleteItem.context.ToLower();
                        deleteItem.safetoignore = deleteItem.safetoignore.ToLower();
                        // "beginnt" given?
                        if (deleteItem.featureid.Length == 32)
                        {
                            deleteItem.Fid = deleteItem.featureid[..16];
                            deleteItem.Beginnt = deleteItem.featureid.Substring(16, 4) + "-" +
                                                 deleteItem.featureid.Substring(20, 2) + "-" +
                                                 deleteItem.featureid.Substring(22, 2) + "T" +
                                                 deleteItem.featureid.Substring(25, 2) + ":" +
                                                 deleteItem.featureid.Substring(27, 2) + ":" +
                                                 deleteItem.featureid.Substring(29, 2) + "Z";
                        }
                        else if (deleteItem.featureid.Length == 16)
                        {
                            deleteItem.Fid = deleteItem.featureid;
                            /* get the oldest object still alive
                             * log if not found but no error "Modellschwaeche"
                            run 'SELECT min(beginnt) FROM ' || NEW.typename  || ' WHERE gml_id=''' || 
                                 NEW.featureid || '''' || ' AND endet IS NULL' INTO beginnt;

                             */
                        }
                        else 
                        {
                            deleteItem.valid=false; // log error / Fid stays empty!
                        }
                        cdil.Add(deleteItem);
                    }
                }
                {
                    cmd.CommandText =
                        "select context, count(1) from \"delete\" group by context";
                    using var reader = cmd.ExecuteReader();
                    this._logger.LogInformation("Operationen:");
                    while (reader.Read())
                    {
                        this._logger.LogInformation(reader.GetString(0) + " " + reader.GetInt32(1));
                    }
                }
                // Process entries from table 'delete'
                {
                    /* siehe 07-1_Anlage_Zuswirk_Dokument.pdf im sourcd/Documentatio
                     * 4.1.3.2 Funktionsumfang der Schnittstelle
                     * Es müssen folgende Operationen realisiert werden:
                     * ▪ Eintragen von Objekten (INSERT)
                     * ▪ Löschen von Objekten (DELETE), nicht beim NBA-Verfahren
                     * ▪ Löschen von Objekten mit Historie (UPDATE), nur beim NBA-Verfahren
                     * ▪ Überschreiben von Objekten (REPLACE)
                     * Der Datenaustausch vom ALKIS zum FNO-Fachinformationssystem soll mit einem
                     * fallbezogenen NBA-Verfahren aufgebaut werden.
                     * Dazu sind folgende Operationen zu implementieren: INSERT, REPLACE und UPDATE. UPDATE
                     * wird für das fallbezogene NBA-Verfahren mit Historie anstelle von DELETE benötigt, um das
                     * Lebenszeitintervall-Ende sowie den Untergangsanlass von Flurstücken zu übermitteln.
                     * Der Datenaustausch vom FNO-Fachinformationssystem zum ALKIS soll mit
                     * Fortführungsaufträgen realisiert werden.
                     * Dazu sind folgende Operationen zu implementieren: INSERT, DELETE und REPLACE.
                     */
                    foreach (var (typename, list) in deleteItems)
                    {
                        string tableName = typename; // just for clarity
                        // Updates
                        foreach (var uItem in list.updates)
                        {
                            if (uItem is null)
                                continue; // should not happen
                            /* Lösche ein Objekt mit Historie mit übergebenem Lebenszeitintervall-Ende und Untergangsanlass
                             * Sollte es fehlerhafterweise mehrere Objekte mit unterschiedlichem beginnt und endet==null geben,
                             * so werden alle auf endet=Endet gesetzt. DIES WIRD NICHT ALS FEHLER GELOGGT!
                             */
                            if (uItem.valid)
                            {
                                string s = " UPDATE " + typename + " SET endet='" + uItem.Endet + "'";
                                if (uItem.anlass.Length > 0)
                                {
                                    s += ",anlass=array_cat(anlass,'{" + string.Join(",", uItem.anlass) + "}')";
                                }
                                s += " WHERE gml_id='" + uItem.Fid + "' AND endet is null";
                                cmd.CommandText = s;
                                cmd.ExecuteNonQuery();
                            }
                            else
                            {
                                this._logger.LogError("DatabaseManager: Post processing error for op update on table: " + 
                                    typename +" gml_id: " + uItem.Fid + " \"endet\" not given.");
                            }
                        }
                        // Replaces
                        foreach (var rItem in list.replaces)
                        {
                            if (rItem is null)
                                continue; // should not happen
                            if (rItem.valid)
                            {
                                List<string> beginntList=FetchBeginnt(cmd, typename, rItem.Fid);
                                int cnt = beginntList.Count;
                                if (cnt > 0)
                                {
                                    if (cnt == 1)
                                    {
                                        if (_args.WarnForReplaceWithSameBeginnt.Equals("yes",StringComparison.OrdinalIgnoreCase))
                                        {
                                            this._logger.LogWarning("DatabaseManager: Post processing warning for op replace on table: " +
                                            typename + " gml_id: " + rItem.featureid + " impossible because only one object found -> Modellschwaeche?");
                                        }
                                        string s = " UPDATE \"delete\"  SET ignored=true " +
                                         " WHERE ogc_fid = " + rItem.ogc_fid;
                                        cmd.CommandText = s;
                                        cmd.ExecuteNonQuery();
                                        ifl.MarkIgnored(rItem.typename, rItem.featureid, beginntList[0]);
                                        if (!ao.ContainsKey(rItem.typename))
                                        {
                                            ao.Add(rItem.typename, new AlkisNumbers { Ignored = 1 });
                                        }
                                        else
                                        {
                                            ao[rItem.typename].Ignored++;
                                        }                                      
                                    }
                                    else
                                    {
                                        if (cnt > 2)
                                        {
                                            // Nur warnen: Alle älteren Instanzen werden auf endet gesetzt
                                            this._logger.LogWarning("DatabaseManager: Post processing warning for op replace on table: " +
                                            typename + " gml_id: " + rItem.featureid + " multiple objects found.");
                                        }
                                        rItem.Endet = beginntList[cnt - 1];
                                        string s = " UPDATE " + typename + " SET endet='" + rItem.Endet + "'";
                                        s += " WHERE gml_id='" + rItem.Fid + "' AND endet is null " +
                                             "and beginnt<>'" + rItem.Endet + "'";
                                        cmd.CommandText = s;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    this._logger.LogError("DatabaseManager: Post processing error for op replace on table: " +
                                        typename + " gml_id: " + rItem.featureid + " not object found to replace.");

                                }
                            }
                            else
                            {
                                this._logger.LogError("DatabaseManager: Post processing error for op replace on table: " +
                                    typename + " gml_id: " + rItem.featureid + " not valid.");
                            }
                        }
                        // Deletes
                        foreach (var dItem in list.deletes)
                        {
                            if (dItem is null)
                                continue; // should not happen
                            if (dItem.valid)
                            {
                                string s = " UPDATE " + typename + " SET endet='" + dItem.Endet + "'"
                                 + " WHERE gml_id='" + dItem.Fid + "' AND endet is null "; 
                                cmd.CommandText = s;
                                cmd.ExecuteNonQuery();
                            }
                            else
                            {
                                this._logger.LogError("DatabaseManager: Post processing error for op delete on table: " +
                                    typename + " gml_id: " + dItem.Fid + ": something weird happend.");
                            }
                        }
                    }
                    if (!_args.KeepHistory.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    { // if you want to keep the history, comment this out or use a parameter.
                        foreach (var typename in typenames)
                        {
                            cmd.CommandText = "DELETE FROM " + typename + " WHERE endet IS NOT NULL";
                            cmd.ExecuteNonQuery();
                        }
                    }
                    {
                        //select ignored, count(1) from delete_hist where context='Replace' group by ignored
                    }
                    {

                        RunCmd(cmd,
                                    "insert into delete_hist (typename, featureid, context, safetoignore, replacedBy, anlass, endet, ignored)" +
                                    " select typename, featureid, context, safetoignore, replacedBy, anlass, endet, ignored from \"delete\""
                              );
                        RunCmd(cmd,
                                    "update delete_hist set filename='" + file + "' where filename is null"
                              );
                        RunCmd(cmd, "delete from \"delete\""
                              );
                        if (exceptionThrown)
                        {
                            this._logger.LogError("DatabaseManager: Post processing error:" + theException);
                            return;
                        }
                    }
                }
                foreach(var f in ifl.Failures)
                {
                    string sql = "insert into insert_error (filename, typename, featureid, beginnt, replace_ignored, incomplete_match) " +
                        "values ('" + file + "','" + f.Table + "','" + f.Gml_ID + "','" + f.Beginnt + "', " + 
                        (f.ReplaceIgnored ? "true" : "false") + ", " + (f.IncompleteMatch ? "true" : "false") + ")";
                    RunCmd(cmd, sql);
                }
            }
            catch (Exception)
            {
                throw;
            }
            int gelesen_gesamt = 0;
            foreach(var (typename, numbers) in ao)
            {
                if (typename.Equals("delete", StringComparison.OrdinalIgnoreCase))
                {
                    this._logger.LogInformation("Operationen (update, delete, replace): " + numbers.Read);
                }
                else
                {
                    gelesen_gesamt += numbers.Read;
                    this._logger.LogInformation("Insert+Replace: " + numbers.Read +
                    /* nur der Anteil von Replace, der geschrieben wurde" geschrieben: " + numbers.Written + */ 
                    " ignoriert: " + numbers.Ignored + " (Modellschwaeche) => "+ typename);
                }
            }
            if(gelesen_gesamt > 0)
            {
                this._logger.LogInformation("Operationen (insert, replace): "+ gelesen_gesamt);
            }
            this._logger.LogInformation("...");
            this._logger.LogInformation("Nachbearbeitung beendet");
            this._logger.LogInformation("...");
        }
#if Del_not_Commented_out

-- Löschsatz verarbeiten (MIT Historie)
-- context='delete'        => "endet" auf aktuelle Zeit setzen
-- context='replace'       => "endet" des ersetzten auf "beginnt" des neuen Objekts setzen
-- context='update'        => "endet" auf übergebene Zeit setzen und "anlass" festhalten
CREATE OR REPLACE FUNCTION delete_feature_hist() RETURNS TRIGGER AS $$
DECLARE
	n INTEGER;
	beginnt TEXT;
	s TEXT;
BEGIN
	NEW.context := coalesce(lower(NEW.context),'delete');

	IF length(NEW.featureid)=32 THEN
		beginnt := substr(NEW.featureid, 17, 4) || '-'
			|| substr(NEW.featureid, 21, 2) || '-'
			|| substr(NEW.featureid, 23, 2) || 'T'
			|| substr(NEW.featureid, 26, 2) || ':'
			|| substr(NEW.featureid, 28, 2) || ':'
			|| substr(NEW.featureid, 30, 2) || 'Z'
			;
	ELSIF length(NEW.featureid)=16 THEN
		-- Ältestes nicht gelöschtes Objekt
		EXECUTE 'SELECT min(beginnt) FROM ' || NEW.typename
			|| ' WHERE gml_id=''' || NEW.featureid || ''''
			|| ' AND endet IS NULL'
			INTO beginnt;

		IF beginnt IS NULL THEN
			RAISE EXCEPTION '%: Keinen Kandidaten zum Löschen gefunden.', NEW.featureid;
		END IF;
	ELSE
		RAISE EXCEPTION '%: Identifikator gescheitert.', NEW.featureid;
	END IF;

	IF NEW.context='delete' THEN
		SELECT endet INTO NEW.endet FROM pg_temp.deletedate;

	ELSIF NEW.context='update' THEN
		IF NEW.endet IS NULL THEN
			RAISE EXCEPTION '%: Endedatum nicht gesetzt', NEW.featureid;
		END IF;

	ELSIF NEW.context='replace' THEN
		NEW.safetoignore := lower(NEW.safetoignore);
		IF NEW.safetoignore IS NULL THEN
			RAISE EXCEPTION '%: safeToIgnore nicht gesetzt.', NEW.featureid;
		ELSIF NEW.safetoignore<>'true' AND NEW.safetoignore<>'false' THEN
			RAISE EXCEPTION '%: safeToIgnore ''%'' ungültig (''true'' oder ''false'' erwartet).', NEW.featureid, NEW.safetoignore;
		END IF;

		IF length(NEW.replacedby)=32 AND NEW.replacedby<>NEW.featureid THEN
			NEW.endet := substr(NEW.replacedby, 17, 4) || '-'
				  || substr(NEW.replacedby, 21, 2) || '-'
				  || substr(NEW.replacedby, 23, 2) || 'T'
				  || substr(NEW.replacedby, 26, 2) || ':'
				  || substr(NEW.replacedby, 28, 2) || ':'
				  || substr(NEW.replacedby, 30, 2) || 'Z'
				  ;
		END IF;

		IF NEW.endet IS NULL THEN
			-- Beginn des ersten Nachfolgeobjektes
			EXECUTE 'SELECT min(beginnt) FROM ' || NEW.typename || ' a'
				|| ' WHERE gml_id=''' || substr(NEW.replacedby, 1, 16) || ''''
				|| ' AND beginnt>''' || beginnt || ''''
				INTO NEW.endet;
		ELSE
			EXECUTE 'SELECT count(*) FROM ' || NEW.typename
				|| ' WHERE gml_id=''' || substr(NEW.replacedby, 1, 16) || ''''
				|| ' AND beginnt=''' || NEW.endet || ''''
				INTO n;
			IF n<>1 THEN
				RAISE EXCEPTION '%: Ersatzobjekt % % nicht gefunden.', NEW.featureid, NEW.replacedby, NEW.endet;
			END IF;
		END IF;

		IF NEW.endet IS NULL THEN
			-- Abbrechen, wenn Austausch nicht ignoriert werden
			-- darf, aber nicht wenn ein Objekt (sinnloserweise?)
			-- gegen selbst getauscht werden soll.
			IF NEW.safetoignore='false' AND NEW.featureid<>NEW.replacedby THEN
				RAISE EXCEPTION '%: Beginn des Ersatzobjekts % nicht gefunden.', NEW.featureid, NEW.replacedby;
				-- RAISE NOTICE '%: Beginn des ersetzenden Objekts % nicht gefunden.', NEW.featureid, NEW.replacedby;
			END IF;

			NEW.ignored=true;
			RETURN NEW;
		END IF;

	ELSE
		RAISE EXCEPTION '%: Ungültiger Kontext % (''delete'', ''replace'' oder ''update'' erwartet).', NEW.featureid, NEW.context;

	END IF;

	s := 'UPDATE ' || NEW.typename || ' SET endet=''' || NEW.endet || '''';

	IF NEW.context='update' AND NEW.anlass IS NOT NULL THEN
		s := s || ',anlass=array_cat(anlass,''{' || array_to_string(NEW.anlass,',') || '}'')';
	END IF;

	s := s || ' WHERE gml_id=''' || substr(NEW.featureid, 1, 16) || ''''
	       || ' AND beginnt=''' || beginnt || ''''
	       ;
	EXECUTE s;
	GET DIAGNOSTICS n = ROW_COUNT;
	-- RAISE NOTICE 'SQL[%]:%', n, s;
	IF n<>1 THEN
		IF n=0 THEN
			s := 'SELECT count(*),min(beginnt) FROM ' || NEW.typename || ' WHERE gml_id=''' || substr(NEW.featureid, 1, 16) || ''' AND endet IS NULL';
			EXECUTE s INTO n, beginnt;
			IF (n=0 AND NEW.context IN ('delete','update')) OR (n=1 AND NEW.context='replace') THEN
				RAISE NOTICE '%: Kein Objekt gefunden [%:%]', NEW.featureid, NEW.context, n;
				NEW.ignored=true;
				RETURN NEW;
			ELSIF n=2 AND beginnt IS NOT NULL THEN
				s := 'UPDATE ' || NEW.typename || ' a SET endet=''' || NEW.endet || '''';

				IF NEW.anlass IS NOT NULL THEN
					s := s || ',anlass=array_cat(anlass,''{' || array_to_string(NEW.anlass,',') || '}'')';
				END IF;

				s := s || ' WHERE gml_id=''' || substr(NEW.featureid, 1, 16) || ''''
				       || ' AND beginnt=''' || beginnt || ''''
				       ;
				EXECUTE s;
				GET DIAGNOSTICS n = ROW_COUNT;
				-- RAISE NOTICE 'SQL[%]:%', n, s;
				IF n<>1 THEN
					RAISE EXCEPTION '%: Aktualisierung des Vorgängerobjekts von % schlug fehl [%:%]', NEW.featureid, beginnt, NEW.context, n;
				END IF;
			ELSE
				RAISE NOTICE '%: Kein eindeutiges Vorgängerobjekt gefunden [%:%]', NEW.featureid, NEW.context, n;
				RETURN NEW;
			END IF;
		ELSE
			RAISE EXCEPTION '%: % schlug fehl [%]', NEW.featureid, NEW.context, n;
		END IF;
	END IF;

	NEW.ignored := false;
	RETURN NEW;
END;
$$ LANGUAGE plpgsql SET search_path = :"alkis_schema", public;

-- Abwandlung der Hist-Version als Kill-Version.
-- Die "gml_id" muss in der Datenbank das Format character(16) haben.
-- Dies kann auch Abgabeart 3100 verarbeiten. Historische Objekte werden aber sofort entfernt.
CREATE OR REPLACE FUNCTION delete_feature_kill() RETURNS TRIGGER AS $$
DECLARE
	n INTEGER;
	vbeginnt TEXT;
	replgml TEXT;
	featgml TEXT;
	s TEXT;
BEGIN
	-- Version 2014-09-23, replace führt auch zum Löschen des Vorgängerobjektes
	NEW.context := coalesce(lower(NEW.context),'delete');

	IF NEW.anlass IS NULL THEN
		NEW.anlass := ARRAY[]::varchar[];
	END IF;
	featgml := substr(NEW.featureid, 1, 16); -- gml_id ohne Timestamp

	IF length(NEW.featureid)=32 THEN
		-- beginnt-Zeit der zu löschenden Vorgänger-Version des Objektes
		vbeginnt := substr(NEW.featureid, 17, 4) || '-'
			 || substr(NEW.featureid, 21, 2) || '-'
			 || substr(NEW.featureid, 23, 2) || 'T'
			 || substr(NEW.featureid, 26, 2) || ':'
			 || substr(NEW.featureid, 28, 2) || ':'
			 || substr(NEW.featureid, 30, 2) || 'Z' ;
	ELSIF length(NEW.featureid)=16 THEN
		-- Ältestes nicht gelöschtes Objekt
		EXECUTE 'SELECT min(beginnt) FROM ' || NEW.typename
			|| ' WHERE gml_id=''' || featgml || '''' || ' AND endet IS NULL'
			INTO vbeginnt;

		IF vbeginnt IS NULL THEN
			RAISE EXCEPTION '%: Keinen Kandidaten zum Löschen gefunden.', NEW.featureid;
		END IF;
	ELSE
		RAISE EXCEPTION '%: Identifikator gescheitert.', NEW.featureid;
	END IF;

	IF NEW.context='replace' THEN
		NEW.safetoignore := lower(NEW.safetoignore);
		IF NEW.safetoignore IS NULL THEN
			RAISE EXCEPTION '%: safeToIgnore nicht gesetzt.', NEW.featureid;
		ELSIF NEW.safetoignore<>'true' AND NEW.safetoignore<>'false' THEN
			RAISE EXCEPTION '%: safeToIgnore ''%'' ungültig (''true'' oder ''false'' erwartet).', NEW.featureid, NEW.safetoignore;
		END IF;

	ELSIF NEW.context NOT IN ('delete', 'update') THEN
		RAISE EXCEPTION '%: Ungültiger Kontext % (''delete'', ''replace'' oder ''update'' erwartet).', NEW.featureid, NEW.context;
	END IF;

	-- Vorgänger-ALKIS-Objekt löschen
	s := 'DELETE FROM ' || NEW.typename || ' WHERE gml_id=''' || featgml || ''' AND beginnt=''' || vbeginnt || '''' ;
	EXECUTE s;
	GET DIAGNOSTICS n = ROW_COUNT;
	-- RAISE NOTICE 'SQL[%]:%', n, s;
	IF n=1 THEN
		NEW.ignored := false;
	ELSE
		RAISE NOTICE '%: % schlug fehl ignoriert [%]', NEW.featureid, NEW.context, n;
		NEW.ignored := true;
	END IF;

	RETURN NEW;
END;












#endif
    }
}