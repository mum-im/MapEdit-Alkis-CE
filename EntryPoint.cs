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
using MapEdit.Alkis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MapEdit.Alkis
{
    public class EntryPoint
    {
        private readonly IPostNasExecuter _postNasExecuter;
        private readonly IDirectoryScanner _directoryScanner;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EntryPoint> _logger;
        private readonly IDatabaseManager _databaseManager;
        private readonly ExecutionArgs _args;
        private string Configuration {get; set;} = string.Empty;

        public EntryPoint(IPostNasExecuter postNasExecuter, IDirectoryScanner directoryScanner, IConfiguration configuration, ILogger<EntryPoint> logger, IDatabaseManager manager, ExecutionArgs args)
        {
            this._directoryScanner = directoryScanner;
            this._postNasExecuter = postNasExecuter;
            this._configuration = configuration;
            this._logger = logger;
            this._databaseManager = manager;
            this._args = args;
        }

        public void Run(string config, string folder, String[] args)
        {
            /*
             * Check some config options
             */
            {
                if(!File.Exists(_args.Ogr2Ogr))
                {
                    this._logger.LogError("OGR not found:" + _args.Ogr2Ogr);
                    throw new ExitCodeException(ExitCodes.Ogr2OgrNotFound, "OGR not found: " + _args.Ogr2Ogr);
                }
                if(!Directory.Exists(_args.Folder))
                {
                    this._logger.LogError("Path not found:" + _args.Folder);
                    throw new ExitCodeException(ExitCodes.DataFolderNotFound, "Path not found: " + _args.Folder);
                }
            }
            Configuration = config;
            if(_databaseManager.StructureExists(_args.ForceOverwrite))
            {
                this._logger.LogDebug("The config is: " + Configuration);
                this._postNasExecuter.Execute();
            }
            else
            {
                this._logger.LogError("The database is no MapEdit database.");
                throw new ExitCodeException(ExitCodes.NotAMapEditDatabase, "The database is no MapEdit database.");
            }
        }
    }
}