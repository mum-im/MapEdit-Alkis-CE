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
using MapEdit.Alkis.Services.Structure;
using System.Data.Common;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace MapEdit.Alkis.Services
{
    [XmlRoot(ElementName = "PropertyDefn")]
    public class PropertyDefn
    {
        [XmlElement(ElementName = "Name")]
        public string? Name { get; set; }

        [XmlElement(ElementName = "ElementPath")]
        public string? ElementPath { get; set; }

        [XmlElement(ElementName = "Type")]
        public string? Type { get; set; }

        [XmlElement(ElementName = "Width")]
        public string? Width { get; set; }
    }

    [XmlRoot(ElementName = "GMLFeatureClass")]
    public class GMLFeatureClass
    {
        [XmlElement(ElementName = "Name")]
        public string? Name { get; set; }

        [XmlElement(ElementName = "ElementPath")]
        public string? ElementPath { get; set; }

        [XmlElement(ElementName = "PropertyDefn")]
        public List<PropertyDefn>? PropertyDefn { get; set; }

        [XmlElement(ElementName = "GeomPropertyDefn")]
        public List<GeomPropertyDefn>? GeomPropertyDefn { get; set; }
    }

    [XmlRoot(ElementName = "GeomPropertyDefn")]
    public class GeomPropertyDefn
    {
        [XmlElement(ElementName = "Name")]
        public string? Name { get; set; }

        [XmlElement(ElementName = "ElementPath")]
        public string? ElementPath { get; set; }

        [XmlElement(ElementName = "GeometryType")]
        public string? GeometryType { get; set; }
    }

    [XmlRoot(ElementName = "GMLFeatureClassList")]
    public class GMLFeatureClassList
    {
        [XmlElement(ElementName = "GMLFeatureClass")]
        public List<GMLFeatureClass>? GMLFeatureClass { get; set; }
    }

    public class ProcessedFileEntry
    {
        /*
            Though ALKIS does not allow different configurations (profiles) to be mixed,
            this error-prone approach is really practised.
        */
        public string Configuration { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string CheckSum { get; set; } = string.Empty;
    }

    public abstract class DatabaseManager : IDatabaseManager
    {
        protected readonly IConfiguration _configuration;
        protected readonly ILogger<IPostNasExecuter> _logger;
        protected readonly ExecutionArgs _args;
        protected int _AlkisVersion { get; private set; } = 101;

        protected int AlkisVersion { get; set; } = 0;

        ///////////////////
        // internal
        // The structure in the db is valid if: AlkisVersion in db equals AND AlkisStructure in db >= _neededAlkisStructureVersion
        protected int _neededAlkisStructureVersion { get; set; } = 4; // 2025/11/12

        protected int Version { get; private set; } = 0; // MapEdit-Version
        protected int databaseSRID { get; set; } = 0;

        public int DatabaseSRID() => databaseSRID;

        protected bool AlkisStructure { get; set; } = false;

        protected void ReadParameters(DbCommand cmd)
        {
            using (DbDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    string value = reader.GetString(1);
                    switch (name)
                    {
                        case "VERSION":
                            this.Version = int.Parse(value);
                            break;

                        case "ALKIS_VERSION":
                            this.AlkisVersion = int.Parse(value);
                            break;

                        case "DEFAULT_SRID":
                            int srid = 0;
                            int.TryParse(value, out srid);
                            this.databaseSRID = srid;
                            break;

                        case "ALKIS_STRUKTUR":
                            int alkis = 0;
                            int.TryParse(value, out alkis);
                            this.AlkisStructure = (alkis >= _neededAlkisStructureVersion);
                            break;
                    }
                }
            }
        }

        protected void WriteParameters(DbCommand cmd)
        {
            cmd.CommandText = "delete from ME_PARAMETER where NAME='ALKIS_STRUKTUR'";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "insert into  ME_PARAMETER(NAME,VALUE) values('ALKIS_STRUKTUR','" + _neededAlkisStructureVersion.ToString() + "')";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "delete from ME_PARAMETER where NAME='ALKIS_VERSION'";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "insert into  ME_PARAMETER(NAME,VALUE) values('ALKIS_VERSION','" + _AlkisVersion.ToString() + "')";
            cmd.ExecuteNonQuery();
        }

        public virtual DBEngine Engine() => DBEngine.PG;

        protected List<ProcessedFileEntry>? AllEntries { get; set; } = null;

        public DatabaseManager(IConfiguration configuration, ILogger<IPostNasExecuter> logger, ExecutionArgs Args)
        {
            this._configuration = configuration;
            this._logger = logger;
            this._args = Args;
        }

        public virtual bool StructureExists(string ForceOverwrite)
        { return false; }

        public virtual void PreRun()
        { }
        public virtual void PostRun()
        { }

        protected static bool TableExists(DbCommand cmd, string tablename)
        {
            try
            {
                cmd.CommandText = "select count(1) from  " + tablename + " where 1 = 0--NOERRORLOG";
                _ = cmd.ExecuteScalar();
                return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public GMLFeatureClassList? ReadAttachedSchema()
            => LoadAttachedXmlObject<GMLFeatureClassList>("MapEdit.Alkis.Norbit", "alkis-schema.gfs");

        protected static T? LoadAttachedXmlObject<T>(string path, string name)
        {
            Type myType = typeof(T);
            object? obj = LoadAttachedXmlObject(path, name, myType);
            return (T?)obj;
        }

        protected static object? LoadAttachedXmlObject(string path, string name, Type objectType)
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Stream? stream = assembly.GetManifestResourceStream(path + "." + name);
                if (stream != null)
                {
                    XmlSerializer ser = new XmlSerializer(objectType);
                    object? obj = ser.Deserialize(stream);
                    return obj;
                }
            }
            catch (System.Exception)
            {
            }
            return null;
        }

        public string BaseMapType(string pName, string pType, string? pWidth, List<string> indexes)
        {
            if (pName.Equals("ogc_fid"))
            {
                indexes.Add("PRIMARY KEY (ogc_fid)");
                return "ogc_fid serial not null,";
            }
            switch (pType)
            {
                case "Real":
                    return pName + " double precision";

                case "Integer":
                    return pName + " integer";

                case "StringList":
                case "String":
                    if (pWidth is not null)
                    {
                        return pName + " character varying(" + pWidth + ")";
                    }
                    else
                    {
                        return pName + " character varying";
                    }
                default:
                    return pName + " character varying";
            }
        }

        public string MapGeomType(string? pType)
        {
            if (pType is null)
            {
                return "POINT";
            }
            switch (pType)
            {
                case "0":
                    return "GEOMETRY";

                case "1":
                    return "GEOMETRY";

                case "2":
                    return "GEOMETRY";

                case "3":
                    return "GEOMETRY";

                case "4":
                    return "GEOMETRY";

                case "5":
                    return "GEOMETRY";

                case "6":
                    return "GEOMETRY";

                default:
                    return "GEOMETRY";
            }
        }

        public bool CreateAlkisStructure(DbCommand cmd, DbConnection nc, Func<string, string, string?, List<string>, string> mapType, bool checkMeParameter = false)
        {
            try
            {
                GMLFeatureClassList? schema = ReadAttachedSchema();
                if (schema is null)
                {
                    return false;
                }
                cmd.Connection = nc;
                if (checkMeParameter)
                {
                    if (!TableExists(cmd, "me_parameter"))
                    {
                        return false;
                        //cmd.CommandText = "create table ME_PARAMETER (NAME varchar(255), VALUE varchar(255), primary key (NAME))";
                        //cmd.ExecuteNonQuery();
                    }
                }
                {
                    try
                    {
                        cmd.CommandText =
                        "drop table me_alkis_files";
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e) { }
                    cmd.CommandText =
                    "create table me_alkis_files (configuration varchar(255), filename varchar(4000), checksum varchar(255), primary key (configuration,filename))";
                    cmd.ExecuteNonQuery();
                }
                //
                //                cmd.CommandText = functions;
                //                cmd.ExecuteNonQuery();
                foreach (GMLFeatureClass fc in (schema.GMLFeatureClass ?? new List<GMLFeatureClass>()))
                {
                    string createString = string.Empty;
                    string deleteString = string.Empty;
                    List<string> geomString = new List<string>();
                    if (string.IsNullOrEmpty(fc.Name))
                    {
                        continue;
                    }
                    string name = fc.Name.ToLower();
                    createString = "create table " + name + "(";
                    deleteString = "drop table " + name + " cascade";
                    int pdc = fc.PropertyDefn?.Count ?? 0;
                    List<string> indexes = new List<string>();
                    bool first = true;
                    foreach (PropertyDefn pd in (fc.PropertyDefn ?? new List<PropertyDefn>()))
                    {
                        if (first)
                        {
                            createString += Environment.NewLine;
                            createString += mapType("ogc_fid", "-", pd.Width, indexes);
                            first = false;
                        }
                        if (pd.Name is null || pd.Type is null)
                        {
                            continue;
                        }
                        createString += Environment.NewLine;
                        createString += mapType(pd.Name, pd.Type, pd.Width, indexes);
                        pdc--;
                        if (pdc > 0)
                        {
                            createString += ",";
                        }
                        else
                        {
                            foreach (var indx in indexes)
                            {
                                createString += "," + Environment.NewLine;
                                createString += indx;
                            }
                            createString += ")" + Environment.NewLine;
                        }
                    }
                    foreach (GeomPropertyDefn gpd in (fc.GeomPropertyDefn ?? new List<GeomPropertyDefn>()))
                    {
                        string geomstr = "SELECT AddGeometryColumn('" + name + "', '" + gpd.Name + "'," + databaseSRID.ToString() + ", '" + MapGeomType(gpd.GeometryType) + "', 2);";
                        geomString.Add(geomstr);
                    }

                    Console.WriteLine(deleteString);
                    Console.WriteLine(createString);
                    foreach (string str in geomString)
                    {
                        Console.WriteLine(str);
                    }
                    try
                    {
                        cmd.CommandText = deleteString;
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    try
                    {
                        cmd.CommandText = createString;
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    foreach (string str in geomString)
                    {
                        try
                        {
                            cmd.CommandText = str;
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
                try
                {
                    cmd.CommandText = "Delete from me_parameter where name='ALKIS_STRUKTUR'";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "insert into me_parameter (name, value) values('ALKIS_STRUKTUR','1')";
                    //cmd.CommandText = "insert into me_parameter (name, value) values('ALKIS_STRUKTUR','2')";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                return true;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (cmd is IDisposable)
                {
                    cmd.Dispose();
                }
            }
        }

        private List<ProcessedFileEntry> GetConfigEntries(DbCommand cmd, DbConnection nc)
        {
            if (AllEntries is null)
            {
                if (AllEntries is null)
                {
                    AllEntries = new List<ProcessedFileEntry>();
                }
                ReadAllEntries(cmd, nc);
            }
            return AllEntries.Where(a => a.Configuration == _args.ConfigurationName).ToList();
        }

        private void ReadAllEntries(DbCommand cmd, DbConnection nc)
        {
            if (AllEntries is null)
            {
                AllEntries = new List<ProcessedFileEntry>();
            }
            try
            {
                if (nc != null && nc.State == System.Data.ConnectionState.Open && cmd != null)
                {
                    cmd.CommandText = "select configuration, filename, checksum from me_alkis_files";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var ent = new ProcessedFileEntry();
                            if (!reader.IsDBNull(0))
                                ent.Configuration = reader.GetString(0);
                            if (!reader.IsDBNull(1))
                                ent.FileName = reader.GetString(1);
                            if (!reader.IsDBNull(2))
                                ent.CheckSum = reader.GetString(2);
                            AllEntries.Add(ent);
                        }
                    }
                }
            }
            catch (Exception)
            {
                //throw;
            }
        }

        protected abstract bool ConnectAndRun(Action<DbConnection, DbCommand> connected);

        public void ProcessFiles(List<ImportEntity> files, Func<string, string, NumbersAndInsertFailures> ogr2ogrRun)
        {
            ConnectAndRun((dbc, dbcmd) => ProcessFiles(dbc, dbcmd, files, ogr2ogrRun));
        }

        private bool FileAlreadyProcessed(List<ProcessedFileEntry> entries, ImportEntity fe)
        {
            if (entries.Count < 1)
            {
                return false;
            }
            foreach (var f in entries)
            {
                if (f.CheckSum == fe.MD5)
                {
                    return true;
                }
            }
            return false;
        }

        private void PreScan(DbConnection nc, DbCommand cmd, List<ImportEntity> files)
        {
            /*
                As implemented, this algorithm will not detect errors if all files already imported are deleted and then files prior to those already imported are added.
            */
            List<ProcessedFileEntry> entries = GetConfigEntries(cmd, nc);
            using (var md5 = MD5.Create())
            {
                foreach (var f in files)
                {
                    using (var stream = File.OpenRead(f.FullyQualifiedFileName))
                    {
                        var hash = md5.ComputeHash(stream);
                        f.MD5 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                    if (FileAlreadyProcessed(entries, f))
                    {
                        f.IsAlreadyProcessed = true;
                    }
                }
            }
        }

        protected void ProcessFiles(DbConnection nc, DbCommand cmd, List<ImportEntity> files, Func<string, string, NumbersAndInsertFailures> ogr2ogrRun)
        {
            // should already be
            cmd.Connection = nc;
            string workfolder = _args.Folder + Path.DirectorySeparatorChar + "work";
            if (!Directory.Exists(workfolder))
            {
                Directory.CreateDirectory(workfolder);
            }
            if (!Directory.Exists(workfolder))
            {
                _logger.LogError("Verzeichnis konnte nicht erstellt werden: " + workfolder);
                return;
            }
            PreScan(nc, cmd, files);
            int count = 0;
            files. ForEach( f => { if(!f.IsAlreadyProcessed) count++; } );
            int current = 0;
            foreach (var f in files)
            {
                string dest = string.Empty;
                try
                {
                    if (!f.IsAlreadyProcessed)
                    {
                        current++;
                        var filename = f.FullyQualifiedFileName;
                        switch (f.TypeOfFile)
                        {
                            case ImportEntity.EntityType.GZIP:
                                dest = workfolder + Path.DirectorySeparatorChar + f.BaseName;
                                try
                                {
                                    File.Delete(dest);
                                }
                                catch { }
                                try
                                {
                                    using FileStream? fs = new FileStream(f.FullyQualifiedFileName, FileMode.Open, FileAccess.Read);
                                    using FileStream? of = new FileStream(dest, FileMode.CreateNew, FileAccess.Write);
                                    if (fs is not null && of is not null)
                                    {
                                        using var gZipStream = new GZipStream(fs, CompressionMode.Decompress);
                                        if (gZipStream is not null)
                                        {
                                            filename = string.Empty;
                                            gZipStream.CopyTo(of);
                                            _logger.LogInformation("Datei wird entpackt nach: " + dest);
                                            filename = dest;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    _logger.LogError(e.Message);
                                }
                                break;

                            case ImportEntity.EntityType.XML:
                                break;

                            case ImportEntity.EntityType.UNKNOWN:
                                continue;
                        }
                        if (!string.IsNullOrEmpty(filename))
                        {
                            BeforeOgr2Ogr(nc, cmd, filename);
                            string output = current.ToString() + " / " + count.ToString();// + ": " + f.FullyQualifiedFileName;
                            NumbersAndInsertFailures ogr2ogrRes = ogr2ogrRun(filename,output);
                            filename = f.FullyQualifiedFileName;
                            ScanResult sr = new ScanResult();
                            sr.Inserts = f.Inserts;
                            sr.Deletes = f.Deletes;
                            sr.Replaces = f.Replaces;
                            sr.Updates = f.Updates;
                            try
                            {
                                cmd.CommandText = "insert into me_alkis_files (configuration, filename, checksum) values('"
                                + _args.ConfigurationName + "', '" + f.FullyQualifiedFileName + "', '" + f.MD5 + "')";
                                cmd.ExecuteNonQuery();
                            }
                            catch (Exception e)
                            {
                                this._logger.LogError("DatabaseManager:" + e.Message);
                            }
                            AfterOgr2Ogr(nc, cmd, filename, ogr2ogrRes, sr);
                        }
                    }
                }
                catch (Exception e)
                {
                    string s = e.Message;
                }
                finally
                {
                    if (!string.IsNullOrEmpty(dest))
                    {
                        File.Delete(dest);
                    }
                }
            }
            //_databaseManager.ProcessImportedFile();
        }
        virtual protected void BeforeOgr2Ogr(DbConnection nc, DbCommand cmd, string file) { }
        virtual protected void AfterOgr2Ogr(DbConnection nc, DbCommand cmd, string file, NumbersAndInsertFailures ogr2ogrRes, ScanResult sr) { }
    }
}