using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Resource;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Create-archive command - Create archive (ERF, RIM) from directory.
    /// </summary>
    /// <remarks>
    /// Based on vendor/PyKotor/Tools/KotorCLI/src/kotorcli/commands/create_archive.py
    /// Supports:
    /// - ERF/MOD/SAV/HAK archives
    /// - RIM archives
    /// </remarks>
    public static class CreateArchiveCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var createArchiveCommand = new Command("create-archive", "Create archive (ERF, RIM) from directory");
            var directoryOption = new Option<string>("--directory", "Directory to pack");
            directoryOption.Required = true;
            createArchiveCommand.Options.Add(directoryOption);
            var outputOption = new Option<string>("--output", "Output archive file");
            outputOption.Required = true;
            createArchiveCommand.Options.Add(outputOption);
            var typeOption = new Option<string>("--type", "Archive type (ERF, MOD, SAV, RIM)");
            createArchiveCommand.Options.Add(typeOption);
            var filterOption = new Option<string>("--filter", "Filter files by pattern (supports wildcards)");
            createArchiveCommand.Options.Add(filterOption);

            createArchiveCommand.SetAction(parseResult =>
            {
                var directory = parseResult.GetValue(directoryOption);
                var output = parseResult.GetValue(outputOption);
                var type = parseResult.GetValue(typeOption);
                var filter = parseResult.GetValue(filterOption);

                var logger = new StandardLogger();
                var exitCode = Execute(directory, output, type, filter, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(createArchiveCommand);
        }

        private static int Execute(string directory, string output, string type, string filter, ILogger logger)
        {
            string inputDir = Path.GetFullPath(directory);
            if (!Directory.Exists(inputDir))
            {
                logger.Error($"Input directory does not exist: {inputDir}");
                return 1;
            }

            string outputPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            try
            {
                // Determine archive type from --type option or output extension
                string archiveType;
                if (!string.IsNullOrEmpty(type))
                {
                    archiveType = type.ToLowerInvariant();
                }
                else
                {
                    string extension = Path.GetExtension(outputPath).ToLowerInvariant();
                    archiveType = extension.TrimStart('.');
                }

                // Create appropriate archive
                if (archiveType == "erf" || archiveType == ".erf" || archiveType == "hak" || archiveType == ".hak" ||
                    archiveType == "mod" || archiveType == ".mod" || archiveType == "sav" || archiveType == ".sav")
                {
                    string erfType = archiveType.TrimStart('.').ToUpperInvariant();
                    CreateErfFromDirectory(inputDir, outputPath, erfType, filter, logger);
                    logger.Info($"Created {erfType} archive: {Path.GetFileName(outputPath)}");
                }
                else if (archiveType == "rim" || archiveType == ".rim")
                {
                    CreateRimFromDirectory(inputDir, outputPath, filter, logger);
                    logger.Info($"Created RIM archive: {Path.GetFileName(outputPath)}");
                }
                else
                {
                    logger.Error($"Unsupported archive type: {archiveType}");
                    logger.Info("Supported types: ERF, MOD, SAV, HAK, RIM");
                    return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to create archive from {inputDir}: {ex.Message}");
                logger.Error(ex.StackTrace);
                return 1;
            }
        }

        /// <summary>
        /// Checks if text matches filter pattern (supports wildcards).
        /// </summary>
        private static bool MatchesFilter(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }

            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                // Convert wildcard pattern to regex
                string regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
            }

            return text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Creates an ERF/MOD/SAV/HAK archive from a directory of files.
        /// </summary>
        private static void CreateErfFromDirectory(string inputDir, string outputPath, string erfType, string filter, ILogger logger)
        {
            // Map string type to ERFType enum
            ERFType erfEnumType;
            bool isSave = false;
            switch (erfType.ToUpperInvariant())
            {
                case "ERF":
                    erfEnumType = ERFType.ERF;
                    break;
                case "MOD":
                    erfEnumType = ERFType.MOD;
                    break;
                case "SAV":
                    erfEnumType = ERFType.MOD; // SAV uses MOD signature
                    isSave = true;
                    break;
                case "HAK":
                    erfEnumType = ERFType.ERF; // HAK uses ERF structure
                    break;
                default:
                    erfEnumType = ERFType.ERF;
                    break;
            }

            var erf = new ERF(erfEnumType, isSave);

            // Collect files from directory
            foreach (string filePath in Directory.GetFiles(inputDir))
            {
                string fileName = Path.GetFileName(filePath);

                // Apply filter if specified
                if (!MatchesFilter(fileName, filter))
                {
                    continue;
                }

                // Parse filename to get resref and extension
                string stem = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath).TrimStart('.');

                // Try to get resource type from extension
                ResourceType restype;
                try
                {
                    restype = ResourceType.FromExtension(ext);
                }
                catch (ArgumentException)
                {
                    // Skip unknown file types
                    continue;
                }

                // Handle files with embedded type in stem (e.g., "model.123.mdl")
                string resref = stem;
                if (stem.Contains("."))
                {
                    string[] parts = stem.Split('.');
                    // Check if last part is numeric (resource ID)
                    if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int _))
                    {
                        resref = string.Join(".", parts.Take(parts.Length - 1));
                    }
                }

                // Read file data and add to ERF
                byte[] fileData = File.ReadAllBytes(filePath);
                ResRef resRef = new ResRef(resref);
                erf.SetData(resRef.ToString(), restype, fileData);
            }

            // Determine ResourceType for writing
            ResourceType outputFormat;
            switch (erfType.ToUpperInvariant())
            {
                case "ERF":
                    outputFormat = ResourceType.ERF;
                    break;
                case "MOD":
                    outputFormat = ResourceType.MOD;
                    break;
                case "SAV":
                    outputFormat = ResourceType.SAV;
                    break;
                case "HAK":
                    outputFormat = ResourceType.HAK;
                    break;
                default:
                    outputFormat = ResourceType.ERF;
                    break;
            }

            // Write ERF archive
            ERFAuto.WriteErf(erf, outputPath, outputFormat);
        }

        /// <summary>
        /// Creates a RIM archive from a directory of files.
        /// </summary>
        private static void CreateRimFromDirectory(string inputDir, string outputPath, string filter, ILogger logger)
        {
            var rim = new RIM();

            // Collect files from directory
            foreach (string filePath in Directory.GetFiles(inputDir))
            {
                string fileName = Path.GetFileName(filePath);

                // Apply filter if specified
                if (!MatchesFilter(fileName, filter))
                {
                    continue;
                }

                // Parse filename to get resref and extension
                string stem = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath).TrimStart('.');

                // Try to get resource type from extension
                ResourceType restype;
                try
                {
                    restype = ResourceType.FromExtension(ext);
                }
                catch (ArgumentException)
                {
                    // Skip unknown file types
                    continue;
                }

                // Handle files with embedded type in stem (e.g., "model.123.mdl")
                string resref = stem;
                if (stem.Contains("."))
                {
                    string[] parts = stem.Split('.');
                    // Check if last part is numeric (resource ID)
                    if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int _))
                    {
                        resref = string.Join(".", parts.Take(parts.Length - 1));
                    }
                }

                // Read file data and add to RIM
                byte[] fileData = File.ReadAllBytes(filePath);
                rim.SetData(resref, restype, fileData);
            }

            // Write RIM archive
            RIMAuto.WriteRim(rim, outputPath, ResourceType.RIM);
        }
    }
}
