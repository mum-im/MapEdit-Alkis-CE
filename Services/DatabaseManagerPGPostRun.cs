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

using Microsoft.Extensions.Logging;
using Npgsql;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;

namespace MapEdit.Alkis.Services
{
    public partial class DatabaseManagerPG : DatabaseManager
    {
        /* Modellarten
+-------------+--------------------------------------------+---------------------------+--------------------------------------------+
| Kürzel      | Langform                                   | Maßstab / Zweck           | Inhalt / Nutzung                           |
+-------------+--------------------------------------------+---------------------------+--------------------------------------------+
| DLKM        | Digitales Liegenschaftskataster-Modell     | Basismodell (Detail)      | Katasterdaten, Flurstücke, Eigentümer      |
| DKKM1000    | Digitales Katasterkarten-Modell 1:1000     | Darstellung (detailliert) | Karte, Darstellung, Beschriftung           |
| DKKM2000    | Digitales Katasterkarten-Modell 1:2000     | Darstellung (vereinfacht) | Generalisierte Karte                       |
| DKKM5000    | Digitales Katasterkarten-Modell 1:5000     | Übersichtsdarstellung     | Stark generalisierte Karte                 |
| DLM         | Digitales Landschaftsmodell                | Landschaft, Topographie   | ATKIS-Daten, nicht Kataster                |
| DLKM_V      | Verwaltungs-/Veröffentlichungsvariante     | interne Nutzung           | ggf. anonymisierte Verwaltungsdaten        |
+-------------+--------------------------------------------+---------------------------+--------------------------------------------+
*/

        public override void PostRun()
        {
            ConnectAndRun(PostRun);
        }
        public bool PostRun(NpgsqlConnection _nconn)
        {
            int jumped = 0;
            bool executeResult = true;
            bool exceptionThrown = false;
            string? theException;
            this._logger.LogDebug(".");
            this._logger.LogDebug("DatabaseManager.PostRun entered!");

            string modell_clause= "modell && ARRAY['DLKM','DKKM1000','norGIS']::character varying[]"; // && overlap operator for arrays
            try
            {
                { // fix MultiSurface
                    List<string> geomTables = new();
                    using (DbCommand cmd = new NpgsqlCommand("select f_table_name from geometry_columns where lower(f_geometry_column) = 'wkb_geometry'"))
                    {
                        cmd.Connection = _nconn;
                        using (DbDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string name = reader.GetString(0);
                                geomTables.Add(name);
                            }
                        }
                        foreach (var name in geomTables)
                        {
                            cmd.CommandText = "select count(1) from " + name + " where ST_GeometryType(wkb_geometry) = 'ST_MultiSurface' ";
                            bool hasMultiSurface = false;
                            using (DbDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    Int32 cnt = reader.GetInt32(0);
                                    if (cnt > 0)
                                        hasMultiSurface = true;
                                }
                            }
                            if (hasMultiSurface)
                            {
                                cmd.CommandText = "update " + name + " set wkb_geometry=ST_CurveToLine(wkb_geometry) where ST_GeometryType(wkb_geometry) = 'ST_MultiSurface'";
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                Action<DbCommand> Run = (cmd) =>
                {
                    try
                    {
                        if (!exceptionThrown)
                        {
                            cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            jumped++;
                        }
                    }
                    catch (Exception e)
                    {
                        theException = e.Message;
                        executeResult = false;
                        exceptionThrown = true;
                        this._logger.LogError("DatabaseManager.PostRun exception thrown:");
                        this._logger.LogError(theException);
                        this._logger.LogError("DatabaseManager.PostRun command:");
                        this._logger.LogError(cmd.CommandText);
                        this._logger.LogError("Stopping PostRun");
                    }
                };
                // "step" ist eine fest im Quellcode vergebene 4-stellige ID pro Aufrufstelle
                // (Zehnerschritte, z.B. "0010", "0020", ... — Basisteil: 0010-1120, VACUUM am
                // Ende: 9990, IPostRunExtension z.B. MTablesPostProcessor: eigener Block ab 5010),
                // KEIN Laufzeit-Zähler mehr. Damit identifiziert die "[PostRunProgress]"-Logzeile
                // eindeutig und dauerhaft die Aufrufstelle im Quellcode (unabhängig davon ob
                // an anderer Stelle Schritte eingefügt/entfernt werden), und der Controller kann
                // sie 1:1 für eine Fortschrittsanzeige mitlesen.
                Action<DbCommand, string, string> RunCmd = (cmd, text, step) =>
                {
                    cmd.CommandText = text;
                    Run(cmd);
                    this._logger.LogInformation("[PostRunProgress] {Step:l}", step);
                };
                Func<DbCommand, string, object?> RunScalar = (cmd, text) =>
                {
                    cmd.CommandText = text;
                    return cmd.ExecuteScalar();
                };

                using (DbCommand cmd = new NpgsqlCommand())
                {
                    // später als parameter?
                    bool rebuildMap = true;
                    bool part1 = true;
                    cmd.Connection = _nconn;
                    cmd.CommandTimeout = _args.DefaultCommandTimeout;
                    {
                        if (part1)
                        {
                            if (rebuildMap)
                            {
                                RunCmd(cmd, "DELETE FROM po_points;", "0010");
                                RunCmd(cmd, "DELETE FROM po_lines;", "0020");
                                RunCmd(cmd, "DELETE FROM po_polygons;", "0030");
                                RunCmd(cmd, "DELETE FROM po_labels;", "0040");
                                RunCmd(cmd, "DELETE FROM po_darstellung;", "0050");
                                RunCmd(cmd, "DELETE FROM po_ppo;", "0060");
                                RunCmd(cmd, "DELETE FROM po_lpo;", "0070");
                                RunCmd(cmd, "DELETE FROM po_fpo;", "0080");
                                RunCmd(cmd, "DELETE FROM po_pto;", "0090");
                                RunCmd(cmd, "DELETE FROM po_lto;", "0100");
                                RunCmd(cmd, "UPDATE po_lastrun SET lastrun = '', npoints = 0, nlines = 0, npolygons = 0, nlabels = 0;", "0110");
                            }
                            var Names = ExecutionArgs.GetManifestResourceNames(); 
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx("postprocessing.d.0_ableitungsregeln.sql", databaseSRID.ToString()), "0120");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx3("postprocessing.d._1_ableitungsregeln.11001.sql", ":alkis_fnbruch", "true"), "0130");
                            // Fehler suchen
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx("postprocessing.d._1_ableitungsregeln.11002a.sql", databaseSRID.ToString()), "0140");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx("postprocessing.d._1_ableitungsregeln.11002b.sql", databaseSRID.ToString()), "0150");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx("postprocessing.d._1_ableitungsregeln.11002c.sql", databaseSRID.ToString()), "0160");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx("postprocessing.d._1_ableitungsregeln.11002d.sql", databaseSRID.ToString()), "0170");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.11003.sql"), "0180");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.12001.g.sql"), "0190");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.12002.sql"), "0200");
                            try
                            {
                                cmd.CommandText = "Drop table pna";
                                cmd.ExecuteNonQuery();
                            }
                            catch (Exception e)
                            {
                            }

                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.12003.sql"), "0210");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.31001.sql"), "0220");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.31002.sql"), "0230");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41001.sql"), "0240");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41002.sql"), "0250");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41003.sql"), "0260");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41004.sql"), "0270");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41005.sql"), "0280");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41006.sql"), "0290");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41007.sql"), "0300");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41008.sql"), "0310");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.41009.sql"), "0320");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.42001.sql"), "0330");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.42006.sql"), "0340");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.42009.sql"), "0350");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.42010.sql"), "0360");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.42015.sql"), "0370");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.42016.sql"), "0380");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43001.sql"), "0390");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43002.sql"), "0400");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43003.sql"), "0410");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43004.sql"), "0420");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43005.sql"), "0430");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43006.sql"), "0440");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.43007.sql"), "0450");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.44001.sql"), "0460");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.44005.sql"), "0470");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.44006.sql"), "0480");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.44007.sql"), "0490");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51002.sql"), "0500");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51003.sql"), "0510");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51004.sql"), "0520");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51005.sql"), "0530");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51006.sql"), "0540");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51007.sql"), "0550");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51008.sql"), "0560");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51009.sql"), "0570");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.51010.sql"), "0580");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53001.sql"), "0590");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53002.sql"), "0600");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53003.sql"), "0610");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53004.sql"), "0620");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53005.sql"), "0630");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53007.sql"), "0640");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53008.sql"), "0650");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.53009.sql"), "0660");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.54001.sql"), "0670");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.55001.sql"), "0680");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.55002.sql"), "0690");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.55006.sql"), "0700");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.57001.sql"), "0710");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.57002.sql"), "0720");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61001.sql"), "0730");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61003.sql"), "0740");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61005.sql"), "0750");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61006.sql"), "0760");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61007.sql"), "0770");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61008.sql"), "0780");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.61009.sql"), "0790");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.62040.sql"), "0800");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71001.sql"), "0810");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71003.sql"), "0820");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71006.sql"), "0830");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71007.sql"), "0840");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71008.sql"), "0850");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71009.sql"), "0860");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.71011.sql"), "0870");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.72004.sql"), "0880");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.74005.sql"), "0890");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.91001.sql"), "0900");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.bw.sql"), "0910");
                            string cmdtext = ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d._1_ableitungsregeln.gebiete.sql");
                            cmdtext = cmdtext
                                .Replace(":flur_buffer", "0.06")
                                .Replace(":flur_simplify", "0.5")
                                .Replace(":gemarkung_simplify", "2.2")
                                .Replace(":gemeinde_simplify", "5.0")
                                .Replace(":kreis_simplify", "7.0");
                            RunCmd(cmd, cmdtext, "0920");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d.2_ableitungsregeln.sql"), "0930");
                            RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d.4_postnas-keytables.sql"), "0940");
                            /*
                                SELECT string_agg(column_name, ', ')
                                FROM information_schema.columns 
                                WHERE table_name = 'ax_historischesflurstueck' 
                                AND table_schema = 'public'
                                AND column_name NOT IN ('ungewünschte_spalte1', 'ungewünschte_spalte2');
                            */
                            /// AX_flurstueck => AX_flurstueck_f
                            cmdtext =
                                """
                                   truncate table ax_flurstueck_f;
                                   insert into ax_flurstueck_f(
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zustaendigestelle_land, zustaendigestelle_stelle, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, istgebucht, zeigtauf, weistauf, beziehtsichaufflurstueck, gehoertanteiligzu, wkb_geometry
                                    )
                                   select 
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zustaendigestelle_land, zustaendigestelle_stelle, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, istgebucht, zeigtauf, weistauf, beziehtsichaufflurstueck, gehoertanteiligzu, wkb_geometry
                                   from ax_flurstueck
                                   where wkb_geometry is not null;
                                """;
                            RunCmd(cmd, cmdtext, "0950");
                            /// AX_flurstueck => AX_flurstueck_o
                            cmdtext =
                                """
                                   truncate table ax_flurstueck_o;
                                   insert into ax_flurstueck_o(
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zustaendigestelle_land, zustaendigestelle_stelle, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, istgebucht, zeigtauf, weistauf, beziehtsichaufflurstueck, gehoertanteiligzu, objektkoordinaten
                                    )
                                   select 
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zustaendigestelle_land, zustaendigestelle_stelle, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, istgebucht, zeigtauf, weistauf, beziehtsichaufflurstueck, gehoertanteiligzu, objektkoordinaten
                                   from ax_flurstueck
                                   where objektkoordinaten is not null
                                """;
                            RunCmd(cmd, cmdtext, "0960");
                            /// AX_historischesflurstueck => AX_historischesflurstueck_f
                            cmdtext =
                                """
                                   truncate table AX_historischesflurstueck_f;
                                   insert into AX_historischesflurstueck_f(
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, blattart, buchungsart, buchungsblattbezirk_bezirk, buchungsblattbezirk_land, buchungsblattkennzeichen, buchungsblattnummermitbuchstabenerweiterung, laufendenummerderbuchungsstelle, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, nachfolgerflurstueckskennzeichen, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zeitpunktderhistorisierung, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, wkb_geometry
                                   )
                                   select 
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, blattart, buchungsart, buchungsblattbezirk_bezirk, buchungsblattbezirk_land, buchungsblattkennzeichen, buchungsblattnummermitbuchstabenerweiterung, laufendenummerderbuchungsstelle, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, nachfolgerflurstueckskennzeichen, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zeitpunktderhistorisierung, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, wkb_geometry
                                   from AX_historischesflurstueck
                                   where wkb_geometry is not null
                                """;
                            RunCmd(cmd, cmdtext, "0970");
                            /// AX_historischesflurstueck => AX_historischesflurstueck_o
                            cmdtext =
                                """
                                   truncate table AX_historischesflurstueck_o;
                                   insert into AX_historischesflurstueck_o(
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, blattart, buchungsart, buchungsblattbezirk_bezirk, buchungsblattbezirk_land, buchungsblattkennzeichen, buchungsblattnummermitbuchstabenerweiterung, laufendenummerderbuchungsstelle, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, nachfolgerflurstueckskennzeichen, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zeitpunktderhistorisierung, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, objektkoordinaten
                                   )
                                   select 
                                    ogc_fid, gml_id, anlass, beginnt, endet, advstandardmodell, sonstigesmodell, quellobjektid, zeigtaufexternes_art, zeigtaufexternes_name, zeigtaufexternes_uri, abweichenderrechtszustand, amtlicheflaeche, blattart, buchungsart, buchungsblattbezirk_bezirk, buchungsblattbezirk_land, buchungsblattkennzeichen, buchungsblattnummermitbuchstabenerweiterung, laufendenummerderbuchungsstelle, flurnummer, flurstuecksfolge, flurstueckskennzeichen, nenner, zaehler, gemarkungsnummer, land, gemeindezugehoerigkeit_gemeinde, gemeindezugehoerigkeit_gemeindeteil, gemeindezugehoerigkeit_kreis, gemeindezugehoerigkeit_land, gemeindezugehoerigkeit_regierungsbezirk, nachfolgerflurstueckskennzeichen, rechtsbehelfsverfahren, angabenzumabschnittbemerkung, angabenzumabschnittflurstueck, angabenzumabschnittnummeraktenzeichen, angabenzumabschnittstelle, flaechedesabschnitts, kennungschluessel, zeitpunktderentstehung, zeitpunktderhistorisierung, zweifelhafterflurstuecksnachweis, hatdirektunten, istabgeleitetaus, traegtbeizu, istteilvon, objektkoordinaten
                                   from AX_historischesflurstueck
                                   where objektkoordinaten is not null
                                """;
                            RunCmd(cmd, cmdtext, "0980");
                            /// 
                            string cmdtext2 = "delete from po_labels_point";
                            RunCmd(cmd, cmdtext2, "0990");
                            cmdtext2 = "delete from M_PO_LABELS_POINT";
                            RunCmd(cmd, cmdtext2, "1000");
                            cmdtext2 = "insert into po_labels_point(ogc_fid,gml_id,gml_ids,thema ,layer ,signaturnummer ,text ,drehwinkel ,drehwinkel_grad,fontsperrung ,skalierung ,horizontaleausrichtung ,vertikaleausrichtung ,modell , point) " +
                                "                            select ogc_fid,gml_id,gml_ids,thema ,layer ,signaturnummer ,text ,drehwinkel ,drehwinkel_grad,fontsperrung ,skalierung ,horizontaleausrichtung ,vertikaleausrichtung ,modell , point from po_labels where point is not null";
                            RunCmd(cmd, cmdtext2, "1010");
                            cmdtext2 = "insert into M_PO_LABELS_POINT   (fid,  modell,  thema ,layer ,signaturnummer ,label_text ,drehwinkel ,drehwinkel_grad,fontsperrung ,skalierung ,horizontal_alignment ,vertical_alignment , geom) " +
                                                                 " select ogc_fid, LEFT(array_to_string(modell, ','), 255) , thema ,layer ,signaturnummer ,text       ,drehwinkel ,drehwinkel_grad,fontsperrung ,skalierung ," +
                                                                    " CASE horizontaleausrichtung        " +
                                                                    "    WHEN 'linksbündig' THEN 'Left'  " +
                                                                    "    WHEN 'rechtsbündig' THEN 'Right'" +
                                                                    "    WHEN 'zentrisch' THEN 'Center'    " +
                                                                    "    ELSE 'Center'                         " +
                                                                    "END,                                  " +
                                                                    "CASE vertikaleausrichtung             " +
                                                                    "    WHEN 'Basis' THEN 'Bottom'        " +
                                                                    "    WHEN 'Mitte' THEN 'Halfline'      " +
                                                                    "    WHEN 'oben' THEN 'Top'            " +
                                                                    "    ELSE 'Halfline'                   " +
                                                                    "END,                                   " +
                                                                 " point from po_labels_point where " + modell_clause;
                            RunCmd(cmd, cmdtext2, "1020");
                            cmdtext2 = "update M_PO_LABELS_POINT set groesze = " + FST_NUM_GROESZE + ", skalierung=" + FST_NUM_SKALIERUNG + "  WHERE ((signaturnummer)::text = ANY (ARRAY[('4111'::character varying)::text, ('4112'::character varying)::text, ('4113'::character varying)::text, ('4115'::character varying)::text, ('4122'::character varying)::text, ('4123'::character varying)::text]))";
                            RunCmd(cmd, cmdtext2, "1030");
                            // M_PO_LINES
                            cmdtext2 = "delete from M_PO_LINES";
                            RunCmd(cmd, cmdtext2, "1040");
                            cmdtext2 = "insert into M_PO_LINES(fid, modell, thema ,layer ,signaturnummer, skalierung , geom) " +
                                "                            select  ogc_fid, LEFT(array_to_string(modell, ','), 255),thema ,layer ,signaturnummer , " + FST_NUM_SKALIERUNG + ",  line from po_lines where line is not null and " + modell_clause;
                            RunCmd(cmd, cmdtext2, "1050");
                            // M_PO_POLYGONS
                            cmdtext2 = "delete from M_PO_POLYGONS";
                            RunCmd(cmd, cmdtext2, "1060");
                            cmdtext2 = "insert into M_PO_POLYGONS(fid,  modell, thema ,layer ,signaturnummer ,sn_flaeche, sn_randlinie, geom) " +
                                "                            select ogc_fid, LEFT(array_to_string(modell, ','), 255),thema ,layer ,signaturnummer,sn_flaeche, sn_randlinie , polygon from po_polygons where polygon is not null";
                            RunCmd(cmd, cmdtext2, "1070");
                            cmdtext2 = "delete from M_PO_POINTS";
                            RunCmd(cmd, cmdtext2, "1080");
                            /*
                             * Doppeltes Kreuzprodukt:
                             * CROSS JOIN LATERAL unnest(t.modell) AS modell_val => Array von Modellwerten expandieren
                             * CROSS JOIN LATERAL ST_Dump(ST_Multi(t.point)) AS dp => MultiPoint in einzelne Punkte zerlegen (ST_DUMP), vorher müssen eventuell vorhandene 
                             * Points in MultiPoints umgewandelt werden (ST_MULTI).
                             * Lateral ist jeweils nötig, um auf t zuzugreifen.
                             * Da t.modell nicht null sein darf, ist die Verwendung von CROSS JOIN LATERAL hier sicher. (Bei null entfiele die Zeile.)
                             */
                            cmdtext2 = $$"""
                                        insert into M_PO_POINTS(
                                            ogc_fid, 
                                            gml_id, 
                                            thema, 
                                            layer, 
                                            signaturnummer, 
                                            drehwinkel, 
                                            modell, 
                                            drehwinkel_grad, 
                                            orientation,
                                            geom
                                        )
                                        select 
                                        	t.ogc_fid, 
                                        	t.gml_id,
                                        	t.thema,
                                        	t.layer,
                                        	signaturnummer,
                                        	t.drehwinkel, 
                                        	LEFT(array_to_string(modell, ','), 255)/*modell_val*/,
                                        	t.drehwinkel_grad,
                                            round((coalesce(t.drehwinkel, 0)*200/Pi())::numeric,2) as orientation,
                                        	(dp).geom
                                        	from po_points as t
                                        /*CROSS JOIN LATERAL unnest(t.modell) AS modell_val*/
                                        CROSS JOIN LATERAL ST_Dump(ST_Multi(t.point)) AS dp
                                        where {{modell_clause}}
                                        """;
                            RunCmd(cmd, cmdtext2, "1090");
                            //////////
                            ///
                            cmdtext2 = "delete from M_BESONDEREGEBAEUDELINIE";
                            RunCmd(cmd, cmdtext2, "1100");
                            cmdtext2 = """
                                        insert into M_BESONDEREGEBAEUDELINIE(
                                            ogc_fid, 
                                            gml_id, 
                                            beschaffenheit,
                                            beschreibung_beschaffenheit,
                                            geom
                                        )
                                        SELECT bgl.ogc_fid, bgl.gml_id, bbgb.wert, bbgb.beschreibung, st_curvetoline(bgl.wkb_geometry) FROM ax_besonderegebaeudelinie bgl
                                        CROSS JOIN LATERAL unnest(coalesce(bgl.beschaffenheit,Array[9999])) AS besch
                                        join  ax_beschaffenheit_besonderegebaeudelinie as bbgb on besch=bbgb.wert
                                        where bgl.endet is null
                                        """;
                            RunCmd(cmd, cmdtext2, "1110");
                            cmdtext2 = "update M_BESONDEREGEBAEUDELINIE set length=ST_Length(geom)";
                            RunCmd(cmd, cmdtext2, "1120");
                        }

                        //////////////////////////////////////
                        /// M-Tabellen
                        /////////////////////////////////////
                        _additionalPostRunExtension?.Execute(cmd, RunCmd, RunScalar); // optionale Erweiterung, siehe IPostRunExtension
                        //////////////////////////////////////
                        ///
                        //RunCmd(cmd, ExecutionArgs.readForkFileEx2("postprocessing.d.6_nohist.sql"));
                        RunCmd(cmd, ExecutionArgs.readEmbeddedResourceEx2("postprocessing.d.99_vacuum.sql"), "9990");
                    }
                }
                this._logger.LogDebug(".");
                this._logger.LogDebug("DatabaseManager.PostRun done.");
                this._logger.LogDebug(".");
                return executeResult;
            }
            catch (Exception)
            {
                this._logger.LogDebug(".");
                this._logger.LogDebug("DatabaseManager.PostRun exception caught - see log above!");
                this._logger.LogDebug(".");
                throw;
            }
            finally
            {
                // Signalisiert dem Controller (Log-Tailing), dass die Fortschrittsanzeige
                // für PostRun wieder ausgeblendet werden kann — unabhängig davon ob
                // PostRun erfolgreich durchlief oder mit einer Exception abbrach.
                this._logger.LogInformation("[PostRunProgress] DONE");
            }
        }// PostRun


    }
}
