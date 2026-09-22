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
using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using static MapEdit.Alkis.Services.PostNasExecuterWindows.InsertAndReplaceErrorCollector;

namespace MapEdit.Alkis.Services
{

    public class PostNasExecuterWindows : PostNasExecuter
    {
        private void ParseGdalOutput(string output)
        {
            var featureCounts = new Dictionary<string, int>();
            var regex = new Regex(@"GDALVectorTranslate: (\d+) features written in layer '(\w+)'");

            foreach (Match match in regex.Matches(output))
            {
                var featureCount = int.Parse(match.Groups[1].Value);
                var layerName = match.Groups[2].Value;
                featureCounts[layerName] = featureCount;
            }

            foreach (var feature in featureCounts)
            {
                Console.WriteLine($"Layer: {feature.Key}, Features Written: {feature.Value}");
            }
        }

        public PostNasExecuterWindows(
            IConfiguration configuration, ILogger<IPostNasExecuter> logger,
            ExecutionArgs args, IDirectoryScanner directoryScanner, IDatabaseManager databaseManager) : base(configuration, logger, args, directoryScanner, databaseManager)
        {
        }

        protected override NumbersAndInsertFailures RunOgr2gr(string filename, string output) => RunOgr2grPG(filename, output);


        private NumbersAndInsertFailures RunOgr2grPG(string filename, string output)
        {
            /*
             https://gdal.org/en/stable/drivers/vector/pg.html#config-OGR_PG_SKIP_CONFLICTS
             Updating existing tables
             When data is appended to an existing table (for example, using the -append option in ogr2ogr) 
             the driver will, by default, emit an INSERT statement for each row of data to be added. 
             This may be significantly slower than the COPY-based approach taken when creating a new table,
             but ensures consistency of unique identifiers if multiple connections are accessing the table 
             simultaneously.
             If only one connection is accessing the table when data is appended, 
             the COPY-based approach can be chosen by setting the config option PG_USE_COPY to YES, 
             which may significantly speed up the operation.

             OGR_PG_RETRIEVE_FID=[YES​/​NO]: Defaults to YES. If set to "YES" (the default), 
              writing an OGRFeature will cause its FID to be set to the value assigned by the database. 
              If a single feature is to be inserted multiple times, this option can be set to "NO" 
              to allow the database to assign a new FID for each insertion.

             OGR_PG_SKIP_CONFLICTS=[YES​/​NO]: (GDAL >= 3.10) Defaults to NO. 
              If set to "YES" (not the default), conflicts when inserting features will be skipped 
              (requires OGR_PG_RETRIEVE_FID to be off and only applies when PG_USE_COPY is off).



             */
            Dictionary<string, AlkisNumbers> ao = new Dictionary<string, AlkisNumbers>();
            string logfilename = LogFileName(filename);
            FileInfo fi = new FileInfo(logfilename);
            StreamWriter sw = new StreamWriter(logfilename, fi.Exists);
            Action<string?> SWWL = (s1) =>
            {
                if (s1 != null)
                    sw.WriteLine(s1);
            };
            SWWL("");
            SWWL(filename);
            SWWL("-----" + string.Format("{0:dd-MMM-yyyy hh:mm:ss tt}", DateTime.Now));
            using System.Diagnostics.Process ogr = new System.Diagnostics.Process();
            ogr.StartInfo.FileName = _args.Ogr2Ogr;
            //ogr2ogr -f "PostgreSQL" PG:"dbname=%database% user=%username% host=%server%  port=%port% password=mum" -a_srs EPSG:25832 jahnstrasse.xml
            string args = "-append -update -progress -skipfailures ";
            //args += " --config PG_USE_COPY NO";
            //args += " --config OGR_PG_RETRIEVE_FID NO";
            //args += " --config OGR_PG_SKIP_CONFLICTS YES";
            args += " -f \"PostgreSQL\" PG:\"dbname="; //-progress
            args += _args.Database;
            args += " user=";
            args += _args.Username;
            args += " host=";
            args += _args.Host;
            args += " port=";
            args += _args.Port;
            args += " password=";
            args += _args.Password;
            args += "\" -a_srs EPSG:";
            args += this._databaseManager.DatabaseSRID().ToString();
            args += " \"";
            args += filename;
            args += "\"";
            ogr.StartInfo.Arguments = args;
            SWWL("Args: " + args);
            if (OperatingSystem.IsWindows())
            {
                var str = ogr.StartInfo.EnvironmentVariables["PATH"];
                var Dir = Path.GetDirectoryName(_args.Ogr2Ogr);
                ogr.StartInfo.EnvironmentVariables["NAS_GFS_TEMPLATE"] = Dir + "\\alkis-schema.gfs";
                if (!string.IsNullOrEmpty(_args.GDAL_LIBRARY_PATH))
                {
                    ogr.StartInfo.EnvironmentVariables["GDAL_LIBRARY_PATH"] = _args.GDAL_LIBRARY_PATH;
                }
                if (!string.IsNullOrEmpty(_args.GDAL_DATA))
                {
                    ogr.StartInfo.EnvironmentVariables["GDAL_DATA"] = _args.GDAL_DATA;
                }
                if (!string.IsNullOrEmpty(_args.GDAL_DRIVER_PATH))
                {
                    ogr.StartInfo.EnvironmentVariables["GDAL_DRIVER_PATH="] = _args.GDAL_DRIVER_PATH;
                }
                if (!string.IsNullOrEmpty(_args.PROJ_LIB))
                {
                    ogr.StartInfo.EnvironmentVariables["PROJ_LIB"] = _args.PROJ_LIB;
                }
            }
            {
                ogr.StartInfo.EnvironmentVariables["CPL_DEBUG"] = "ON";
                ogr.StartInfo.EnvironmentVariables["CPL_MAX_ERROR_REPORTS"] = "-1";
                // Felder als String interpretieren (d.h. führende Nullen nicht abschneiden)
                ogr.StartInfo.EnvironmentVariables["GML_FIELDTYPES"] = "ALWAYS_STRING";
                // Warnen, wenn numerische Felder mit alphanumerischen Werten gefüllt werden sollen
                ogr.StartInfo.EnvironmentVariables["OGR_SETFIELD_NUMERIC_WARNING"] = "ON";

                // Mindestlänge für Kreisbogensegmente
                ogr.StartInfo.EnvironmentVariables["OGR_ARC_MINLENGTH"] = "0.1";

                // Headerkennungen die NAS-Daten identifizieren
                ogr.StartInfo.EnvironmentVariables["NAS_INDICATOR"] = "NAS-Operationen;AAA-Fachschema;aaa.xsd;aaa-suite;adv/gid/7.1";
                ogr.StartInfo.EnvironmentVariables["PGCLIENTENCODING"] = "UTF8";

                if (_args.PG_USE_COPY.Equals("YES", StringComparison.OrdinalIgnoreCase))
                {
                    ogr.StartInfo.EnvironmentVariables["PG_USE_COPY"] = "YES";
                }
                else
                {
                    ogr.StartInfo.EnvironmentVariables["PG_USE_COPY"] = "NO";
                }
                if (_args.OGR_PG_RETRIEVE_FID.Equals("YES", StringComparison.OrdinalIgnoreCase))
                {
                    ogr.StartInfo.EnvironmentVariables["OGR_PG_RETRIEVE_FID"] = "YES";
                }
                else
                {
                    ogr.StartInfo.EnvironmentVariables["OGR_PG_RETRIEVE_FID"] = "NO";
                }
                if (_args.OGR_PG_SKIP_CONFLICTS.Equals("YES", StringComparison.OrdinalIgnoreCase))
                {
                    ogr.StartInfo.EnvironmentVariables["OGR_PG_SKIP_CONFLICTS"] = "YES";
                }
                else
                {
                    ogr.StartInfo.EnvironmentVariables["OGR_PG_SKIP_CONFLICTS"] = "NO";
                }

                ogr.StartInfo.EnvironmentVariables["NAS_NO_RELATION_LAYER"] = "YES";
                ogr.StartInfo.EnvironmentVariables["NAS_SKIP_CORRUPTED_FEATURES"] = "YES";
                ogr.StartInfo.EnvironmentVariables["TABLES"] = "aa_advstandardmodell,aa_nas_ausgabeform,nas_filter_capabilities,aa_themendimension,aa_art_themendefinition,operation,ap_horizontaleausrichtung,ap_vertikaleausrichtung,ap_dateityp_3d,ax_artdesnullpunktes_nullpunkt,ax_li_processstep_mitdatenerhebung_description,ax_datenerhebung,ax_sportart_bauwerkoderanlagefuersportfreizeitunderholung,ax_lagezurerdoberflaeche_transportanlage,ax_produkt_transportanlage,ax_bauwerksfunktion_turm,ax_hydrologischesmerkmal_sonstigesbauwerkodersonstigeeinri,ax_zustand_turm,ax_art_heilquellegasquelle,ax_bauwerksfunktion_transportanlage,ax_lagezurerdoberflaeche_vorratsbehaelterspeicherbauwerk,ax_speicherinhalt_vorratsbehaelterspeicherbauwerk,ax_bauwerksfunktion_bauwerkoderanlagefuerindustrieundgewer,ax_art_einrichtunginoeffentlichenbereichen,ax_bauwerksfunktion_bauwerkoderanlagefuersportfreizeitunde,ax_archaeologischertyp_historischesbauwerkoderhistorischee,ax_hydrologischesmerkmal_heilquellegasquelle,ax_zustand_bauwerkoderanlagefuerindustrieundgewerbe,ax_bauwerksfunktion_sonstigesbauwerkodersonstigeeinrichtun,ax_funktion_bauwerk,ax_bauwerksfunktion_leitung,ax_bauwerksfunktion_vorratsbehaelterspeicherbauwerk,ax_befestigung_wegpfadsteig,ax_oberflaechenmaterial_flugverkehrsanlage,ax_art_gleis,ax_bahnkategorie_gleis,ax_art_strassenverkehrsanlage,ax_markierung_wegpfadsteig,ax_bahnhofskategorie_bahnverkehrsanlage,ax_bahnkategorie_seilbahnschwebebahn,ax_zustand_bahnverkehrsanlage,ax_zustand_bauwerkimgewaesserbereich,ax_art_wegpfadsteig,ax_lagezuroberflaeche_gleis,ax_art_flugverkehrsanlage,ax_bauwerksfunktion_bauwerkimverkehrsbereich,ax_bauwerksfunktion_bauwerkimgewaesserbereich,ax_art_einrichtungenfuerdenschiffsverkehr,ax_zustand_bauwerkimverkehrsbereich,ax_artdergewaesserachse,ax_art_schifffahrtsliniefaehrverkehr,ax_zustand_schleuse,ax_nutzung_hafen,ax_konstruktionsmerkmalbauart_schleuse,ax_hafenkategorie_hafen,ax_art_gewaessermerkmal,ax_hydrologischesmerkmal_untergeordnetesgewaesser,ax_lagezurerdoberflaeche_untergeordnetesgewaesser,ax_artdespolders,ax_funktion_polder,ax_funktion_untergeordnetesgewaesser,ax_hydrologischesmerkmal_gewaessermerkmal,ax_funktion_vegetationsmerkmal,ax_zustand_vegetationsmerkmal,ax_bewuchs_vegetationsmerkmal,ax_eigentuemerart_namensnummer,ax_li_processstep_ohnedatenerhebung_description,ax_blattart_buchungsblatt,ax_anrede_person,ax_artderrechtsgemeinschaft_namensnummer,ax_buchungsart_buchungsstelle,ax_klassifikation_hierarchiestufe3d_lagefestpunkt,ax_punktstabilitaet,ax_punktstabilitaet_hoehenfestpunkt_geologischestabilitaet,ax_klassifikation_ordnung_lagefestpunkt,ax_punktstabilitaet_hoehenfestpunkt_guetedesvermarkungstra,ax_ordnung_schwerefestpunkt,ax_funktion_referenzstationspunkt,ax_funktion_lagefestpunkt,ax_skizzenart_skizze,ax_funktion_schwerefestpunkt,ax_punktstabilitaet_hoehenfestpunkt_hoehenstabilitaetauswi,ax_punktstabilitaet_hoehenfestpunkt_guetedesbaugrundes,ax_punktstabilitaet_hoehenfestpunkt_grundwasserschwankung,ax_punktstabilitaet_hoehenfestpunkt_topographieundumwelt,ax_klassifikation_wertigkeit_lagefestpunkt,ax_gnsstauglichkeit,ax_punktstabilitaet_hoehenfestpunkt_grundwasserstand,ax_punktstabilitaet_hoehenfestpunkt_vermutetehoehenstabili,ax_ordnung_hoehenfestpunkt,ax_horizontfreiheit_grenzpunkt,ax_gruendederausgesetztenabmarkung_grenzpunkt,ax_bemerkungzurabmarkung_grenzpunkt,ax_artderflurstuecksgrenze_besondereflurstuecksgrenze,ax_horizontfreiheit_netzpunkt,ax_marke,ax_genauigkeitsstufe_punktort,ax_messmethode_schwere,ax_koordinatenstatus_punktort,ax_datenerhebung_schwere,ax_vertrauenswuerdigkeit_schwere,ax_schwereanomalie_schwere_art,ax_vertrauenswuerdigkeit_punktort,ax_schwerestatus_schwere,ax_li_processstep_punktort_description,ax_genauigkeitsstufe_schwere,ax_datenerhebung_punktort,ax_schweresystem_schwere,ax_blattart_historischesflurstueck,ax_qualitaet_hauskoordinate,ax_art_punktkennung,ax_art_reservierung,ax_art_adressat_auszug,ax_lagezurerdoberflaeche_bauteil,ax_lagezurerdoberflaeche_gebaeude,ax_zustand_gebaeude,ax_dachgeschossausbau_gebaeude,ax_dachform,ax_bauweise_gebaeude,ax_gebaeudefunktion,ax_art_gebaeudepunkt,ax_weitere_gebaeudefunktion,ax_beschaffenheit_besonderegebaeudelinie,ax_bauart_bauteil,ax_nutzung,ax_art_verbandsgemeinde,ax_art_baublock,ax_artdergebietsgrenze_gebietsgrenze,ax_sonstigeangaben_bodenschaetzung,ax_kulturart_musterlandesmusterundvergleichsstueck,ax_entstehungsartoderklimastufewasserverhaeltnisse_bodensc,ax_sonstigeangaben_musterlandesmusterundvergleichsstueck,ax_kulturart_bodenschaetzung,ax_klassifizierung_bewertung,ax_merkmal_musterlandesmusterundvergleichsstueck,ax_zustandsstufeoderbodenstufe_bodenschaetzung,ax_bedeutung_grablochderbodenschaetzung,ax_zustandsstufeoderbodenstufe_musterlandesmusterundvergle,ax_entstehungsartoderklimastufewasserverhaeltnisse_musterl,ax_bodenart_bodenschaetzung,ax_bodenart_musterlandesmusterundvergleichsstueck,ax_landschaftstyp,ax_art_verband,ax_behoerde,ax_administrative_funktion,ax_bezeichnung_verwaltungsgemeinschaft,ax_funktion_schutzgebietnachwasserrecht,ax_artderfestlegung_schutzgebietnachnaturumweltoderbodensc,ax_artderfestlegung_anderefestlegungnachstrassenrecht,ax_artderfestlegung_schutzgebietnachwasserrecht,ax_besonderefunktion_forstrecht,ax_zone_schutzzone,ax_artderfestlegung_klassifizierungnachstrassenrecht,ax_artderfestlegung_denkmalschutzrecht,ax_artderfestlegung_klassifizierungnachwasserrecht,ax_rechtszustand_schutzzone,ax_artderfestlegung_bauraumoderbodenordnungsrecht,ax_artderfestlegung_anderefestlegungnachwasserrecht,ax_artderfestlegung_forstrecht,ax_zustand_naturumweltoderbodenschutzrecht,ax_artderfestlegung_sonstigesrecht,ax_artderfestlegung_naturumweltoderbodenschutzrecht,ax_liniendarstellung_topographischelinie,ax_darstellung_gebaeudeausgestaltung,ax_datenformat_benutzer,ax_art_bereichzeitlich,ax_letzteabgabeart,ax_ausgabemedium_benutzer,ax_identifikation,ax_dqerfassungsmethodemarkantergelaendepunkt,ax_dqerfassungsmethodestrukturiertegelaendepunkte,ax_dqerfassungsmethode,ax_besonderebedeutung,ax_dqerfassungsmethodebesondererhoehenpunkt,ax_artdergeripplinie,ax_artdergelaendekante,ax_artderstrukturierung,ax_dqerfassungsmethodegewaesserbegrenzung,ax_artdernichtgelaendepunkte,ax_artdesmarkantengelaendepunktes,ax_artderaussparung,ax_besondereartdergewaesserbegrenzung,ax_ursprung,ax_funktion_dammwalldeich,ax_art_dammwalldeich,ax_funktion_einschnitt,ax_zustand_boeschungkliff,ax_zustand_hoehleneingang,ax_berechnungsmethode,ax_verwendeteobjekte,ax_berechnungsmethodehoehenlinie,ax_dqerfassungsmethodesekundaeresdgm,ax_zustand_kanal,ax_funktion_stehendesgewaesser,ax_schifffahrtskategorie,ax_hydrologischesmerkmal_fliessgewaesser,ax_schifffahrtskategorie_kanal,ax_funktion_fliessgewaesser,ax_widmung_wasserlauf,ax_funktion_meer,ax_hydrologischesmerkmal_gewaesserachse,ax_tidemerkmal_meer,ax_nutzung_hafenbecken,ax_hydrologischesmerkmal_stehendesgewaesser,ax_widmung_stehendesgewaesser,ax_funktion_gewaesserachse,ax_funktion_hafenbecken,ax_widmung_kanal,ax_zustand_wohnbauflaeche,ax_artderbebauung_wohnbauflaeche,ax_zustand_flaechebesondererfunktionalerpraegung,ax_funktion_flaechegemischternutzung,ax_foerdergut_industrieundgewerbeflaeche,ax_artderbebauung_flaechegemischternutzung,ax_zustand_sportfreizeitunderholungsflaeche,ax_funktion_flaechebesondererfunktionalerpraegung,ax_funktion_sportfreizeitunderholungsflaeche,ax_lagergut_industrieundgewerbeflaeche,ax_zustand_halde,ax_zustand_bergbaubetrieb,ax_abbaugut_tagebaugrubesteinbruch,ax_primaerenergie_industrieundgewerbeflaeche,ax_abbaugut_bergbaubetrieb,ax_zustand_flaechegemischternutzung,ax_zustand_industrieundgewerbeflaeche,ax_funktion_friedhof,ax_zustand_friedhof,ax_lagergut_halde,ax_funktion_industrieundgewerbeflaeche,ax_zustand_tagebaugrubesteinbruch,ax_artderbebauung_siedlungsflaeche,ax_artderbebauung_flaechebesondererfunktionalerpraegung,ax_vegetationsmerkmal_gehoelz,ax_vegetationsmerkmal_wald,ax_vegetationsmerkmal_landwirtschaft,ax_oberflaechenmaterial_unlandvegetationsloseflaeche,ax_funktion_unlandvegetationsloseflaeche,ax_funktion_gehoelz,ax_bahnkategorie,ax_funktion_weg,ax_funktion_bahnverkehr,ax_verkehrsbedeutunginneroertlich,ax_internationalebedeutung_strasse,ax_besonderefahrstreifen,ax_zustand_bahnverkehr,ax_befestigung_fahrwegachse,ax_spurweite,ax_zustand_schiffsverkehr,ax_funktion_platz,ax_art_flugverkehr,ax_elektrifizierung,ax_zustand,ax_fahrbahntrennung_strasse,ax_funktion_fahrbahnachse,ax_oberflaechenmaterial_strasse,ax_funktion_flugverkehr,ax_funktion_wegachse,ax_zustand_strasse,ax_markierung_wegachse,ax_zustand_flugverkehr,ax_funktion_strassenachse,ax_verkehrsbedeutungueberoertlich,ax_nutzung_flugverkehr,ax_funktion_schiffsverkehr,ax_funktion_strasse,ax_widmung_strasse,ax_anzahlderstreckengleise,ax_funktionoa_k_tngr_all,ax_klassifizierunggr_k_bewgr,ax_funktionoa_k_tnfl,ax_klassifizierungobg_k_bewfl,ax_funktionoa_k_tngrerweitert_all,ax_funktionhgr_k_tnhgr,ax_wirtschaftsart,ax_punktart_k_punkte,ax_k_zeile_punktart,aa_besonderemeilensteinkategorie,aa_anlassart,aa_levelofdetail,aa_anlassart_benutzungsauftrag,aa_weiteremodellart,aa_instanzenthemen,ax_benutzer,ax_benutzergruppemitzugriffskontrolle,ax_benutzergruppenba,ap_darstellung,aa_projektsteuerung,aa_meilenstein,aa_antrag,aa_aktivitaet,aa_vorgang,ax_person,ax_namensnummer,ax_anschrift,ax_verwaltung,ax_buchungsstelle,ax_personengruppe,ax_buchungsblatt,ax_vertretung,ax_skizze,ax_schwere,ax_historischesflurstueckalb,ax_historischesflurstueckohneraumbezug,ax_lagebezeichnungohnehausnummer,ax_lagebezeichnungmithausnummer,ax_lagebezeichnungmitpseudonummer,ax_reservierung,ax_punktkennunguntergegangen,ax_punktkennungvergleichend,ax_fortfuehrungsnachweisdeckblatt,ax_fortfuehrungsfall,ax_gemeinde,ax_buchungsblattbezirk,ax_gemarkungsteilflur,ax_kreisregion,ax_bundesland,ax_regierungsbezirk,ax_gemeindeteil,ax_lagebezeichnungkatalogeintrag,ax_gemarkung,ax_dienststelle,ax_verband,ax_nationalstaat,ax_besondererbauwerkspunkt,ax_netzknoten,ax_referenzstationspunkt,ax_lagefestpunkt,ax_hoehenfestpunkt,ax_schwerefestpunkt,ax_grenzpunkt,ax_aufnahmepunkt,ax_sonstigervermessungspunkt,ax_sicherungspunkt,ax_besonderergebaeudepunkt,ax_wirtschaftlicheeinheit,ax_verwaltungsgemeinschaft,ax_schutzgebietnachnaturumweltoderbodenschutzrecht,ax_schutzgebietnachwasserrecht,ax_boeschungkliff,ax_besonderertopographischerpunkt,ax_kanal,ax_wasserlauf,ax_strasse,ap_fpo,aa_antragsgebiet,ax_polder,ax_historischesflurstueck,ax_kondominium,ax_baublock,ax_aussparungsflaeche,ax_soll,ax_duene,ax_transportanlage,ax_wegpfadsteig,ax_gleis,ax_bahnverkehrsanlage,ax_strassenverkehrsanlage,ax_einrichtungenfuerdenschiffsverkehr,ax_flugverkehrsanlage,ax_hafen,ax_testgelaende,ax_schleuse,ax_ortslage,ax_grenzuebergang,ax_gewaessermerkmal,ax_untergeordnetesgewaesser,ax_vegetationsmerkmal,ax_musterlandesmusterundvergleichsstueck,ax_insel,ax_gewann,ax_kleinraeumigerlandschaftsteil,ax_landschaft,ax_felsenfelsblockfelsnadel,ap_lto,ax_leitung,ax_abschnitt,ax_ast,ap_lpo,ax_seilbahnschwebebahn,ax_gebaeudeausgestaltung,ax_topographischelinie,ax_geripplinie,ax_gewaesserbegrenzung,ax_strukturierterfasstegelaendepunkte,ax_einschnitt,ax_hoehenlinie,ax_abgeleitetehoehenlinie,ap_pto,ax_heilquellegasquelle,ax_wasserspiegelhoehe,ax_nullpunkt,ax_punktortau,ax_georeferenziertegebaeudeadresse,ax_grablochderbodenschaetzung,ax_wohnplatz,ax_markantergelaendepunkt,ax_besondererhoehenpunkt,ax_hoehleneingang,ap_ppo,ax_sickerstrecke,ax_firstlinie,ax_besonderegebaeudelinie,ax_gelaendekante,ax_sonstigesbauwerkodersonstigeeinrichtung,ax_bauwerkoderanlagefuersportfreizeitunderholung,ax_bauwerkoderanlagefuerindustrieundgewerbe,ax_einrichtunginoeffentlichenbereichen,ax_historischesbauwerkoderhistorischeeinrichtung,ax_turm,ax_vorratsbehaelterspeicherbauwerk,ax_bauwerkimgewaesserbereich,ax_bauwerkimverkehrsbereich,ax_schifffahrtsliniefaehrverkehr,ax_gebaeude,ax_anderefestlegungnachstrassenrecht,ax_naturumweltoderbodenschutzrecht,ax_klassifizierungnachstrassenrecht,ax_sonstigesrecht,ax_denkmalschutzrecht,ax_dammwalldeich,ax_punktortag,ax_bauteil,ax_tagesabschnitt,ax_bewertung,ax_anderefestlegungnachwasserrecht,ax_klassifizierungnachwasserrecht,ax_forstrecht,ax_bauraumoderbodenordnungsrecht,ax_schutzzone,ax_boeschungsflaeche,ax_flurstueck,ax_gebiet_kreis,ax_gebiet_bundesland,ax_gebiet_regierungsbezirk,ax_gebiet_nationalstaat,ax_kommunalesgebiet,ax_gebiet_verwaltungsgemeinschaft,ax_bodenschaetzung,ax_gewaesserstationierungsachse,ax_besondereflurstuecksgrenze,ax_gebietsgrenze,ax_gewaesserachse,ax_strassenachse,ax_bahnstrecke,ax_fahrwegachse,ax_fahrbahnachse,ax_punktortta,ax_stehendesgewaesser,ax_meer,ax_fliessgewaesser,ax_hafenbecken,ax_bergbaubetrieb,ax_friedhof,ax_flaechegemischternutzung,ax_wohnbauflaeche,ax_flaechebesondererfunktionalerpraegung,ax_industrieundgewerbeflaeche,ax_siedlungsflaeche,ax_tagebaugrubesteinbruch,ax_sportfreizeitunderholungsflaeche,ax_halde,ax_flaechezurzeitunbestimmbar,ax_sumpf,ax_unlandvegetationsloseflaeche,ax_gehoelz,ax_wald,ax_heide,ax_moor,ax_landwirtschaft,ax_bahnverkehr,ax_weg,ax_schiffsverkehr,ax_flugverkehr,ax_platz,ax_strassenverkehr,ta_compositesolidcomponent_3d,ta_surfacecomponent_3d,ta_curvecomponent_3d,ta_pointcomponent_3d,au_trianguliertesoberflaechenobjekt_3d,au_mehrfachflaechenobjekt_3d,au_mehrfachlinienobjekt_3d,au_umringobjekt_3d,ap_kpo_3d,au_punkthaufenobjekt_3d,au_koerperobjekt_3d,au_geometrieobjekt_3d,ax_fortfuehrungsauftrag,ks_einrichtunginoeffentlichenbereichen,ks_bauwerkanlagenfuerverundentsorgung,ks_sonstigesbauwerk,ks_verkehrszeichen,ks_bauwerkimgewaesserbereich,ks_vegetationsmerkmal,ks_bauraumoderbodenordnungsrecht,ks_kommunalerbesitz";
                ogr.StartInfo.EnvironmentVariables["LIST_ALL_TABLES"] = "YES";
            }
            ogr.StartInfo.UseShellExecute = false;
            ogr.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
            ogr.StartInfo.RedirectStandardOutput = true;
            bool useOutPutDataRecieved = false;
            ogr.StartInfo.RedirectStandardError = true;
            ogr.EnableRaisingEvents = true;
            InsertAndReplaceErrorCollector rec = new InsertAndReplaceErrorCollector(SWWL, true);
            var NASregex = new Regex(@"NAS: (\w+): (\d+) features\."); 
            var GdalVectorregex = new Regex(@"GDALVectorTranslate: (\d+) features written in layer '(\w+)'");

            if (ogr.StartInfo.RedirectStandardOutput && useOutPutDataRecieved)
            {
                ogr.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        //SWWL("out: " + e.Data, null);
                        Console.WriteLine(e.Data);
                    }
                });
            }
            if (ogr.StartInfo.RedirectStandardError)
            {
                ogr.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        if(rec.Match(e.Data))
                        {
                            return;
                        }
                        // Look for NAS: Klasse: x features.
                        string a = new StringBuilder(e.Data).ToString();
                        foreach (Match match in NASregex.Matches(a))
                        {
                            var featureName = match.Groups[1].Value.ToLower();
                            var featureCount = int.Parse(match.Groups[2].Value);
                            if (!ao.ContainsKey(featureName))
                            {
                                ao.Add(featureName, new AlkisNumbers { Read = featureCount });
                            }
                            else
                            {
                                ao[featureName].Read = featureCount;
                            }
                            // return; let it write,too
                        }
                        // Look for GDALVectorTranslate: x features written in layer 'Klasse'
                        string b = new StringBuilder(e.Data).ToString();
                        foreach (Match match in GdalVectorregex.Matches(b))
                        {
                            var featureCount = int.Parse(match.Groups[1].Value);
                            var featureName = match.Groups[2].Value.ToLower();
                            if (!ao.ContainsKey(featureName))
                            {
                                ao.Add(featureName, new AlkisNumbers { Written = featureCount });
                            }
                            else
                            {
                                ao[featureName].Written = featureCount;
                            }
                            // return; let it write,too
                        }


                        // GDAL:
                        {
                            string sk = "GDAL:";
                            if (e.Data.Length >= sk.Length && e.Data[..sk.Length].Equals(sk))
                                return;
                        }
                        // PG:
                        {
                            string sk = "PG: Primary key name (FID)";
                            if (e.Data.Length >= sk.Length && e.Data[..sk.Length].Equals(sk))
                                return;
                            sk = "PG: Using column 'ogc_fid' as FID";
                            if (e.Data.Length >= sk.Length && e.Data[..sk.Length].Equals(sk))
                                return;
                        }
                        // NAS:
                        {
                            string sk = "NAS: Skipping empty layer";
                            if (e.Data.Length >= sk.Length && e.Data[..sk.Length].Equals(sk))
                                return;
                        }
                        SWWL(e.Data);
                    }
                });
            }
            ogr.Start();
            if (ogr.StartInfo.RedirectStandardOutput)
            {
                if (useOutPutDataRecieved)
                {
                    ogr.BeginOutputReadLine();
                }
                else
                {
                    Task.Run(() =>
                    {
                        // Optional: stdout in Datei spiegeln damit Controller live mitlesen kann
                        StreamWriter? stdoutLog = null;
                        if (!string.IsNullOrEmpty(_args.StdoutLogPath))
                        {
                            try
                            {
                                var dir = System.IO.Path.GetDirectoryName(_args.StdoutLogPath);
                                if (!string.IsNullOrEmpty(dir))
                                    System.IO.Directory.CreateDirectory(dir);
                                stdoutLog = new StreamWriter(_args.StdoutLogPath, append: true)
                                {
                                    AutoFlush = true
                                };
                            }
                            catch { /* Log-Datei nicht kritisch */ }
                        }
                        try
                        {
                            if (!string.IsNullOrEmpty(output) && output.Length > 0)
                            {
                                var prefix = output + ": ";
                                Console.Write(prefix);
                                stdoutLog?.Write(prefix);
                            }
                            var reader = ogr.StandardOutput;
                            int c;
                            while ((c = reader.Read()) != -1) // Zeichenweise lesen
                            {
                                Console.Write((char)c);
                                Console.Out.Flush();
                                stdoutLog?.Write((char)c);
                            }
                        }
                        finally
                        {
                            stdoutLog?.Dispose();
                        }
                    });
                }
            }
            if (ogr.StartInfo.RedirectStandardError)
                ogr.BeginErrorReadLine();
            ogr.WaitForExit();
            sw.Flush();
            sw.Close();
            ogr.Close();
            return new NumbersAndInsertFailures { Numbers = ao, InsertFailures = new InsertFailureList { Failures = rec.ErrorItems } };
        }

        protected override void BeforeExecute()
        {
            if (OperatingSystem.IsWindows())
            {
                if (!string.IsNullOrEmpty(_args.Ogr2Ogr))
                {
                    string? Dir = Path.GetDirectoryName(_args.Ogr2Ogr);
                    if (!string.IsNullOrEmpty(Dir))
                    {
                        Directory.SetCurrentDirectory(Dir);
                        var Schema = ExecutionArgs.readEmbeddedResource("alkis-schema.gfs");
                        System.IO.File.WriteAllText(Dir + "\\alkis-schema.gfs", Schema);
                    }
                }
            }
        }
    }
}