﻿//  Copyright (C) 2019, The Duplicati Team
//  http://www.duplicati.com, info@duplicati.com
//
//  This library is free software; you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as
//  published by the Free Software Foundation; either version 2.1 of the
//  License, or (at your option) any later version.
//
//  This library is distributed in the hope that it will be useful, but
//  WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//  Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public
//  License along with this library; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
using Duplicati.Library.SQLiteHelper;
using Duplicati.Server;
using Duplicati.Server.Serializable;
using Duplicati.Server.WebServer.RESTMethods;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Duplicati.CommandLine.ConfigurationImporter
{
    public class ConfigurationImporter
    {
        private static readonly string usageString = $"Usage: {nameof(ConfigurationImporter)}.exe <configuration-file> --import-metadata=(true | false) [<advanced-option>]...";

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                throw new ArgumentException($"Incorrect number of input arguments.  {ConfigurationImporter.usageString}");
            }

            string configurationFile = args[0];
            Dictionary<string, string> importOptions = Duplicati.Library.Utility.CommandLineParser.ExtractOptions(new List<string> { args[1] });
            if (!importOptions.TryGetValue("import-metadata", out string importMetadataString))
            {
                throw new ArgumentException($"Invalid import-metadata argument.  {ConfigurationImporter.usageString}");
            }
            bool importMetadata = Duplicati.Library.Utility.Utility.ParseBool(importMetadataString, false);

            // This removes the ID and DBPath from the backup configuration.
            ImportExportStructure importedStructure = Backups.LoadConfiguration(configurationFile, importMetadata, () => ConfigurationImporter.ReadPassword($"Password for {configurationFile}: "));

            Dictionary<string, string> advancedOptions = Duplicati.Library.Utility.CommandLineParser.ExtractOptions(new List<string>(args.Skip(2)));

            // This will create the Duplicati-server.sqlite database file if it doesn't exist.
            Duplicati.Server.Database.Connection connection = Program.GetDatabaseConnection(advancedOptions);

            if (connection.Backups.Any(x => x.Name.Equals(importedStructure.Backup.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A backup with the name {importedStructure.Backup.Name} already exists.");
            }

            string error = connection.ValidateBackup(importedStructure.Backup, importedStructure.Schedule);
            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException(error);
            }

            // This creates a new ID and DBPath.
            connection.AddOrUpdateBackupAndSchedule(importedStructure.Backup, importedStructure.Schedule);

            // This creates the local database file at the location specified by the DBPath.
            SQLiteLoader.LoadConnection(importedStructure.Backup.DBPath);

            Console.WriteLine($"Imported \"{importedStructure.Backup.Name}\" with ID {importedStructure.Backup.ID} and local database at {importedStructure.Backup.DBPath}.");
        }

        private static string ReadPassword(string prompt)
        {
            Console.Write(prompt);
            string password = "";
            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    break;
                }
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password = password.Substring(0, password.Length - 1);
                        Console.Write("\b \b");
                    }
                }
                else if (keyInfo.KeyChar != '\u0000') // Only accept if the key maps to a Unicode character (e.g., ignore F1 or Home).
                {

                    password += keyInfo.KeyChar;
                    Console.Write("*");
                }
            }

            Console.WriteLine();
            return password;
        }
    }
}
