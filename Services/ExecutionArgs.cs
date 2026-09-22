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

using System.Reflection;

namespace MapEdit.Alkis.Services
{
    public class ExecutionArgs : ConfigurationItem
    {
        private const string ResourceRootNamespace = "MapEdit.Alkis";

        public string ConfigurationName
        {
            get { return Name; }
        }
        /// <summary>
        /// Wenn gesetzt, spiegelt MapEdit.Alkis alle Console.Write-Ausgaben
        /// (ogr2ogr-Fortschritt) zusätzlich in diese Datei — der Controller liest
        /// sie live als Fortschrittsanzeige.
        /// </summary>
        public string StdoutLogPath { get; set; } = string.Empty;
        public ExecutionArgs() { }
        public ExecutionArgs(ConfigurationItem ci) { ci.CiForceCopyTo(this as ConfigurationItem); }
        // Just to make it clear. Note that strings are immutable. On change a new object is created.
        public void CopyTo(ExecutionArgs dest) => CiForceCopyTo(dest);
        public static string[] GetManifestResourceNames()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames();
        }

        private static string readAttachedFile(string path, string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream? stream = assembly.GetManifestResourceStream(path + "." + name);
            if (stream != null)
            {
                StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            return string.Empty;
        }

        private static string readAttachedFileEx(string path, string name, string? SRID, string? replaceWhat = null, string? replaceBy = null)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream? stream = assembly.GetManifestResourceStream(path + "." + name);
            if (stream != null)
            {
                StreamReader reader = new StreamReader(stream);
                string result = reader.ReadToEnd()
                    .Replace("SET client_encoding TO 'UTF8';", "")
                    .Replace("SET search_path = :\"alkis_schema\", :\"parent_schema\", :\"postgis_schema\", public;", " "); // auch am Ende der Prozeduren
                if (!string.IsNullOrEmpty(SRID))
                {
                    result = result.Replace(":alkis_epsg", SRID);
                }
                if (!string.IsNullOrEmpty(replaceWhat) && !string.IsNullOrEmpty(replaceBy))
                {
                    result = result.Replace(replaceWhat, replaceBy);
                }
                return result;
            }
            return string.Empty;
        }

        public static string readEmbeddedResource(string name, string version = "101") => readAttachedFile(ResourceRootNamespace + ".Version" + version, name);

        public static string readEmbeddedResourceEx(string name, string? SRID, string version = "101") => readAttachedFileEx(ResourceRootNamespace + ".Version" + version, name, SRID);

        public static string readEmbeddedResourceEx2(string name, string version = "101") => readAttachedFileEx(ResourceRootNamespace + ".Version" + version, name, null);

        public static string readEmbeddedResourceEx3(string name, string replaceWhat, string replaceBy, string version = "101") => readAttachedFileEx(ResourceRootNamespace + ".Version" + version, name, null, replaceWhat, replaceBy);

    }
}