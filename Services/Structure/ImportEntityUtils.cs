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
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Xml;
using Serilog;

namespace MapEdit.Alkis.Services.Structure
{
    public partial class ImportEntity : IEquatable<ImportEntity>, IComparable<ImportEntity>
    {
        // Wurzelelemente, die eine Datei als NAS-Datei ausweisen (Art der Abgabe).
        private const string RootElementNba = "AX_NutzerbezogeneBestandsdatenaktualisierung_NBA";
        private const string RootElementBda = "AX_Bestandsdatenauszug";

        // "Art"-Werte, siehe auch ImportEntity.Art (Default "BDA").
        private const string ArtNba = "NBA";
        private const string ArtBda = "BDA";

        // XML-Elementnamen innerhalb von NBA/BDA-Dateien.
        private const string ElementGeaenderteObjekte = "geaenderteObjekte";
        private const string ElementTransaction = "Transaction";
        private const string ElementInsert = "Insert";
        private const string ElementReplace = "Replace";
        private const string ElementDelete = "Delete";
        private const string ElementUpdate = "Update";
        private const string ElementFilter = "Filter";
        private const string ElementAntragsnummer = "antragsnummer";
        private const string ElementProfilkennung = "profilkennung";
        private const string ElementPortionskennung = "portionskennung";
        private const string ElementDatum = "datum";
        private const string ElementLaufendeNummerVonGesamtzahl = "laufendeNummerVonGesamtzahl";
        private const string ElementGesamtzahl = "gesamtzahl";
        private const string ElementSuedwestEcke = "suedwestEcke";
        private const string ElementAuftragsnummer = "auftragsnummer";
        private const string ElementEnthaelt = "enthaelt";
        private const string ElementFeatureCollection = "FeatureCollection";
        private const string ElementFeatureMember = "featureMember";
        private const string ElementMember = "member";

        // SRID-Erkennung ueber das "srsName"-Attribut.
        private const string AttributeSrsName = "srsName";
        private const string CrsUrnUtm32 = "urn:adv:crs:ETRS89_UTM32";
        private const string CrsUrnUtm33 = "urn:adv:crs:ETRS89_UTM33";
        private const string CrsUrnUtm31 = "urn:adv:crs:ETRS89_UTM31";
        private const int SridUtm32 = 25832;
        private const int SridUtm33 = 25833;
        private const int SridUtm31 = 25831;

        /// <summary>
        /// Sammelt die Werte, die waehrend des Parsens von ReadStream/ParseNbaSection/
        /// ParseBdaSection anfallen, bevor sie am Ende auf die ImportEntity-Instanz
        /// uebertragen werden. Ersetzt die vorher direkt in ReadStream deklarierten
        /// lokalen Variablen, damit ParseNbaSection/ParseBdaSection sie gemeinsam
        /// nutzen koennen.
        /// </summary>
        private sealed class ReadState
        {
            public String Auftragsnummer = "k.A.";
            public String Antragsnummer = "k.A.";
            public String Profilkennung = string.Empty;
            public String Datum = string.Empty;
            public String LaufendeNummerVonGesamtzahl = string.Empty;
            public String Gesamtzahl = string.Empty;
            public String SuedwestEcke = string.Empty;
            public Int32 Inserts = 0;
            public Int32 Replaces = 0;
            public Int32 Deletes = 0;
            public Int32 Updates = 0;
        }

        /// <summary>
        /// Untersucht die Datei (XML oder GZip, je nach <see cref="TypeOfFile"/>) und
        /// befuellt diese Instanz, falls es sich um eine erkannte NAS-Datei handelt
        /// (Wurzelelement <see cref="RootElementNba"/> oder <see cref="RootElementBda"/>).
        /// Liefert <c>false</c> bei Fremddateien - der Aufrufer entscheidet dann, ob
        /// die Datei ignoriert wird (siehe DirectoryScanner.Scan).
        /// </summary>
        internal Boolean ReadFile(bool RequireProfilkennung)
        {
            Boolean IsNasFile = false;
            switch (this.TypeOfFile)
            {
                case EntityType.XML:
                    IsNasFile = ReadXML(RequireProfilkennung);
                    break;
                case EntityType.GZIP:
                    IsNasFile = ReadGZip(RequireProfilkennung);
                    break;
                default:
                    Log.Error("ImportEntity.ReadFile: Unknown file type:" + this.fqfn);
                    break;
            }
            return IsNasFile;
        }
        protected Boolean ReadGZip(bool RequireProfilkennung)
        {
            Boolean IsNasFile = false;
            try
            {
                using FileStream fs = new FileStream(this.fqfn, FileMode.Open, FileAccess.Read);
                using var gZipStream = new GZipStream(fs, CompressionMode.Decompress);
                Log.Information("Examining file: " + this.AlkisName);
                IsNasFile = ReadStream(gZipStream, RequireProfilkennung);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
            return IsNasFile;
        }
        protected Boolean ReadXML(bool RequireProfilkennung)
        {
            Boolean IsNasFile = false;
            try
            {
                using FileStream fs = new FileStream(this.fqfn, FileMode.Open, FileAccess.Read);
                Log.Information("Examining file: " + this.BaseName);
                IsNasFile = ReadStream(fs, RequireProfilkennung);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
            }
            return IsNasFile;
        }
        private void ReadElements(String currentTag, ref Int32 ops, System.Xml.XmlTextReader rd)
        {
            // Hier "stehen wir auf "Insert"
            // nach der Funktion stehen wir auf dem Endelement
            int InsertDepth = rd.Depth; // tiefe der Insert- Anweisung
            int ElDepth = InsertDepth + 1;
            bool gelesen = rd.Read();
            // Nach Skip darf kein Read folgen, da skip bereits auf das Element NACH diesem positioniert
            while (gelesen)
            {
                gelesen = false;
                if (XmlNodeType.Element == rd.NodeType)
                {

                    if (ElDepth == rd.Depth)
                    {
                        if (ElementFilter != rd.LocalName) // bei Replace
                            ops++;
                        if (this.Srid > -1)
                        {
                            rd.Skip();
                            gelesen = true;
                        }
                    }
                    if (Srid < 0 && rd.HasAttributes)
                    {
                        if (rd.MoveToAttribute(AttributeSrsName))
                        {
                            String srsName = rd.Value;
                            if (!String.IsNullOrEmpty(srsName))
                            {
                                if (0 == String.Compare(srsName, CrsUrnUtm32))
                                { // am häufigsten?
                                    this.Srid = SridUtm32;
                                }
                                else if (0 == String.Compare(srsName, CrsUrnUtm33))
                                {
                                    this.Srid = SridUtm33;
                                }
                                else if (0 == String.Compare(srsName, CrsUrnUtm31))
                                {
                                    this.Srid = SridUtm31;
                                }
                            }
                        }
                    }
                }
                else if (XmlNodeType.EndElement == rd.NodeType && InsertDepth == rd.Depth)
                {
                    if (currentTag == rd.LocalName)
                    {
                        return;
                    }
                }
                if (!gelesen) gelesen = rd.Read();
            }
        }

        /// <summary>
        /// Liest den Inhalt einer NBA-Datei (AX_NutzerbezogeneBestandsdatenaktualisierung_NBA)
        /// ab dem bereits konsumierten Wurzelelement bis zum Ende des Streams und
        /// befuellt <paramref name="state"/>. Setzt Art/IsBDA auf dieser Instanz.
        /// </summary>
        private void ParseNbaSection(System.Xml.XmlTextReader rd, ReadState state)
        {
            this.Art = ArtNba;
            this.IsBDA = false;
            while (rd.Read())
            {
                if (XmlNodeType.Element == rd.NodeType)
                {
                    if (ElementGeaenderteObjekte == rd.LocalName)
                    {
                        while (rd.Read())
                        {
                            if (XmlNodeType.Element == rd.NodeType)
                            {
                                if (ElementTransaction == rd.LocalName)
                                {
                                    if (!rd.IsEmptyElement)
                                    { // Dann gibts kein EndElement !!!!
                                        while (rd.Read())
                                        {
                                            if (XmlNodeType.Element == rd.NodeType)
                                            {
                                                if (ElementInsert == rd.LocalName)
                                                {
                                                    ReadElements(ElementInsert, ref state.Inserts, rd);
                                                }
                                                else if (ElementReplace == rd.LocalName)
                                                {
                                                    ReadElements(ElementReplace, ref state.Replaces, rd);
                                                }
                                                else if (ElementDelete == rd.LocalName)
                                                {
                                                    // Suchen hier nicht nach SRID
                                                    state.Deletes++;
                                                }
                                                else if (ElementUpdate == rd.LocalName)
                                                {
                                                    state.Updates++;
                                                }
                                            }
                                            else if (XmlNodeType.EndElement == rd.NodeType)
                                            {
                                                if (ElementTransaction == rd.LocalName)
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (XmlNodeType.EndElement == rd.NodeType)
                            {
                                if (ElementGeaenderteObjekte == rd.LocalName)
                                    break;
                            }
                        }
                    }
                    else if (ElementAntragsnummer == rd.LocalName)
                    {
                        state.Antragsnummer = rd.ReadElementString();
                        if (!String.IsNullOrEmpty(state.Antragsnummer))
                        {
                            state.Antragsnummer = state.Antragsnummer.Trim();
                        }
                    }
                    else if (ElementProfilkennung == rd.LocalName)
                    {
                        state.Profilkennung = rd.ReadElementString();
                        if (!String.IsNullOrEmpty(state.Profilkennung))
                        {
                            state.Profilkennung = state.Profilkennung.Trim();
                        }
                    }

                }
                for (long i = 0; i < 2; ++i)
                {//Dadurch ist die Reihenfolge egal.
                    if (XmlNodeType.Element == rd.NodeType)
                    {
                        if (ElementPortionskennung == rd.LocalName)
                        {
                            while (rd.Read())
                            {
                                if (XmlNodeType.Element == rd.NodeType)
                                {
                                    if (ElementProfilkennung == rd.LocalName)
                                    {
                                        state.Profilkennung = rd.ReadElementString();
                                        if (!String.IsNullOrEmpty(state.Profilkennung))
                                        {
                                            state.Profilkennung = state.Profilkennung.Trim();
                                        }
                                    }
                                    if (ElementDatum == rd.LocalName)
                                    {
                                        state.Datum = rd.ReadElementString();
                                        if (!String.IsNullOrEmpty(state.Datum))
                                        {
                                            state.Datum = state.Datum.Trim();
                                        }
                                    }
                                    if (ElementLaufendeNummerVonGesamtzahl == rd.LocalName)
                                    {
                                        state.LaufendeNummerVonGesamtzahl = rd.ReadElementString();
                                        if (!String.IsNullOrEmpty(state.LaufendeNummerVonGesamtzahl))
                                        {
                                            state.LaufendeNummerVonGesamtzahl = state.LaufendeNummerVonGesamtzahl.Trim();
                                        }
                                    }
                                    if (ElementGesamtzahl == rd.LocalName)
                                    {
                                        state.Gesamtzahl = rd.ReadElementString();
                                        if (!String.IsNullOrEmpty(state.Gesamtzahl))
                                        {
                                            state.Gesamtzahl = state.Gesamtzahl.Trim();
                                        }
                                    }
                                    if (ElementSuedwestEcke == rd.LocalName)
                                    {
                                        state.SuedwestEcke = rd.ReadElementString();
                                        if (!String.IsNullOrEmpty(state.SuedwestEcke))
                                        {
                                            state.SuedwestEcke = state.SuedwestEcke.Trim();
                                        }
                                    }
                                }
                                else if (XmlNodeType.EndElement == rd.NodeType)
                                {
                                    if (ElementPortionskennung == rd.LocalName)
                                        break;
                                }

                            }
                        }
                    }
                    if (XmlNodeType.Element == rd.NodeType)
                    {
                        if (ElementAuftragsnummer == rd.LocalName)
                        {
                            state.Auftragsnummer = rd.ReadElementString();
                            if (!String.IsNullOrEmpty(state.Auftragsnummer))
                            {
                                state.Auftragsnummer = state.Auftragsnummer.Trim();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Liest den Inhalt einer BDA-Datei (AX_Bestandsdatenauszug) ab dem bereits
        /// konsumierten Wurzelelement bis zum Ende des Streams und befuellt
        /// <paramref name="state"/>. Setzt Art/IsBDA auf dieser Instanz.
        /// </summary>
        private void ParseBdaSection(System.Xml.XmlTextReader rd, ReadState state)
        {
            this.Art = ArtBda;
            this.IsBDA = true;
            while (rd.Read())
            {
                if (XmlNodeType.Element == rd.NodeType)
                {
                    if (ElementEnthaelt == rd.LocalName)
                    {
                        while (rd.Read())
                        {
                            if (XmlNodeType.Element == rd.NodeType)
                            {
                                if (ElementFeatureCollection == rd.LocalName)
                                {
                                    if (!rd.IsEmptyElement)
                                    { // Dann gibts kein EndElement !!!!
                                        while (rd.Read())
                                        {
                                            if (XmlNodeType.Element == rd.NodeType)
                                            {
                                                if (ElementFeatureMember == rd.LocalName || ElementMember == rd.LocalName)
                                                {
                                                    ReadElements(ElementFeatureMember, ref state.Inserts, rd);
                                                }
                                            }
                                            else if (XmlNodeType.EndElement == rd.NodeType)
                                            {
                                                if (ElementFeatureCollection == rd.LocalName)
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (XmlNodeType.EndElement == rd.NodeType)
                            {
                                if (ElementEnthaelt == rd.LocalName)
                                    break;
                            }
                        }
                    }
                    else if (ElementAntragsnummer == rd.LocalName)
                    {
                        state.Antragsnummer = rd.ReadElementString();
                        if (!String.IsNullOrEmpty(state.Antragsnummer))
                        {
                            state.Antragsnummer = state.Antragsnummer.Trim();
                            if (String.IsNullOrEmpty(state.Auftragsnummer))
                                state.Auftragsnummer = state.Antragsnummer;
                        }
                    }
                }
                state.Profilkennung = RootElementBda;
                state.LaufendeNummerVonGesamtzahl = "1";
                state.Gesamtzahl = "1";
            }
        }

        internal Boolean ReadStream(Stream st, bool RequireProfilkennung)
        {
            Boolean IsNasFile = false;
            try
            {
                var state = new ReadState();

                using System.Xml.XmlTextReader? rd = new System.Xml.XmlTextReader(st);
                rd.WhitespaceHandling = WhitespaceHandling.None;

                /*
                 * Insert und Replace können n Unterelemente haben (Replace hat aber immer nur eins):
                 * Delete hat nur ein Unterelement.
                 * Deswegen wird hier unterschidlich behandelt.
                 */
                while (rd.Read())
                {
                    if (XmlNodeType.Element == rd.NodeType)
                    {
                        if (RootElementNba == rd.LocalName)
                        {
                            IsNasFile = true;
                            ParseNbaSection(rd, state);
                        }
                        else if (RootElementBda == rd.LocalName)
                        {
                            IsNasFile = true;
                            ParseBdaSection(rd, state);
                        }
                    }
                }
                Inserts = state.Inserts;
                Deletes = state.Deletes;
                Replaces = state.Replaces;
                Updates = state.Updates;

                if (!String.IsNullOrEmpty(state.Auftragsnummer))
                {
                    if (!String.IsNullOrEmpty(state.Gesamtzahl) && !String.IsNullOrEmpty(state.LaufendeNummerVonGesamtzahl))
                    {
                        this.Gesamt = System.Convert.ToInt32(state.Gesamtzahl);
                        this.Portion = System.Convert.ToInt32(state.LaufendeNummerVonGesamtzahl);
                        this.Auftragsnummer = state.Auftragsnummer;
                        this.Antragsnummer = state.Antragsnummer;
                    }
                    else
                    {
                        this.Gesamt = 1;
                        this.Portion = 1;
                        this.Auftragsnummer = state.Auftragsnummer;
                        this.Antragsnummer = state.Antragsnummer;
                    }
                    if (String.IsNullOrEmpty(state.Profilkennung))
                    {
                        if (RequireProfilkennung)
                        {
                            Log.Error("ImportEntity.ReadStream: " + this.FullyQualifiedFileName);
                            Log.Error("ImportEntity.ReadStream: Profile ID not specified in multi-profile mode.");
                            IsNasFile = false;
                            throw new Exception("ImportEntity.ReadStream: Profile ID not specified in multi-profile mode.");
                        }
                        else
                        {
                            state.Profilkennung = "-";
                        }
                    }
                    if (String.IsNullOrEmpty(state.Datum)) state.Datum = "-";
                    if (!String.IsNullOrEmpty(state.Profilkennung)) this.Profilkennung = state.Profilkennung;
                    if (!String.IsNullOrEmpty(state.Datum)) this.Datum = state.Datum;
                    if (!String.IsNullOrEmpty(state.SuedwestEcke)) this.Suedwestecke = state.SuedwestEcke;
                }
            }
            catch (System.Xml.XPath.XPathException e)
            {
                Log.Error(this.FullyQualifiedFileName);
                Log.Error(e.Message);
            }
            catch (System.Exception e)
            {
                Log.Error(this.FullyQualifiedFileName);
                Log.Error(e.Message);
            }
            return IsNasFile;
        }
    }
}
