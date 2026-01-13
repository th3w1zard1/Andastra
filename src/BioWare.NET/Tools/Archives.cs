using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.KEY;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.BIF;
using JetBrains.Annotations;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:33-47
    // Original: def matches_filter(text: str, pattern: str) -> bool:
    /// <summary>
    /// Check if text matches filter pattern (supports wildcards).
    /// </summary>
    public static class ArchiveHelpers
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:33-47
        // Original: def matches_filter(text: str, pattern: str) -> bool:
        public static bool MatchesFilter(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            {
                return false;
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:50-93
        // Original: def extract_erf(...) -> Iterator[tuple[ArchiveResource, Path]]:
        /// <summary>
        /// Extract resources from an ERF/MOD/SAV/HAK archive.
        /// This is a generator that yields (resource, output_path) tuples for each resource
        /// that matches the filter. The caller is responsible for writing the resource data.
        /// </summary>
        public static IEnumerable<(ArchiveResource resource, string outputPath)> ExtractErf(
            string erfPath,
            string outputDir,
            string filterPattern = null,
            Func<ArchiveResource, bool> resourceFilter = null)
        {
            var erf = ERFAuto.ReadErf(erfPath);

            foreach (var erfResource in erf)
            {
                string resref = erfResource.ResRef?.ToString() ?? "unknown";
                var archiveResource = new ArchiveResource(erfResource.ResRef, erfResource.ResType, erfResource.Data);

                // Apply filters
                if (resourceFilter != null)
                {
                    if (!resourceFilter(archiveResource))
                    {
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(filterPattern) && !MatchesFilter(resref, filterPattern))
                {
                    continue;
                }

                string ext = erfResource.ResType?.Extension ?? "bin";
                string outputFile = Path.Combine(outputDir, $"{resref}.{ext}");

                yield return (archiveResource, outputFile);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:96-139
        // Original: def extract_rim(...) -> Iterator[tuple[ArchiveResource, Path]]:
        /// <summary>
        /// Extract resources from a RIM archive.
        /// </summary>
        public static IEnumerable<(ArchiveResource resource, string outputPath)> ExtractRim(
            string rimPath,
            string outputDir,
            string filterPattern = null,
            Func<ArchiveResource, bool> resourceFilter = null)
        {
            var rim = RIMAuto.ReadRim(rimPath);

            foreach (var rimResource in rim)
            {
                string resref = rimResource.ResRef?.ToString() ?? "unknown";
                var archiveResource = new ArchiveResource(rimResource.ResRef, rimResource.ResType, rimResource.Data);

                // Apply filters
                if (resourceFilter != null)
                {
                    if (!resourceFilter(archiveResource))
                    {
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(filterPattern) && !MatchesFilter(resref, filterPattern))
                {
                    continue;
                }

                string ext = rimResource.ResType?.Extension ?? "bin";
                string outputFile = Path.Combine(outputDir, $"{resref}.{ext}");

                yield return (archiveResource, outputFile);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:142-189
        // Original: def extract_bif(...) -> Iterator[tuple[ArchiveResource, Path]]:
        /// <summary>
        /// Extract resources from a BIF file.
        /// </summary>
        public static IEnumerable<(ArchiveResource resource, string outputPath)> ExtractBif(
            string bifPath,
            string outputDir,
            string keyPath = null,
            string filterPattern = null,
            Func<ArchiveResource, bool> resourceFilter = null)
        {
            BIF bifData;
            var reader = new BIFBinaryReader(bifPath);
            bifData = reader.Load();

            // Merge KEY data for resource names if KEY file is provided
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:171-174
            // Original: bif_data = read_bif(bif_file, key_source=key_source)
            if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
            {
                MergeKeyDataIntoBif(bifData, keyPath, bifPath);
            }

            int i = 0;
            foreach (var bifResource in bifData)
            {
                string resref = bifResource.ResRef?.ToString() ?? $"resource_{i:D5}";
                var archiveResource = new ArchiveResource(bifResource.ResRef, bifResource.ResType, bifResource.Data);

                // Apply filters
                if (resourceFilter != null)
                {
                    if (!resourceFilter(archiveResource))
                    {
                        i++;
                        continue;
                    }
                }
                else if (!string.IsNullOrEmpty(filterPattern) && !MatchesFilter(resref, filterPattern))
                {
                    i++;
                    continue;
                }

                string ext = bifResource.ResType?.Extension ?? "bin";
                string outputFile = Path.Combine(outputDir, $"{resref}.{ext}");

                yield return (archiveResource, outputFile);
                i++;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:192-264
        // Original: def extract_key_bif(...) -> Iterator[tuple[ArchiveResource, Path, Path]]:
        /// <summary>
        /// Extract resources from KEY/BIF archives.
        /// This is a generator that yields (resource, output_path, bif_path) tuples for each resource
        /// that matches the filter. The caller is responsible for writing the resource data.
        /// </summary>
        public static IEnumerable<(ArchiveResource resource, string outputPath, string bifPath)> ExtractKeyBif(
            string keyPath,
            string outputDir,
            string bifSearchDir = null,
            string filterPattern = null,
            Func<ArchiveResource, bool> resourceFilter = null)
        {
            var keyData = KEYAuto.ReadKey(keyPath);

            // Find BIF files
            string searchDir = bifSearchDir ?? Path.GetDirectoryName(keyPath);
            var bifFiles = new Dictionary<int, string>();

            for (int idx = 0; idx < keyData.BifEntries.Count; idx++)
            {
                var bifEntry = keyData.BifEntries[idx];
                string bifName = bifEntry.Filename;
                string bifPath = Path.Combine(searchDir, bifName);

                if (!File.Exists(bifPath))
                {
                    // Try case-insensitive search
                    if (Directory.Exists(searchDir))
                    {
                        var candidates = Directory.GetFiles(searchDir, "*.bif", SearchOption.TopDirectoryOnly)
                            .Concat(Directory.GetFiles(searchDir, "*.bzf", SearchOption.TopDirectoryOnly));
                        foreach (string candidate in candidates)
                        {
                            if (string.Equals(Path.GetFileName(candidate), bifName, StringComparison.OrdinalIgnoreCase))
                            {
                                bifPath = candidate;
                                break;
                            }
                        }
                    }
                    if (!File.Exists(bifPath))
                    {
                        continue; // Skip missing BIF files
                    }
                }

                bifFiles[idx] = bifPath;
            }

            // Extract from each BIF
            foreach (var kvp in bifFiles)
            {
                int bifIndex = kvp.Key;
                string bifPath = kvp.Value;

                var reader = new BIFBinaryReader(bifPath);
                BIF bifData = reader.Load();
                // Merge KEY data for resource names
                MergeKeyDataIntoBif(bifData, keyPath, bifPath);

                int i = 0;
                foreach (var bifResource in bifData)
                {
                    string resref = bifResource.ResRef?.ToString() ?? $"resource_{i:D5}";
                    var archiveResource = new ArchiveResource(bifResource.ResRef, bifResource.ResType, bifResource.Data);

                    // Apply filters
                    if (resourceFilter != null)
                    {
                        if (!resourceFilter(archiveResource))
                        {
                            i++;
                            continue;
                        }
                    }
                    else if (!string.IsNullOrEmpty(filterPattern) && !MatchesFilter(resref, filterPattern))
                    {
                        i++;
                        continue;
                    }

                    string ext = bifResource.ResType?.Extension ?? "bin";
                    string bifStem = Path.GetFileNameWithoutExtension(bifPath);
                    string outputFile = Path.Combine(outputDir, bifStem, $"{resref}.{ext}");

                    yield return (archiveResource, outputFile, bifPath);
                    i++;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:267-279
        // Original: def list_erf(erf_path: Path) -> Iterator[ArchiveResource]:
        /// <summary>
        /// List all resources in an ERF/MOD/SAV/HAK archive.
        /// </summary>
        public static IEnumerable<ArchiveResource> ListErf(string erfPath)
        {
            var erfData = ERFAuto.ReadErf(erfPath);
            foreach (var erfResource in erfData)
            {
                yield return new ArchiveResource(erfResource.ResRef, erfResource.ResType, erfResource.Data);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:282-294
        // Original: def list_rim(rim_path: Path) -> Iterator[ArchiveResource]:
        /// <summary>
        /// List all resources in a RIM archive.
        /// </summary>
        public static IEnumerable<ArchiveResource> ListRim(string rimPath)
        {
            var rimData = RIMAuto.ReadRim(rimPath);
            foreach (var rimResource in rimData)
            {
                yield return new ArchiveResource(rimResource.ResRef, rimResource.ResType, rimResource.Data);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:297-318
        // Original: def list_bif(...) -> Iterator[ArchiveResource]:
        /// <summary>
        /// List all resources in a BIF file.
        /// </summary>
        public static IEnumerable<ArchiveResource> ListBif(string bifPath, string keyPath = null)
        {
            var reader = new BIFBinaryReader(bifPath);
            BIF bifData = reader.Load();
            // Merge KEY data for resource names if keyPath provided
            if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
            {
                MergeKeyDataIntoBif(bifData, keyPath, bifPath);
            }
            foreach (var bifResource in bifData)
            {
                yield return new ArchiveResource(bifResource.ResRef, bifResource.ResType, bifResource.Data);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:321-345
        // Original: def list_key(key_path: Path) -> tuple[list[str], list[tuple[str, str, int, int]]]:
        /// <summary>
        /// List BIF files and resources in a KEY file.
        /// </summary>
        public static (List<string> bifFiles, List<(string resref, string restypeExt, int bifIndex, int resIndex)> resources) ListKey(string keyPath)
        {
            var keyData = KEYAuto.ReadKey(keyPath);

            var bifFiles = keyData.BifEntries.Select(b => b.Filename).ToList();

            var resources = new List<(string, string, int, int)>();
            foreach (var keyEntry in keyData.KeyEntries)
            {
                string resref = keyEntry.ResRef.ToString();
                string restypeExt = keyEntry.ResType?.Extension ?? "?";
                resources.Add((resref, restypeExt, keyEntry.BifIndex, keyEntry.ResIndex));
            }

            return (bifFiles, resources);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:348-422
        // Original: def create_erf_from_directory(...) -> None:
        /// <summary>
        /// Create an ERF/MOD/SAV archive from a directory of files.
        /// </summary>
        public static void CreateErfFromDirectory(
            string inputDir,
            string outputPath,
            string erfType = "ERF",
            string fileFilter = null)
        {
            var typeMap = new Dictionary<string, ERFType>(StringComparer.OrdinalIgnoreCase)
            {
                { "ERF", ERFType.ERF },
                { "MOD", ERFType.MOD },
                { "SAV", ERFType.MOD } // SAV uses MOD signature
            };

            if (!typeMap.TryGetValue(erfType.ToUpperInvariant(), out ERFType erfEnumType))
            {
                erfEnumType = ERFType.ERF;
            }

            var erf = new ERF(erfEnumType);
            if (erfType.Equals("SAV", StringComparison.OrdinalIgnoreCase))
            {
                erf.IsSaveErf = true;
            }

            // Collect files from directory
            if (Directory.Exists(inputDir))
            {
                foreach (string filePath in Directory.GetFiles(inputDir))
                {
                    // Apply filter if specified
                    if (!string.IsNullOrEmpty(fileFilter) && !MatchesFilter(Path.GetFileName(filePath), fileFilter))
                    {
                        continue;
                    }

                    // Parse filename to get resref and extension
                    string stem = Path.GetFileNameWithoutExtension(filePath);
                    string ext = Path.GetExtension(filePath).TrimStart('.');

                    try
                    {
                        ResourceType restype = ResourceType.FromExtension(ext);
                        if (restype == ResourceType.INVALID || restype.IsInvalid)
                        {
                            continue; // Skip unknown file types
                        }

                        // Handle files with embedded type in stem (e.g., "model.123.mdl")
                        string resref;
                        if (stem.Contains("."))
                        {
                            string[] parts = stem.Split('.');
                            // Check if last part is numeric (resource ID)
                            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _))
                            {
                                resref = string.Join(".", parts.Take(parts.Length - 1));
                            }
                            else
                            {
                                resref = stem;
                            }
                        }
                        else
                        {
                            resref = stem;
                        }

                        // Read file data and add to ERF
                        byte[] fileData = File.ReadAllBytes(filePath);
                        erf.SetData(new ResRef(resref), restype, fileData);
                    }
                    catch
                    {
                        // Skip files that can't be processed
                    }
                }
            }

            // Write ERF archive
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            ResourceType outputFormat = erfType.Equals("MOD", StringComparison.OrdinalIgnoreCase) ? ResourceType.MOD :
                erfType.Equals("SAV", StringComparison.OrdinalIgnoreCase) ? ResourceType.SAV : ResourceType.ERF;
            ERFAuto.WriteErf(erf, outputPath, outputFormat);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:425-485
        // Original: def create_rim_from_directory(...) -> None:
        /// <summary>
        /// Create a RIM archive from a directory of files.
        /// </summary>
        public static void CreateRimFromDirectory(
            string inputDir,
            string outputPath,
            string fileFilter = null)
        {
            var rim = new RIM();

            // Collect files from directory
            if (Directory.Exists(inputDir))
            {
                foreach (string filePath in Directory.GetFiles(inputDir))
                {
                    // Apply filter if specified
                    if (!string.IsNullOrEmpty(fileFilter) && !MatchesFilter(Path.GetFileName(filePath), fileFilter))
                    {
                        continue;
                    }

                    // Parse filename to get resref and extension
                    string stem = Path.GetFileNameWithoutExtension(filePath);
                    string ext = Path.GetExtension(filePath).TrimStart('.');

                    try
                    {
                        ResourceType restype = ResourceType.FromExtension(ext);
                        if (restype == ResourceType.INVALID || restype.IsInvalid)
                        {
                            continue; // Skip unknown file types
                        }

                        // Handle files with embedded type in stem
                        string resref;
                        if (stem.Contains("."))
                        {
                            string[] parts = stem.Split('.');
                            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _))
                            {
                                resref = string.Join(".", parts.Take(parts.Length - 1));
                            }
                            else
                            {
                                resref = stem;
                            }
                        }
                        else
                        {
                            resref = stem;
                        }

                        // Read file data and add to RIM
                        byte[] fileData = File.ReadAllBytes(filePath);
                        rim.SetData(new ResRef(resref), restype, fileData);
                    }
                    catch
                    {
                        // Skip files that can't be processed
                    }
                }
            }

            // Write RIM archive
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            RIMAuto.WriteRim(rim, outputPath, ResourceType.RIM);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:488-550
        // Original: def search_in_erf(...) -> Iterator[tuple[str, str]]:
        /// <summary>
        /// Search for resources in an ERF archive by name or content.
        /// </summary>
        public static IEnumerable<(string resref, string restype)> SearchInErf(
            string erfPath,
            string pattern,
            bool caseSensitive = false,
            bool searchContent = false)
        {
            var erfData = ERFAuto.ReadErf(erfPath);

            // Compile pattern
            string searchPattern = caseSensitive ? pattern : pattern.ToLowerInvariant();
            bool useWildcard = pattern.Contains("*") || pattern.Contains("?");
            Regex regexPattern = null;
            if (!useWildcard)
            {
                regexPattern = new Regex(searchPattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            }

            foreach (var resource in erfData)
            {
                string resref = resource.ResRef?.ToString() ?? "unknown";
                string restype = resource.ResType?.Extension ?? "bin";

                // Search in name
                string nameToSearch = caseSensitive ? resref : resref.ToLowerInvariant();
                bool nameMatch = false;
                if (useWildcard)
                {
                    nameMatch = MatchesFilter(nameToSearch, searchPattern);
                }
                else if (regexPattern != null)
                {
                    nameMatch = regexPattern.IsMatch(nameToSearch);
                }

                if (nameMatch)
                {
                    yield return (resref, restype);
                }

                // Search in content if requested
                if (searchContent)
                {
                    bool contentMatch = false;
                    try
                    {
                        byte[] data = resource.Data;
                        string content = System.Text.Encoding.UTF8.GetString(data);
                        string contentToSearch = caseSensitive ? content : content.ToLowerInvariant();
                        if (useWildcard)
                        {
                            contentMatch = contentToSearch.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else if (regexPattern != null)
                        {
                            contentMatch = regexPattern.IsMatch(contentToSearch);
                        }
                    }
                    catch
                    {
                        // Skip binary resources that can't be decoded
                    }

                    if (contentMatch && !nameMatch)
                    {
                        yield return (resref, restype);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:553-609
        // Original: def search_in_rim(...) -> Iterator[tuple[str, str]]:
        /// <summary>
        /// Search for resources in a RIM archive by name or content.
        /// </summary>
        public static IEnumerable<(string resref, string restype)> SearchInRim(
            string rimPath,
            string pattern,
            bool caseSensitive = false,
            bool searchContent = false)
        {
            var rimData = RIMAuto.ReadRim(rimPath);

            // Compile pattern
            string searchPattern = caseSensitive ? pattern : pattern.ToLowerInvariant();
            bool useWildcard = pattern.Contains("*") || pattern.Contains("?");
            Regex regexPattern = null;
            if (!useWildcard)
            {
                regexPattern = new Regex(searchPattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
            }

            foreach (var resource in rimData)
            {
                string resref = resource.ResRef?.ToString() ?? "unknown";
                string restype = resource.ResType?.Extension ?? "bin";

                // Search in name
                string nameToSearch = caseSensitive ? resref : resref.ToLowerInvariant();
                bool nameMatch = false;
                if (useWildcard)
                {
                    nameMatch = MatchesFilter(nameToSearch, searchPattern);
                }
                else if (regexPattern != null)
                {
                    nameMatch = regexPattern.IsMatch(nameToSearch);
                }

                if (nameMatch)
                {
                    yield return (resref, restype);
                    continue; // Skip content search if name already matched
                }

                // Search in content if requested
                if (searchContent)
                {
                    bool contentMatch = false;
                    try
                    {
                        byte[] data = resource.Data;
                        string content = System.Text.Encoding.UTF8.GetString(data);
                        string contentToSearch = caseSensitive ? content : content.ToLowerInvariant();
                        if (useWildcard)
                        {
                            contentMatch = contentToSearch.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        else if (regexPattern != null)
                        {
                            contentMatch = regexPattern.IsMatch(contentToSearch);
                        }
                    }
                    catch
                    {
                        // Skip binary resources that can't be decoded
                    }

                    if (contentMatch)
                    {
                        yield return (resref, restype);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:612-669
        // Original: def get_resource_from_archive(...) -> bytes | None:
        /// <summary>
        /// Get a resource's data from an archive.
        /// </summary>
        [CanBeNull]
        public static byte[] GetResourceFromArchive(
            string archivePath,
            string resref,
            string restype = null)
        {
            string suffix = Path.GetExtension(archivePath).ToLowerInvariant();

            // Determine resource type
            ResourceType resourceType = null;
            if (!string.IsNullOrEmpty(restype))
            {
                try
                {
                    resourceType = ResourceType.FromExtension(restype);
                }
                catch
                {
                    return null;
                }
            }

            // Read archive
            if (suffix == ".erf" || suffix == ".mod" || suffix == ".sav" || suffix == ".hak")
            {
                var erfData = ERFAuto.ReadErf(archivePath);
                if (resourceType != null)
                {
                    return erfData.Get(resref, resourceType);
                }
                // Try common types if not specified
                foreach (ResourceType commonType in new[] { ResourceType.NSS, ResourceType.DLG, ResourceType.UTC, ResourceType.UTI })
                {
                    byte[] result = erfData.Get(resref, commonType);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            else if (suffix == ".rim")
            {
                var rimData = RIMAuto.ReadRim(archivePath);
                if (resourceType != null)
                {
                    return rimData.Get(resref, resourceType);
                }
                // Try common types if not specified
                foreach (ResourceType commonType in new[] { ResourceType.NSS, ResourceType.DLG, ResourceType.UTC, ResourceType.UTI })
                {
                    byte[] result = rimData.Get(resref, commonType);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/archives.py:672-749
        // Original: def create_key_from_directory(...) -> None:
        /// <summary>
        /// Create a KEY file from a directory containing BIF files.
        /// </summary>
        public static void CreateKeyFromDirectory(
            string inputDir,
            string bifDir,
            string outputPath,
            string fileFilter = null)
        {
            var key = new KEY();

            // Collect all BIF files
            var bifFiles = new List<string>();
            if (Directory.Exists(inputDir))
            {
                foreach (string filePath in Directory.GetFiles(inputDir, "*.bif", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(inputDir, "*.bzf", SearchOption.AllDirectories)))
                {
                    // Apply filter if specified
                    if (!string.IsNullOrEmpty(fileFilter) && !MatchesFilter(Path.GetFileName(filePath), fileFilter))
                    {
                        continue;
                    }

                    bifFiles.Add(filePath);
                }
            }

            // Process each BIF file
            foreach (string bifPath in bifFiles)
            {
                // Calculate relative path from bifDir
                string bifFilename;
                try
                {
                    string relPath = Path.GetRelativePath(bifDir, bifPath);
                    bifFilename = relPath.Replace("\\", "/");
                }
                catch
                {
                    // If not relative, use just the filename
                    bifFilename = Path.GetFileName(bifPath);
                }

                var fileInfo = new FileInfo(bifPath);
                int bifSize = (int)fileInfo.Length;

                // Read BIF to get resources
                var reader = new BIFBinaryReader(bifPath);
                BIF bifData = reader.Load();

                // Create BIF entry
                var bifEntry = new BifEntry
                {
                    Filename = bifFilename,
                    Filesize = bifSize
                };
                int bifIndex = key.BifEntries.Count;
                key.BifEntries.Add(bifEntry);

                // Add resource entries
                int i = 0;
                foreach (var resource in bifData)
                {
                    string resref = resource.ResRef?.ToString() ?? $"resource_{i:D5}";
                    ResourceType restype = resource.ResType ?? ResourceType.INVALID;

                    // Resource ID: top 12 bits = BIF index, bottom 20 bits = resource index
                    uint resourceId = (uint)((bifIndex << 20) | i);

                    var keyEntry = new KeyEntry
                    {
                        ResRef = new ResRef(resref),
                        ResType = restype,
                        ResourceId = resourceId
                    };
                    key.KeyEntries.Add(keyEntry);
                    i++;
                }
            }

            // Build lookup tables and write KEY
            key.BuildLookupTables();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            KEYAuto.WriteKey(key, outputPath);
        }

        /// <summary>
        /// Merges KEY file data into BIF resources to populate ResRef names.
        /// </summary>
        /// <param name="bifData">The BIF data to merge KEY data into</param>
        /// <param name="keyPath">Path to the KEY file</param>
        /// <param name="bifPath">Path to the BIF file (used to determine BIF index)</param>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py
        /// Original: read_bif accepts key_source parameter and merges resource names
        /// BIF resources have ResnameKeyIndex which matches KEY entries' ResourceId
        /// </remarks>
        private static void MergeKeyDataIntoBif(BIF bifData, string keyPath, string bifPath)
        {
            if (bifData == null || string.IsNullOrEmpty(keyPath) || !File.Exists(keyPath))
            {
                return;
            }

            // Load KEY file
            KEY keyData = KEYAuto.ReadKey(keyPath);
            if (keyData == null)
            {
                return;
            }

            // Find the BIF index for this BIF file
            string bifFileName = Path.GetFileName(bifPath);
            int bifIndex = -1;
            for (int i = 0; i < keyData.BifEntries.Count; i++)
            {
                if (string.Equals(keyData.BifEntries[i].Filename, bifFileName, StringComparison.OrdinalIgnoreCase))
                {
                    bifIndex = i;
                    break;
                }
            }

            if (bifIndex < 0)
            {
                // BIF not found in KEY file, cannot merge
                return;
            }

            // Create lookup dictionary: ResourceId -> KeyEntry for this BIF
            var keyEntryLookup = new Dictionary<uint, KeyEntry>();
            foreach (KeyEntry keyEntry in keyData.KeyEntries)
            {
                if (keyEntry.BifIndex == bifIndex)
                {
                    keyEntryLookup[keyEntry.ResourceId] = keyEntry;
                }
            }

            // Merge KEY data into BIF resources
            foreach (BIFResource bifResource in bifData.Resources)
            {
                uint resourceId = (uint)bifResource.ResnameKeyIndex;
                if (keyEntryLookup.TryGetValue(resourceId, out KeyEntry keyEntry))
                {
                    // Set ResRef from KEY entry
                    bifResource.ResRef = keyEntry.ResRef;
                    // Note: ResType should already match, but we verify it
                    if (bifResource.ResType != keyEntry.ResType)
                    {
                        // Log mismatch but don't change - BIF data is authoritative for type
                        System.Diagnostics.Debug.WriteLine(
                            $"KEY and BIF disagree on type for resource ID {resourceId}: KEY={keyEntry.ResType}, BIF={bifResource.ResType}");
                    }
                }
            }
        }
    }
}
