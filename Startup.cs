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
using System.IO;
using System.Linq;
using System.Reflection;
using MapEdit.Alkis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MapEdit.Alkis
{
    public class ConfigurationItem
    {
        public readonly static int DefaultCommandTimeoutInSeconds = 300;
        public string Name { get; set; } = string.Empty;
        //
        public string Extends = string.Empty; // virtual heritage
        public string Driver { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Passfile { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Ogr2Ogr { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string GDAL_DRIVER_PATH { get; set; } = string.Empty;
        public string GDAL_LIBRARY_PATH { get; set; } = string.Empty; // mostly not needed
        public string GDAL_DATA { get; set; } = string.Empty;
        public string PROJ_LIB { get; set; } = string.Empty;
        public string ForceOverwrite { get; set; } = string.Empty;
        public string OnlyPostRun { get; set; } = string.Empty;
        public string NoPostRun { get; set; } = string.Empty;
        /// <summary>
        /// "No" (Default): eine erkannte Portionsluecke/Sequenzstoerung bricht den Lauf
        /// mit einer <see cref="Services.Structure.PortionGapException"/> ab (Exit-Code
        /// <see cref="Services.ExitCodes.PortionGap"/>). "Yes": es wird nur geloggt,
        /// der Import laeuft trotzdem mit der vollen Dateiliste weiter.
        /// </summary>
        public string AllowGaps { get; set; } = string.Empty;
        public string WarnForReplaceWithSameBeginnt { get; set; } = string.Empty;
        public string KeepHistory { get; set; } = string.Empty;
        public string PG_USE_COPY { get; set; } = string.Empty;
        public string OGR_PG_RETRIEVE_FID { get; set; } = string.Empty;
        public string OGR_PG_SKIP_CONFLICTS { get; set; } = string.Empty;
        public int DefaultCommandTimeout { get; set; } = DefaultCommandTimeoutInSeconds; // in seconds
        /// <summary>
        /// "No" (Default): Npgsql blendet das PostgreSQL-"DETAIL"-Feld aus Exceptions aus
        /// (kann sensible Daten enthalten, z.B. betroffene Zeilenwerte). "Yes": wird per
        /// "Include Error Detail=true" an Npgsql durchgereicht, fuer Diagnosezwecke.
        /// </summary>
        public string IncludeErrorDetail { get; set; } = string.Empty;
        public ConfigurationItem() { }
        public ConfigurationItem(IConfigurationSection c)
        {
            Name = c["Name"] ?? string.Empty;
            Extends = c["Extends"] ?? string.Empty;  // here correct - need to read extends
            Driver = c["Driver"] ?? string.Empty;
            Port = c["Port"] ?? string.Empty;
            Host = c["Host"] ?? string.Empty;
            Username = c["Username"] ?? string.Empty;
            Database = c["Database"] ?? string.Empty;
            Passfile = c["Passfile"] ?? string.Empty;
            Password = c["Password"] ?? string.Empty;
            Service = c["Service"] ?? string.Empty;
            Ogr2Ogr = c["Ogr2Ogr"] ?? string.Empty;
            Folder = c["Folder"] ?? string.Empty;
            GDAL_DRIVER_PATH = c["GDAL_DRIVER_PATH"] ?? string.Empty;
            GDAL_LIBRARY_PATH = c["GDAL_LIBRARY_PATH"] ?? string.Empty;
            GDAL_DATA = c["GDAL_DATA"] ?? string.Empty;
            PROJ_LIB = c["PROJ_LIB"] ?? string.Empty;
            ForceOverwrite = c["ForceOverwrite"] ?? string.Empty;
            OnlyPostRun = c["OnlyPostRun"] ?? string.Empty;
            NoPostRun = c["NoPostRun"] ?? string.Empty;
            AllowGaps = c["AllowGaps"] ?? string.Empty;
            WarnForReplaceWithSameBeginnt = c["WarnForReplaceWithSameBeginnt"] ?? string.Empty;
            KeepHistory = c["KeepHistory"] ?? string.Empty;
            PG_USE_COPY = c["PG_USE_COPY"] ?? string.Empty;
            OGR_PG_RETRIEVE_FID = c["OGR_PG_RETRIEVE_FID"] ?? string.Empty;
            OGR_PG_SKIP_CONFLICTS = c["OGR_PG_SKIP_CONFLICTS"] ?? string.Empty;
            DefaultCommandTimeout = c.GetValue("DefaultCommandTimeout", DefaultCommandTimeoutInSeconds);
            IncludeErrorDetail = c["IncludeErrorDetail"] ?? string.Empty;
        }
        public ConfigurationItem(ConfigurationItem source)
        { 
            source.CiForceCopyTo(this);
        }

        public void CiForceCopyTo(ConfigurationItem dest)
        {
            dest.Name = Name;
            dest.Extends = Extends;
            dest.Driver = Driver;
            dest.Port = Port;
            dest.Host = Host;
            dest.Username = Username;
            dest.Database = Database;
            dest.Passfile = Passfile;
            dest.Password = Password;
            dest.Service = Service;
            dest.Ogr2Ogr = Ogr2Ogr;
            dest.Folder = Folder;
            dest.GDAL_DRIVER_PATH = GDAL_DRIVER_PATH;
            dest.GDAL_LIBRARY_PATH = GDAL_LIBRARY_PATH;
            dest.GDAL_DATA = GDAL_DATA;
            dest.PROJ_LIB = PROJ_LIB;
            dest.ForceOverwrite = ForceOverwrite;
            dest.OnlyPostRun = OnlyPostRun;
            dest.NoPostRun = NoPostRun;
            dest.AllowGaps = AllowGaps;
            dest.WarnForReplaceWithSameBeginnt = WarnForReplaceWithSameBeginnt;
            dest.KeepHistory = KeepHistory;
            dest.PG_USE_COPY = PG_USE_COPY;
            dest.OGR_PG_RETRIEVE_FID = OGR_PG_RETRIEVE_FID;
            dest.OGR_PG_SKIP_CONFLICTS = OGR_PG_SKIP_CONFLICTS;
            dest.DefaultCommandTimeout = DefaultCommandTimeout;
            dest.IncludeErrorDetail = IncludeErrorDetail;
        }
        public void CiMergeTo(ConfigurationItem dest) // do not write if dest not empty
        {
            if (string.IsNullOrEmpty(dest.Name)) dest.Name = Name;
            if (string.IsNullOrEmpty(dest.Extends)) dest.Extends = Extends;
            if (string.IsNullOrEmpty(dest.Driver)) dest.Driver = Driver;
            if (string.IsNullOrEmpty(dest.Port)) dest.Port = Port;
            if (string.IsNullOrEmpty(dest.Host)) dest.Host = Host;
            if (string.IsNullOrEmpty(dest.Username)) dest.Username = Username;
            if (string.IsNullOrEmpty(dest.Database)) dest.Database = Database;
            if (string.IsNullOrEmpty(dest.Passfile)) dest.Passfile = Passfile;
            if (string.IsNullOrEmpty(dest.Password)) dest.Password = Password;
            if (string.IsNullOrEmpty(dest.Service)) dest.Service = Service;
            if (string.IsNullOrEmpty(dest.Ogr2Ogr)) dest.Ogr2Ogr = Ogr2Ogr;
            if (string.IsNullOrEmpty(dest.Folder)) dest.Folder = Folder;
            if (string.IsNullOrEmpty(dest.GDAL_DRIVER_PATH)) dest.GDAL_DRIVER_PATH = GDAL_DRIVER_PATH;
            if (string.IsNullOrEmpty(dest.GDAL_LIBRARY_PATH)) dest.GDAL_LIBRARY_PATH = GDAL_LIBRARY_PATH;
            if (string.IsNullOrEmpty(dest.GDAL_DATA)) dest.GDAL_DATA = GDAL_DATA;
            if (string.IsNullOrEmpty(dest.PROJ_LIB)) dest.PROJ_LIB = PROJ_LIB;
            if (string.IsNullOrEmpty(dest.ForceOverwrite)) dest.ForceOverwrite = ForceOverwrite;
            if (string.IsNullOrEmpty(dest.OnlyPostRun)) dest.OnlyPostRun = OnlyPostRun;
            if (string.IsNullOrEmpty(dest.NoPostRun)) dest.NoPostRun = NoPostRun;
            if (string.IsNullOrEmpty(dest.AllowGaps)) dest.AllowGaps = AllowGaps;
            if (string.IsNullOrEmpty(dest.WarnForReplaceWithSameBeginnt)) dest.WarnForReplaceWithSameBeginnt = WarnForReplaceWithSameBeginnt;
            if (string.IsNullOrEmpty(dest.KeepHistory)) dest.KeepHistory = KeepHistory;
            if (string.IsNullOrEmpty(dest.PG_USE_COPY)) dest.PG_USE_COPY = PG_USE_COPY;
            if (string.IsNullOrEmpty(dest.OGR_PG_RETRIEVE_FID)) dest.OGR_PG_RETRIEVE_FID = OGR_PG_RETRIEVE_FID;
            if (string.IsNullOrEmpty(dest.OGR_PG_SKIP_CONFLICTS)) dest.OGR_PG_SKIP_CONFLICTS = OGR_PG_SKIP_CONFLICTS;
            if(dest.DefaultCommandTimeout == ConfigurationItem.DefaultCommandTimeoutInSeconds)
                dest.DefaultCommandTimeout = DefaultCommandTimeout;
            if (string.IsNullOrEmpty(dest.IncludeErrorDetail)) dest.IncludeErrorDetail = IncludeErrorDetail;
        }
        public void CiApplyDefaults()
        {
            Driver = "PG";
            if (string.IsNullOrEmpty(Port))
               Port = "5432";
            if (string.IsNullOrEmpty(Passfile)) Passfile = string.Empty; // ?
            if (string.IsNullOrEmpty(Service)) Service = "ORCL";
            if (string.IsNullOrEmpty(ForceOverwrite)) ForceOverwrite = "No";
            if (string.IsNullOrEmpty(OnlyPostRun)) OnlyPostRun = "No";
            if (string.IsNullOrEmpty(NoPostRun)) NoPostRun = "No";
            if (string.IsNullOrEmpty(AllowGaps)) AllowGaps = "No";
            if (string.IsNullOrEmpty(IncludeErrorDetail)) IncludeErrorDetail = "No";
            if (OnlyPostRun.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                ForceOverwrite = "No";
            if (string.IsNullOrEmpty(WarnForReplaceWithSameBeginnt))
                WarnForReplaceWithSameBeginnt = "No";
            if (string.IsNullOrEmpty(KeepHistory))
                KeepHistory = "No";
            /*
             * For OGR_PG_SKIP_CONFLICTS see https://gdal.org/drivers/vector/pg.html
             * For OGR_PG_SKIP_CONFLICTS == Yes to work set PG_USE_COPY and   OGR_PG_RETRIEVE_FID to No 
             */
            if (string.IsNullOrEmpty(PG_USE_COPY))
                PG_USE_COPY = "No"; // do not change unless you really know what you are doing
            if (string.IsNullOrEmpty(OGR_PG_RETRIEVE_FID))
                OGR_PG_RETRIEVE_FID = "No"; // do not change unless you really know what you are doing
            if (string.IsNullOrEmpty(OGR_PG_SKIP_CONFLICTS))
                OGR_PG_SKIP_CONFLICTS = "Yes"; // do not change unless you really know what you are doing
        }
    }

    public static class Startup
    {
        /// <summary>
        /// Baut den DI-Container fuer MapEdit.Alkis auf. Liefert bei Erfolg immer einen
        /// vollstaendig konfigurierten Container (inkl. EntryPoint) zurueck — bei einem
        /// bekannten Start-/Konfigurationsfehler wird eine ExitCodeException geworfen statt
        /// (wie frueher) still null oder ein unvollstaendig registriertes IServiceCollection
        /// zurueckzugeben, was in Program.Main zu einem stillen No-Op mit Exit-Code 0 fuehrte.
        /// </summary>
        public static IServiceCollection ConfigureServices(string basePath, string config, string stdoutLogPath = "")
        {
            var services = new ServiceCollection();

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            IConfiguration configuration;
            try
            {
                configuration = builder.Build();
            }
            catch (Exception ex)
            {
                throw new ExitCodeException(ExitCodes.ConfigFileError, "Error reading appsettings.json: " + ex.Message);
            }
            services.AddSingleton(configuration);
            ConfigurationItem confItem = new ConfigurationItem();
            {
                List<ConfigurationItem> WithExtends = new List<ConfigurationItem>();
                List<ConfigurationItem> WithOutExtends = new List<ConfigurationItem>();
                List<IConfigurationSection> lconfiguration = configuration.GetSection("Configurations").GetChildren().ToList();
                foreach (IConfigurationSection c in lconfiguration)
                {
                    var ci = new ConfigurationItem(c);
                    if (!string.IsNullOrEmpty(ci.Name))
                    {
                        if (!string.IsNullOrEmpty(ci.Extends))
                        {
                            WithExtends.Add(ci);
                        }
                        else
                        {
                            WithOutExtends.Add(ci);
                        }
                    } // Else no error is logged yet !!!
                }
                int i = 0; // ageinst cyclic Extends: a.Extends=b.Name && b.Extends=a.Name
                while(WithExtends.Count>0 && i++ < 10)
                {
                    var tmp = WithExtends;
                    WithExtends = new List<ConfigurationItem>();
                    foreach (ConfigurationItem c in tmp)
                    {
                        bool found = false;
                        var cd = new ConfigurationItem(c);
                        // WithOutExtends
                        if (!found)
                        {
                            var woe = WithOutExtends;
                            WithOutExtends = new List<ConfigurationItem>();
                            WithOutExtends.AddRange(woe);
                            foreach (ConfigurationItem co in woe)
                            {
                                if (cd.Extends.Equals(co.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    found = true;
                                    cd.Extends = string.Empty;
                                    co.CiMergeTo(cd);
                                    WithOutExtends.Add(cd);
                                }
                            }
                        }
                        // WithExtends
                        if (!found)
                        {
                            foreach (ConfigurationItem cw in tmp)
                            {
                                if (!cd.Name.Equals(cw.Name, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (cd.Extends.Equals(cw.Name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        found = true;
                                        cd.Extends = string.Empty;
                                        cw.CiMergeTo(cd);
                                        if (!string.IsNullOrEmpty(cd.Extends))
                                        {
                                            WithExtends.Add(cd);
                                        }
                                        else
                                        {
                                            WithOutExtends.Add(cd);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        if (!found)
                        {
                            cd.Extends = string.Empty; // No error log yet !!!
                            WithOutExtends.Add(cd);
                        }
                    }
                }


                //
                foreach (ConfigurationItem c in WithOutExtends)
                {
                    if (c.Name.Equals(config,StringComparison.OrdinalIgnoreCase))
                    {
                        confItem = c;
                        break;
                    }
                }
                confItem.CiApplyDefaults();
            }
            if (string.IsNullOrEmpty(confItem.Folder) || !Directory.Exists(confItem.Folder))
            {
                throw new ExitCodeException(ExitCodes.ConfigOrFolderNotFound,
                    "Error reading appsettings.json: config " + config + " not found or folder " + confItem.Folder + " not found.");
            }
            string LogPath = confItem.Folder + Path.DirectorySeparatorChar + "Log";
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);
            if (!Directory.Exists(LogPath))
                throw new ExitCodeException(ExitCodes.LogDirectoryUnavailable, "Log directory could not be created: " + LogPath);
            string MinimumLevel = string.Empty;
            {
                var lconfiguration = configuration.GetSection("Serilog").GetChildren().ToList();
                foreach (var c in lconfiguration)
                {
                    if (c.Key == "MinimumLevel")
                        MinimumLevel = c.Value;
                }
            }
            var loggerConfiguration = new LoggerConfiguration()
                .WriteTo.File(path: LogPath + Path.DirectorySeparatorChar + "log-.log",
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{SourceContext}] {Message}{NewLine}{Exception}",
                                rollingInterval: RollingInterval.Day,
                                rollOnFileSizeLimit: true,
                                retainedFileCountLimit: 10,
                                fileSizeLimitBytes: 4194304)
                .MinimumLevel.Information();
            if (!string.IsNullOrEmpty(MinimumLevel))
            {
                switch (MinimumLevel)
                {
                    case "Verbose":
                        loggerConfiguration.MinimumLevel.Verbose();
                        break;

                    case "Debug":
                        loggerConfiguration.MinimumLevel.Debug();
                        break;

                    case "Information":
                        loggerConfiguration.MinimumLevel.Information();
                        break;

                    case "Warning":
                        loggerConfiguration.MinimumLevel.Warning();
                        break;

                    case "Error":
                        loggerConfiguration.MinimumLevel.Error();
                        break;

                    case "Fatal":
                        loggerConfiguration.MinimumLevel.Fatal();
                        break;
                }
            }
            var logger = loggerConfiguration.CreateLogger();
            Log.Logger = logger;

            services.AddLogging(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                //builder.AddConsole();
                builder.AddSerilog();
            });
            services.AddSingleton<ExecutionArgs>(_ =>
            {
                var ea = new ExecutionArgs(confItem);
                ea.StdoutLogPath = stdoutLogPath;
                return ea;
            });
            services.AddSingleton<IDirectoryScanner, BasicDirectoryScanner>();
            if (OperatingSystem.IsLinux())
                services.AddSingleton<IPostNasExecuter, PostNasExecuterLinux>();
            else if (OperatingSystem.IsWindows())
                services.AddSingleton<IPostNasExecuter, PostNasExecuterWindows>();
            else
                throw new ExitCodeException(ExitCodes.UnsupportedOperatingSystem, "Unsupported operating system.");
            services.AddSingleton<IDatabaseManager, DatabaseManagerPG>();
            var mTablesExtension = LoadPostRunExtension();
            if (mTablesExtension != null)
                services.AddSingleton<IPostRunExtension>(mTablesExtension);
            services.AddSingleton<EntryPoint>();

            return services;
        }

        // Optionale Erweiterung "X": wird nur ausgeführt, wenn die DLL neben der .exe liegt.
        // Community-Edition ohne diese DLL -> IPostRunExtension bleibt unregistriert, DatabaseManagerPG erhält null.
        private static IPostRunExtension? LoadPostRunExtension()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "MapEdit.Alkis.PostRunExtension.dll");
                if (!File.Exists(path))
                    return null;
                Assembly assembly = Assembly.LoadFrom(path);
                Type? implementationType = assembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPostRunExtension).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                if (implementationType is null)
                    return null;
                return Activator.CreateInstance(implementationType) as IPostRunExtension;
            }
            catch
            {
                return null;
            }
        }
    }
}