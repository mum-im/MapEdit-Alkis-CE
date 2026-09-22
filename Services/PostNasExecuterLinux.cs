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
using System.IO;

namespace MapEdit.Alkis.Services
{
    /* not yet implemented */
    public class PostNasExecuterLinux : PostNasExecuter
    {
        public PostNasExecuterLinux(
            IConfiguration configuration, ILogger<IPostNasExecuter> logger, 
            ExecutionArgs args, IDirectoryScanner directoryScanner, IDatabaseManager databaseManager)  :  base(configuration, logger, args, directoryScanner, databaseManager)
        {
        }
        protected override NumbersAndInsertFailures RunOgr2gr(string filename, string output)
        {
            return new NumbersAndInsertFailures();
        }
    }
}