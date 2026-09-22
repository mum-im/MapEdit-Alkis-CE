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
using Microsoft.Extensions.DependencyInjection;
using MapEdit.Alkis.Services;

namespace MapEdit.Alkis
{
    public class Program
    {
        private static (string folder, string config, string stdoutLog)? ProcessArgs(string[] args)
        {
            string folder = string.Empty;
            string config = string.Empty;
            string stdoutLog = string.Empty;
            for (int i = 0; i < args.Length-1; i++)
            {
                var arg = args[i].ToLower();
                if (arg == "--folder" || arg=="-f")
                    folder = args[i + 1];
                if (arg == "--configuration" || arg=="-c")
                    config = args[i + 1];
                if (arg == "--stdout-log")
                    stdoutLog = args[i + 1];
            }

            if (string.IsNullOrEmpty(folder))
            {
                Console.WriteLine("--folder or -f is a required flag");
                return null;
            }
            if (string.IsNullOrEmpty(config))
            {
                Console.WriteLine("--configuration or -c is a required flag");
                return null;
            }
            return (folder, config, stdoutLog);
        }

        public static void Main(String[] args)
        {
            var result = ProcessArgs(args);
            if(result is null)
                return;
            var (folder, config, stdoutLog) = result.Value;
            try
            {
                var services = Startup.ConfigureServices(folder, config, stdoutLog);
                var serviceProvider = services.BuildServiceProvider();
                var service = serviceProvider.GetService<EntryPoint>();
                service?.Run(config, folder, args);
            }
            catch (ExitCodeException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Environment.ExitCode = ex.ExitCode;
            }
        }
    }


}