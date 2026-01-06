using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Formats.KEY;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.BIF;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Extract command - Extract resources from archive files (KEY/BIF, RIM, ERF, etc.).
    /// </summary>
    /// <remarks>
    /// Based on vendor/PyKotor/Tools/KotorCLI/src/kotorcli/commands/extract.py
    /// Supports:
    /// - KEY/BIF archives
    /// - RIM archives
    /// - ERF/MOD/SAV/HAK archives
    /// - BIF files (with optional KEY file for resource names)
    /// </remarks>
    public static class ExtractCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var extractCommand = new Command("extract", "Extract resources from archive files (KEY/BIF, RIM, ERF, etc.)");
            var fileOption = new Option<string>("--file", "Archive file to extract");
            fileOption.Required = true;
            extractCommand.Options.Add(fileOption);
            var outputOption = new Option<string>("--output", "Output directory (default: archive_name)");
            extractCommand.Options.Add(outputOption);
            var filterOption = new Option<string>("--filter", "Filter resources by name pattern (supports wildcards)");
            extractCommand.Options.Add(filterOption);
            var keyFileOption = new Option<string>("--key-file", "KEY file for BIF extraction (default: chitin.key)");
            extractCommand.Options.Add(keyFileOption);

            extractCommand.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(fileOption);
                var output = parseResult.GetValue(outputOption);
                var filter = parseResult.GetValue(filterOption);
                var keyFile = parseResult.GetValue(keyFileOption);

                var logger = new StandardLogger();
                var exitCode = Execute(file, output, filter, keyFile, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(extractCommand);
        }

        private static int Execute(string file, string output, string filter, string keyFile, ILogger logger)
        {
            if (string.IsNullOrEmpty(file))
            {
                logger.Error("No input file specified. Use --file <archive>");
                return 1;
            }

            string inputPath = Path.GetFullPath(file);
            if (!File.Exists(inputPath))
            {
                logger.Error($"Input file not found: {inputPath}");
                return 1;
            }

            // Determine output directory
            string outputDir;
            if (!string.IsNullOrEmpty(output))
            {
                outputDir = Path.GetFullPath(output);
            }
            else
            {
                string archiveName = Path.GetFileNameWithoutExtension(inputPath);
                outputDir = Path.Combine(Directory.GetCurrentDirectory(), archiveName);
            }

            Directory.CreateDirectory(outputDir);

            // Detect archive type by extension
            string extension = Path.GetExtension(inputPath).ToLowerInvariant();
            logger.Info($"Extracting from {extension} archive: {Path.GetFileName(inputPath)}");

            try
            {
                // Dispatch to appropriate extractor
                if (extension == ".key")
                {
                    return ExtractKey(inputPath, outputDir, filter, logger);
                }
                else if (extension == ".bif")
                {
                    return ExtractBif(inputPath, outputDir, filter, keyFile, logger);
                }
                else if (extension == ".rim")
                {
                    return ExtractRim(inputPath, outputDir, filter, logger);
                }
                else if (extension == ".erf" || extension == ".mod" || extension == ".sav" || extension == ".hak")
                {
                    return ExtractErf(inputPath, outputDir, filter, logger);
                }
                else
                {
                    logger.Error($"Unsupported archive type: {extension}");
                    logger.Info("Supported types: .key, .bif, .rim, .erf, .mod, .sav, .hak");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to extract archive: {ex.Message}");
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
        /// Extracts resources from an ERF/MOD/SAV/HAK archive.
        /// </summary>
        private static int ExtractErf(string erfPath, string outputDir, string filter, ILogger logger)
        {
            try
            {
                ERF erf = new ERFBinaryReader(erfPath).Load();
                if (erf == null)
                {
                    logger.Error("Failed to load ERF file");
                    return 1;
                }

                int extractedCount = 0;
                foreach (ERFResource resource in erf)
                {
                    string resref = resource.ResRef?.ToString() ?? "unknown";

                    // Apply filter
                    if (!MatchesFilter(resref, filter))
                    {
                        continue;
                    }

                    string ext = resource.ResType?.Extension ?? "bin";
                    string outputFile = Path.Combine(outputDir, $"{resref}.{ext}");

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    File.WriteAllBytes(outputFile, resource.Data);
                    extractedCount++;
                }

                logger.Info($"Extracted {extractedCount} resources from ERF archive");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to extract ERF: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Extracts resources from a RIM archive.
        /// </summary>
        private static int ExtractRim(string rimPath, string outputDir, string filter, ILogger logger)
        {
            try
            {
                RIM rim = new RIMBinaryReader(rimPath).Load();
                if (rim == null)
                {
                    logger.Error("Failed to load RIM file");
                    return 1;
                }

                int extractedCount = 0;
                foreach (RIMResource resource in rim)
                {
                    string resref = resource.ResRef?.ToString() ?? "unknown";

                    // Apply filter
                    if (!MatchesFilter(resref, filter))
                    {
                        continue;
                    }

                    string ext = resource.ResType?.Extension ?? "bin";
                    string outputFile = Path.Combine(outputDir, $"{resref}.{ext}");

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    File.WriteAllBytes(outputFile, resource.Data);
                    extractedCount++;
                }

                logger.Info($"Extracted {extractedCount} resources from RIM archive");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to extract RIM: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Extracts resources from a BIF file (requires KEY for resource names).
        /// </summary>
        private static int ExtractBif(string bifPath, string outputDir, string filter, string keyFile, ILogger logger)
        {
            try
            {
                // Determine KEY file path
                string keyPath;
                if (!string.IsNullOrEmpty(keyFile))
                {
                    keyPath = Path.GetFullPath(keyFile);
                }
                else
                {
                    keyPath = Path.Combine(Path.GetDirectoryName(bifPath), "chitin.key");
                }

                KEY key = null;
                if (File.Exists(keyPath))
                {
                    try
                    {
                        key = KEYAuto.ReadKey(keyPath);
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"KEY file not found or invalid: {keyPath}. Resources will have numeric names. Error: {ex.Message}");
                    }
                }
                else
                {
                    logger.Warning($"KEY file not found: {keyPath}. Resources will have numeric names.");
                }

                // Load BIF file
                BIF bif = new BIFBinaryReader(bifPath).Load();
                if (bif == null)
                {
                    logger.Error("Failed to load BIF file");
                    return 1;
                }

                // Build resource name lookup from KEY if available
                Dictionary<int, KeyEntry> resourceLookup = null;
                if (key != null)
                {
                    resourceLookup = new Dictionary<int, KeyEntry>();
                    foreach (KeyEntry keyEntry in key.KeyEntries)
                    {
                        // Extract BIF index and resource index from resource ID
                        int bifIndex = (int)(keyEntry.ResourceId >> 20) & 0xFFF;
                        int resIndex = (int)(keyEntry.ResourceId & 0xFFFFF);

                        // Only include entries for this BIF (we need to know which BIF this is)
                        // TODO: STUB - For now, we'll match by checking if the resource ID could belong to this BIF
                        // TODO:  This is a simplified approach - full implementation would need to track BIF index
                        resourceLookup[resIndex] = keyEntry;
                    }
                }

                int extractedCount = 0;
                int resourceIndex = 0;
                foreach (BIFResource resource in bif.Resources)
                {
                    string resref;
                    ResourceType restype = resource.ResType ?? ResourceType.INVALID;

                    // Try to get name from KEY lookup
                    if (resourceLookup != null && resourceLookup.TryGetValue(resourceIndex, out KeyEntry keyEntry))
                    {
                        resref = keyEntry.ResRef?.ToString() ?? $"resource_{resourceIndex:05d}";
                        restype = keyEntry.ResType ?? restype;
                    }
                    else
                    {
                        // Use resource's own ResRef if available, otherwise numeric name
                        resref = resource.ResRef?.ToString() ?? $"resource_{resourceIndex:05d}";
                    }

                    // Apply filter
                    if (!MatchesFilter(resref, filter))
                    {
                        resourceIndex++;
                        continue;
                    }

                    string ext = restype?.Extension ?? "bin";
                    string outputFile = Path.Combine(outputDir, $"{resref}.{ext}");

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    File.WriteAllBytes(outputFile, resource.Data);
                    extractedCount++;
                    resourceIndex++;
                }

                logger.Info($"Extracted {extractedCount} resources");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to extract BIF: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Extracts resources from KEY/BIF archives.
        /// </summary>
        private static int ExtractKey(string keyPath, string outputDir, string filter, ILogger logger)
        {
            try
            {
                // Load KEY file
                KEY key = KEYAuto.ReadKey(keyPath);
                if (key == null)
                {
                    logger.Error("Failed to load KEY file");
                    return 1;
                }

                // Find BIF files
                string searchDir = Path.GetDirectoryName(keyPath);
                Dictionary<int, string> bifFiles = new Dictionary<int, string>();

                for (int i = 0; i < key.BifEntries.Count; i++)
                {
                    BifEntry bifEntry = key.BifEntries[i];
                    string bifName = bifEntry.Filename;
                    string bifPath = Path.Combine(searchDir, bifName);

                    if (!File.Exists(bifPath))
                    {
                        // Try case-insensitive search
                        bool found = false;
                        if (Directory.Exists(searchDir))
                        {
                            foreach (string candidate in Directory.GetFiles(searchDir))
                            {
                                if (string.Equals(Path.GetFileName(candidate), bifName, StringComparison.OrdinalIgnoreCase))
                                {
                                    bifPath = candidate;
                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (!found)
                        {
                            logger.Warning($"BIF file not found: {bifName}");
                            continue;
                        }
                    }

                    bifFiles[i] = bifPath;
                }

                // Extract from each BIF
                int extractedCount = 0;
                HashSet<string> seenBifs = new HashSet<string>();

                foreach (KeyValuePair<int, string> kvp in bifFiles)
                {
                    int bifIndex = kvp.Key;
                    string bifPath = kvp.Value;

                    if (!seenBifs.Contains(bifPath))
                    {
                        logger.Info($"Extracting from BIF: {Path.GetFileName(bifPath)}");
                        seenBifs.Add(bifPath);
                    }

                    // Load BIF
                    BIF bif = new BIFBinaryReader(bifPath).Load();
                    if (bif == null)
                    {
                        logger.Warning($"Failed to load BIF: {bifPath}");
                        continue;
                    }

                    // Build resource lookup for this BIF
                    Dictionary<int, KeyEntry> resourceLookup = new Dictionary<int, KeyEntry>();
                    foreach (KeyEntry keyEntry in key.KeyEntries)
                    {
                        // Extract BIF index and resource index from resource ID
                        int entryBifIndex = (int)(keyEntry.ResourceId >> 20) & 0xFFF;
                        int resIndex = (int)(keyEntry.ResourceId & 0xFFFFF);

                        if (entryBifIndex == bifIndex)
                        {
                            resourceLookup[resIndex] = keyEntry;
                        }
                    }

                    // Extract resources
                    int resourceIndex = 0;
                    foreach (BIFResource resource in bif.Resources)
                    {
                        string resref;
                        ResourceType restype = resource.ResType ?? ResourceType.INVALID;

                        // Get name from KEY lookup
                        if (resourceLookup.TryGetValue(resourceIndex, out KeyEntry keyEntry))
                        {
                            resref = keyEntry.ResRef?.ToString() ?? $"resource_{resourceIndex:05d}";
                            restype = keyEntry.ResType ?? restype;
                        }
                        else
                        {
                            resref = resource.ResRef?.ToString() ?? $"resource_{resourceIndex:05d}";
                        }

                        // Apply filter
                        if (!MatchesFilter(resref, filter))
                        {
                            resourceIndex++;
                            continue;
                        }

                        string ext = restype?.Extension ?? "bin";
                        string bifStem = Path.GetFileNameWithoutExtension(bifPath);
                        string outputFile = Path.Combine(outputDir, bifStem, $"{resref}.{ext}");

                        Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                        File.WriteAllBytes(outputFile, resource.Data);
                        extractedCount++;
                        resourceIndex++;
                    }
                }

                logger.Info($"Extracted {extractedCount} resources");
                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to extract KEY/BIF: {ex.Message}");
                return 1;
            }
        }
    }
}
