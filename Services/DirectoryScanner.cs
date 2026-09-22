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
using MapEdit.Alkis.Services.Structure;
using Microsoft.Extensions.Logging;

namespace MapEdit.Alkis.Services
{
    public class BasicDirectoryScanner : IDirectoryScanner
    {
        private readonly ILogger<IDirectoryScanner> _logger;

        public BasicDirectoryScanner(ILogger<IDirectoryScanner> logger)
        {
            this._logger = logger;
        }

        private List<ImportEntity> Scan(DirectoryInfo directory, List<ImportEntity> files, ImportEntity.EntityType _type, bool recurseSubDirectories, bool requireProfilkennung)
        {
            string filter = string.Empty;
            switch(_type)
            {
                case ImportEntity.EntityType.XML:
                    filter="*.xml";
                    break;
                case ImportEntity.EntityType.GZIP:
                    filter="*.gz";
                    break;
                default:
                    return files;
            }
            try
            {
                var opts = new EnumerationOptions();
                opts.MatchCasing = MatchCasing.CaseInsensitive;
                opts.RecurseSubdirectories = false;
                foreach (FileInfo file in directory.GetFiles(filter, opts))
                {
                    var f = new ImportEntity();
                    f.FullyQualifiedFileName=file.FullName;
                    f.TypeOfFile = _type;
                    if (f.ReadFile(RequireProfilkennung: requireProfilkennung))
                    {
                        files.Add(f);
                    }
                    else
                    {
                        _logger.LogInformation(f.FullyQualifiedFileName + " ist keine NAS-Datei.");
                    }
                }

                if (recurseSubDirectories)
                {
                    DirectoryInfo [] subDirectories = directory.GetDirectories();
                    foreach (DirectoryInfo subDirectory in subDirectories)
                    {
                        Scan(subDirectory, files, _type, recurseSubDirectories, requireProfilkennung);
                    }
                }
                return files;
            }catch(Exception e)
            {
                _logger.LogError(e.Message);
                return files;
            }
        }

        public List<ImportEntity> Scan(string Path, bool recurseSubDirectories = true, bool requireProfilkennung = true, bool allowGaps = false)
        {
            this._logger.LogDebug("BasicDirectoryScanner.Scan entered!");
            var files = new List<ImportEntity> ();
            Scan(new DirectoryInfo(Path), files, ImportEntity.EntityType.XML, recurseSubDirectories, requireProfilkennung);
            Scan(new DirectoryInfo(Path), files, ImportEntity.EntityType.GZIP, recurseSubDirectories, requireProfilkennung);
            files.Sort();
            if(ImportEntitySequenceValidator.IsContinuous(files))
            {
                return files;
            }
            if (allowGaps)
            {
                _logger.LogWarning("BasicDirectoryScanner.Scan: Portionsluecke oder Sequenzproblem erkannt - Import wird laut AllowGaps=Yes trotzdem mit der vollen Dateiliste fortgesetzt.");
                return files;
            }
            throw new PortionGapException("Es wurde eine Portionsluecke oder ein Sequenzproblem erkannt. Abbruch, da AllowGaps=No.");
        }
    }
}
