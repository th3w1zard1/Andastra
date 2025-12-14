// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:3294-3386
// Original: def run_differ_from_args_impl(...): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Common;
using CSharpKOTOR.Installation;
using KotorDiff.NET.Generator;
using KotorDiff.NET.Resolution;
using KotorDiff.NET.Cache;

namespace KotorDiff.NET.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:3294-3386
    // Original: def run_differ_from_args_impl(...): ...
    public static class DiffEngine
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:3294-3386
        // Original: def run_differ_from_args_impl(...): ...
        public static bool? RunDifferFromArgsImpl(
            List<object> filesAndFoldersAndInstallations,
            List<string> filters = null,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            try
            {
                if (filesAndFoldersAndInstallations == null || filesAndFoldersAndInstallations.Count < 2)
                {
                    string msg = $"At least 2 paths required for comparison, got {filesAndFoldersAndInstallations?.Count ?? 0}";
                    throw new ArgumentException(msg);
                }

                if (logFunc == null)
                {
                    logFunc = Console.WriteLine;
                }

                logFunc($"Starting {filesAndFoldersAndInstallations.Count}-way comparison...");
                for (int idx = 0; idx < filesAndFoldersAndInstallations.Count; idx++)
                {
                    object path = filesAndFoldersAndInstallations[idx];
                    string pathType = "Path"; // TODO: Determine if Installation, Folder, or File
                    logFunc($"  Path {idx}: {path} ({pathType})");
                }
                logFunc("-------------------------------------------");
                logFunc("");

                // Validate all paths exist
                logFunc("[DEBUG] Validating paths...");
                if (!ValidatePaths(filesAndFoldersAndInstallations, logFunc))
                {
                    logFunc("[ERROR] Path validation failed");
                    return null;
                }
                logFunc("[DEBUG] Path validation successful");

                // Load installations and create PathInfo objects
                logFunc("[DEBUG] Loading installations...");
                var pathInfos = LoadInstallations(filesAndFoldersAndInstallations, logFunc);
                logFunc($"[DEBUG] Loaded {pathInfos.Count} PathInfo objects");

                // For Installation-to-Installation comparison, use resolution-aware comparison
                var allInstallations = filesAndFoldersAndInstallations.OfType<Installation>().ToList();
                logFunc($"[DEBUG] Found {allInstallations.Count} Installation objects");

                if (allInstallations.Count >= 2)
                {
                    logFunc("Detected multiple installations - using resolution-aware comparison...");
                    return Resolution.InstallationDiffWithResolution.DiffInstallationsWithResolution(
                        filesAndFoldersAndInstallations,
                        filters: filters,
                        logFunc: logFunc,
                        compareHashes: compareHashes,
                        modificationsByType: modificationsByType,
                        incrementalWriter: incrementalWriter);
                }

                // Mixed path types or non-Installation comparison
                logFunc("[DEBUG] Using non-Installation comparison (folder/file diffing)...");

                // Collect all resources from all paths
                logFunc("[DEBUG] Collecting resources...");
                var allResources = CollectAllResources(pathInfos, filters: filters, logFunc: logFunc);
                logFunc($"[DEBUG] Collected {allResources.Count} unique resources");

                // Compare resources across all paths
                logFunc("[DEBUG] Starting n-way comparison...");
                bool? result = CompareResourcesNWay(
                    allResources,
                    pathInfos,
                    logFunc: logFunc,
                    compareHashes: compareHashes,
                    modificationsByType: modificationsByType,
                    incrementalWriter: incrementalWriter);

                logFunc("[DEBUG] Comparison complete");
                return result;
            }
            catch (Exception e)
            {
                if (logFunc != null)
                {
                    logFunc($"[CRITICAL ERROR] Exception in RunDifferFromArgsImpl: {e.GetType().Name}: {e.Message}");
                    logFunc("Full traceback:");
                    logFunc(e.StackTrace);
                }
                return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2963-2978
        // Original: def validate_paths(...): ...
        private static bool ValidatePaths(List<object> paths, Action<string> logFunc)
        {
            if (paths == null)
            {
                return false;
            }

            foreach (object path in paths)
            {
                if (path is string pathStr)
                {
                    if (!Directory.Exists(pathStr) && !File.Exists(pathStr))
                    {
                        logFunc($"[ERROR] Path does not exist: {pathStr}");
                        return false;
                    }
                }
                else if (path is DirectoryInfo dirInfo)
                {
                    if (!dirInfo.Exists)
                    {
                        logFunc($"[ERROR] Directory does not exist: {dirInfo.FullName}");
                        return false;
                    }
                }
                else if (path is FileInfo fileInfo)
                {
                    if (!fileInfo.Exists)
                    {
                        logFunc($"[ERROR] File does not exist: {fileInfo.FullName}");
                        return false;
                    }
                }
            }

            return true;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2979-3022
        // Original: def load_installations(...): ...
        private static List<PathInfo> LoadInstallations(List<object> filesAndFoldersAndInstallations, Action<string> logFunc)
        {
            var pathInfos = new List<PathInfo>();

            for (int idx = 0; idx < filesAndFoldersAndInstallations.Count; idx++)
            {
                object path = filesAndFoldersAndInstallations[idx];

                if (path is Installation existingInstallation)
                {
                    var pathInfo = PathInfo.FromPathOrInstallation(existingInstallation, idx);
                    pathInfos.Add(pathInfo);
                    logFunc($"Path {idx}: Using existing Installation object: {existingInstallation.Path}");
                }
                else if (path is string pathStr)
                {
                    if (DiffEngineUtils.IsKotorInstallDir(pathStr))
                    {
                        logFunc($"Path {idx}: Loading installation from: {pathStr}");
                        try
                        {
                            var newInstallation = new Installation(pathStr);
                            var pathInfo = PathInfo.FromPathOrInstallation(newInstallation, idx);
                            pathInfos.Add(pathInfo);
                        }
                        catch (Exception e)
                        {
                            logFunc($"Error loading installation from path {idx} '{pathStr}': {e.GetType().Name}: {e.Message}");
                            // Create PathInfo for the raw path anyway
                            var pathInfo = PathInfo.FromPathOrInstallation(pathStr, idx);
                            pathInfos.Add(pathInfo);
                        }
                    }
                    else
                    {
                        var pathInfo = PathInfo.FromPathOrInstallation(pathStr, idx);
                        pathInfos.Add(pathInfo);
                        string pathType = File.Exists(pathStr) ? "File" : (Directory.Exists(pathStr) ? "Folder" : "Unknown");
                        logFunc($"Path {idx}: Using {pathType} path: {pathStr}");
                    }
                }
                else
                {
                    var pathInfo = PathInfo.FromPathOrInstallation(path, idx);
                    pathInfos.Add(pathInfo);
                }
            }

            return pathInfos;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:3059-3115
        // Original: def collect_all_resources(...): ...
        private static Dictionary<string, Dictionary<int, ComparableResource>> CollectAllResources(
            List<PathInfo> pathInfos,
            List<string> filters = null,
            Action<string> logFunc = null)
        {
            var allResources = new Dictionary<string, Dictionary<int, ComparableResource>>();

            foreach (var pathInfo in pathInfos)
            {
                logFunc?.Invoke($"Collecting resources from path {pathInfo.Index} ({pathInfo.Name})...");

                try
                {
                    var walker = new ResourceWalker(pathInfo.GetPath());
                    int resourceCount = 0;

                    foreach (var resource in walker.Walk())
                    {
                        // Apply filters if provided
                        if (filters != null && !ShouldIncludeInFilteredDiff(resource.Identifier, filters))
                        {
                            continue;
                        }

                        // Update resource with source index
                        resource.SourceIndex = pathInfo.Index;

                        // Add to collection
                        if (!allResources.ContainsKey(resource.Identifier))
                        {
                            allResources[resource.Identifier] = new Dictionary<int, ComparableResource>();
                        }
                        allResources[resource.Identifier][pathInfo.Index] = resource;
                        resourceCount++;
                    }

                    logFunc?.Invoke($"  Collected {resourceCount} resources from path {pathInfo.Index}");
                }
                catch (Exception e)
                {
                    logFunc?.Invoke($"Error collecting resources from path {pathInfo.Index}: {e.GetType().Name}: {e.Message}");
                    continue;
                }
            }

            logFunc?.Invoke($"Total unique resources across all paths: {allResources.Count}");
            return allResources;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:3118-3200
        // Original: def compare_resources_n_way(...): ...
        private static bool? CompareResourcesNWay(
            Dictionary<string, Dictionary<int, ComparableResource>> allResources,
            List<PathInfo> pathInfos,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            bool? isSameResult = true;
            int processedCount = 0;
            int diffCount = 0;

            logFunc?.Invoke($"Comparing {allResources.Count} unique resources across {pathInfos.Count} paths...");

            foreach (var kvp in allResources)
            {
                string resourceId = kvp.Key;
                var pathData = kvp.Value;
                processedCount++;

                if (processedCount % 100 == 0)
                {
                    logFunc?.Invoke($"Progress: {processedCount}/{allResources.Count} resources processed...");
                }

                // If resource only exists in one path, create install patch
                if (pathData.Count == 1)
                {
                    var firstKvp = pathData.First();
                    int pathIndex = firstKvp.Key;
                    var resource = firstKvp.Value;
                    var pathInfo = pathInfos[pathIndex];

                    logFunc?.Invoke($"\n[UNIQUE RESOURCE] {resourceId}");
                    logFunc?.Invoke($"  Only in path {pathIndex} ({pathInfo.Name})");
                    logFunc?.Invoke("  → Creating InstallList entry and patch");

                    // TODO: Generate install patch for unique resource
                    isSameResult = false;
                }
                else
                {
                    // Resource exists in multiple paths - compare them using DiffData
                    var resources = pathData.Values.ToList();
                    bool allIdentical = true;

                    // Use first resource as base, compare all others against it
                    var baseResource = resources[0];
                    int basePathIndex = pathData.First(entry => entry.Value == baseResource).Key;

                    for (int i = 1; i < resources.Count; i++)
                    {
                        var compareResource = resources[i];
                        int comparePathIndex = pathData.First(entry => entry.Value == compareResource).Key;

                        // Create DiffContext for comparison
                        string file1Rel = $"path{basePathIndex}/{baseResource.Identifier}";
                        string file2Rel = $"path{comparePathIndex}/{compareResource.Identifier}";
                        var ctx = new DiffContext(file1Rel, file2Rel, baseResource.Ext);

                        // Perform format-aware comparison
                        bool? diffResult = DiffData(
                            baseResource.Data,
                            compareResource.Data,
                            ctx,
                            compareHashes: compareHashes,
                            modificationsByType: modificationsByType,
                            logFunc: logFunc,
                            incrementalWriter: incrementalWriter);

                        if (diffResult == false)
                        {
                            allIdentical = false;
                            logFunc?.Invoke($"\n[DIFFERENT] {resourceId}");
                            logFunc?.Invoke($"  Differences found between path {basePathIndex} and path {comparePathIndex}");
                            diffCount++;
                        }
                        else if (diffResult == null)
                        {
                            // Error occurred, mark as different
                            allIdentical = false;
                            isSameResult = null;
                        }
                    }

                    if (!allIdentical)
                    {
                        isSameResult = false;
                    }
                }
            }

            logFunc?.Invoke($"\nComparison complete: {processedCount} resources processed, {diffCount} differences found");
            return isSameResult;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2252-2288
        // Original: def should_include_in_filtered_diff(...): ...
        private static bool ShouldIncludeInFilteredDiff(string filePath, List<string> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return true;
            }

            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            string filePathLower = filePath.ToLowerInvariant();

            foreach (string filterPattern in filters)
            {
                string filterLower = filterPattern.ToLowerInvariant();
                string filterName = Path.GetFileName(filterLower);

                // Direct filename match
                if (filterName == fileName)
                {
                    return true;
                }

                // Check if filter name appears in path
                if (filePathLower.Contains(filterName))
                {
                    return true;
                }

                // Module name match (for .rim/.mod/.erf files)
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".rim" || ext == ".mod" || ext == ".erf")
                {
                    try
                    {
                        string root = DiffEngineUtils.GetModuleRoot(filePath);
                        if (filterLower == root.ToLowerInvariant())
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue to next filter
                    }
                }
            }

            return false;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1236-1701
        // Original: def diff_data(...): ...
        public static bool? DiffData(
            byte[] data1,
            byte[] data2,
            DiffContext context,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null,
            Action<string> logFunc = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            string where = context.Where;

            // Fast path: identical byte arrays
            if (data1.SequenceEqual(data2))
            {
                return true;
            }

            // Handle GFF types first (special handling)
            var gffTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "utc", "uti", "utp", "ute", "utm", "utd", "utw", "dlg", "are", "git", "ifo", "gui", "jrl", "fac", "gff"
            };

            if (gffTypes.Contains(context.Ext))
            {
                return DiffGffData(data1, data2, context, modificationsByType, logFunc, incrementalWriter);
            }

            // Handle 2DA files
            if (context.Ext.Equals("2da", StringComparison.OrdinalIgnoreCase))
            {
                return DiffTwoDaData(data1, data2, context, modificationsByType, logFunc, compareHashes, incrementalWriter);
            }

            // Handle TLK files
            if (context.Ext.Equals("tlk", StringComparison.OrdinalIgnoreCase))
            {
                return DiffTlkData(data1, data2, context, modificationsByType, logFunc, incrementalWriter);
            }

            // Handle SSF files
            if (context.Ext.Equals("ssf", StringComparison.OrdinalIgnoreCase))
            {
                return DiffSsfData(data1, data2, context, modificationsByType, logFunc, compareHashes, incrementalWriter);
            }

            // Handle text files
            var binaryFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ncs", "mdl", "mdx", "wok", "pwk", "dwk", "tga", "tpc", "txi", "wav", "bik",
                "erf", "rim", "mod", "sav"
            };

            if (!binaryFormats.Contains(context.Ext) && DiffEngineUtils.IsTextContent(data1) && DiffEngineUtils.IsTextContent(data2))
            {
                logFunc($"Comparing text content for '{where}'");
                return DiffEngineUtils.CompareTextContent(data1, data2, where) ? (bool?)true : false;
            }

            // Fallback to hash comparison for binary content
            if (compareHashes)
            {
                string hash1 = DiffEngineUtils.CalculateSha256(data1);
                string hash2 = DiffEngineUtils.CalculateSha256(data2);
                if (hash1 != hash2)
                {
                    logFunc($"'{where}': SHA256 is different");
                    return false;
                }
                return true;
            }

            // If not comparing hashes and not text, assume different
            return false;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1294-1411
        // Original: GFF handling in diff_data
        private static bool? DiffGffData(
            byte[] data1,
            byte[] data2,
            DiffContext context,
            ModificationsByType modificationsByType,
            Action<string> logFunc,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            try
            {
                // TODO: Load GFF files and compare
                // For now, use analyzer
                var analyzer = DiffAnalyzerFactory.GetAnalyzer("gff");
                if (analyzer != null)
                {
                    var result = analyzer.Analyze(data1, data2, context.Where);
                    if (result != null && modificationsByType != null)
                    {
                        // Add modifications
                        if (result is CSharpKOTOR.Mods.GFF.ModificationsGFF modGff)
                        {
                            string resourceName = Path.GetFileName(context.Where);
                            modGff.Destination = DiffEngineUtils.DetermineDestinationForSource(context.File2Rel);
                            modGff.SourceFile = resourceName;
                            modGff.SaveAs = resourceName;

                            if (incrementalWriter != null)
                            {
                                incrementalWriter.AddModification(modGff);
                            }
                            else
                            {
                                modificationsByType?.Gff.Add(modGff);
                            }

                            logFunc($"\n[PATCH] {context.Where}");
                            logFunc("  |-- !ReplaceFile: 0 (patch existing file, don't replace)");
                            logFunc($"  |-- Modifications: {modGff.Modifiers.Count} field/struct changes");
                        }
                        return false; // Differences found
                    }
                }

                // Fallback: simple byte comparison
                return data1.SequenceEqual(data2) ? (bool?)true : false;
            }
            catch (Exception e)
            {
                logFunc($"[Error] Failed to compare GFF '{context.Where}': {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1511-1546
        // Original: 2DA handling in diff_data
        private static bool? DiffTwoDaData(
            byte[] data1,
            byte[] data2,
            DiffContext context,
            ModificationsByType modificationsByType,
            Action<string> logFunc,
            bool compareHashes,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            try
            {
                var analyzer = DiffAnalyzerFactory.GetAnalyzer("2da");
                if (analyzer != null)
                {
                    var result = analyzer.Analyze(data1, data2, context.Where);
                    if (result != null)
                    {
                        if (result is CSharpKOTOR.Mods.TwoDA.Modifications2DA mod2da)
                        {
                            string resourceName = Path.GetFileName(context.Where);
                            mod2da.Destination = DiffEngineUtils.DetermineDestinationForSource(context.File2Rel);
                            mod2da.SourceFile = resourceName;

                            if (incrementalWriter != null)
                            {
                                incrementalWriter.AddModification(mod2da);
                            }
                            else if (modificationsByType != null)
                            {
                                modificationsByType.Twoda.Add(mod2da);
                            }
                            logFunc($"\n[PATCH] {context.Where}");
                            logFunc("  |-- !ReplaceFile: 0 (patch existing 2DA)");
                            logFunc($"  |-- Modifications: {mod2da.Modifiers.Count} row/column changes");
                        }
                        return false; // Differences found
                    }
                }

                // Fallback: hash comparison
                if (compareHashes)
                {
                    string hash1 = DiffEngineUtils.CalculateSha256(data1);
                    string hash2 = DiffEngineUtils.CalculateSha256(data2);
                    return hash1 == hash2 ? (bool?)true : false;
                }
                return false;
            }
            catch (Exception e)
            {
                logFunc($"[Error] Failed to compare 2DA '{context.Where}': {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1547-1587
        // Original: TLK handling in diff_data
        private static bool? DiffTlkData(
            byte[] data1,
            byte[] data2,
            DiffContext context,
            ModificationsByType modificationsByType,
            Action<string> logFunc,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            try
            {
                var analyzer = DiffAnalyzerFactory.GetAnalyzer("tlk");
                if (analyzer != null)
                {
                    var result = analyzer.Analyze(data1, data2, context.Where);
                    if (result != null)
                    {
                        // TLK analyzer returns tuple: (ModificationsTLK, strref_mappings)
                        if (result is ValueTuple<CSharpKOTOR.Mods.TLK.ModificationsTLK, Dictionary<int, int>> tuple)
                        {
                            var modTlk = tuple.Item1;

                            if (incrementalWriter != null)
                            {
                                incrementalWriter.AddModification(modTlk);
                            }
                            else if (modificationsByType != null)
                            {
                                modificationsByType.Tlk.Add(modTlk);
                            }
                            logFunc($"\n[PATCH] {context.Where}");
                            logFunc("  |-- Mode: Append entries (TSLPatcher design)");
                            logFunc($"  |-- Modifications: {modTlk.Modifiers.Count} TLK entries");
                            logFunc("  +-- tslpatchdata: append.tlk and/or replace.tlk will be generated");
                        }
                        return false; // Differences found
                    }
                }

                // Fallback: simple byte comparison
                return data1.SequenceEqual(data2) ? (bool?)true : false;
            }
            catch (Exception e)
            {
                logFunc($"[Error] Failed to compare TLK '{context.Where}': {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1588-1621
        // Original: SSF handling in diff_data
        private static bool? DiffSsfData(
            byte[] data1,
            byte[] data2,
            DiffContext context,
            ModificationsByType modificationsByType,
            Action<string> logFunc,
            bool compareHashes,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            try
            {
                var analyzer = DiffAnalyzerFactory.GetAnalyzer("ssf");
                if (analyzer != null)
                {
                    var result = analyzer.Analyze(data1, data2, context.Where);
                    if (result != null)
                    {
                        if (result is CSharpKOTOR.Mods.SSF.ModificationsSSF modSsf)
                        {
                            string resourceName = Path.GetFileName(context.Where);
                            modSsf.Destination = DiffEngineUtils.DetermineDestinationForSource(context.File2Rel);
                            modSsf.SourceFile = resourceName;

                            if (incrementalWriter != null)
                            {
                                incrementalWriter.AddModification(modSsf);
                            }
                            else if (modificationsByType != null)
                            {
                                modificationsByType.Ssf.Add(modSsf);
                            }
                            logFunc($"\n[PATCH] {context.Where}");
                            logFunc("  |-- !ReplaceFile: 0 (patch existing SSF)");
                            logFunc($"  |-- Modifications: {modSsf.Modifiers.Count} sound slot changes");
                        }
                        return false; // Differences found
                    }
                }

                // Fallback: hash comparison
                if (compareHashes)
                {
                    string hash1 = DiffEngineUtils.CalculateSha256(data1);
                    string hash2 = DiffEngineUtils.CalculateSha256(data2);
                    return hash1 == hash2 ? (bool?)true : false;
                }
                return false;
            }
            catch (Exception e)
            {
                logFunc($"[Error] Failed to compare SSF '{context.Where}': {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1704-1774
        // Original: def diff_files(...): ...
        public static bool? DiffFiles(
            string file1,
            string file2,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            if (!File.Exists(file1))
            {
                logFunc($"Missing file:\t{Path.GetFileName(file1)}");
                return false;
            }
            if (!File.Exists(file2))
            {
                logFunc($"Missing file:\t{Path.GetFileName(file2)}");
                return false;
            }

            // Check if this is a capsule file
            if (DiffEngineUtils.IsCapsuleFile(file1))
            {
                return DiffCapsuleFiles(
                    file1,
                    file2,
                    Path.GetFileName(file1),
                    Path.GetFileName(file2),
                    logFunc: logFunc,
                    compareHashes: compareHashes,
                    modificationsByType: modificationsByType);
            }

            // Regular file diff
            string ext = DiffEngineUtils.ExtOf(file1);
            var ctx = new DiffContext(Path.GetFileName(file1), Path.GetFileName(file2), ext);
            byte[] data1 = File.ReadAllBytes(file1);
            byte[] data2 = File.ReadAllBytes(file2);
            return DiffData(data1, data2, ctx, compareHashes, modificationsByType, logFunc, null);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1777-1903
        // Original: def diff_capsule_files(...): ...
        public static bool? DiffCapsuleFiles(
            string cFile1,
            string cFile2,
            string cFile1Rel,
            string cFile2Rel,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            // Check if we should use composite module loading
            bool useCompositeFile1 = DiffEngineUtils.ShouldUseCompositeForFile(cFile1, cFile2);
            bool useCompositeFile2 = DiffEngineUtils.ShouldUseCompositeForFile(cFile2, cFile1);

            if (useCompositeFile1)
            {
                string stem = Path.GetFileNameWithoutExtension(cFile1);
                logFunc($"Using composite module loading for {cFile1Rel} ({stem}.rim + {stem}._s.rim + {stem}._dlg.erf)");
            }
            if (useCompositeFile2)
            {
                string stem = Path.GetFileNameWithoutExtension(cFile2);
                logFunc($"Using composite module loading for {cFile2Rel} ({stem}.rim + {stem}._s.rim + {stem}._dlg.erf)");
            }

            // TODO: Implement composite module loading
            // For now, just load as regular capsules

            // Load capsules
            CSharpKOTOR.Formats.Capsule.Capsule file1Capsule = null;
            CSharpKOTOR.Formats.Capsule.Capsule file2Capsule = null;

            try
            {
                file1Capsule = new CSharpKOTOR.Formats.Capsule.Capsule(cFile1);
            }
            catch (Exception e)
            {
                logFunc($"Could not load '{cFile1}'. Reason: {e.GetType().Name}: {e.Message}");
                return null;
            }

            try
            {
                file2Capsule = new CSharpKOTOR.Formats.Capsule.Capsule(cFile2);
            }
            catch (Exception e)
            {
                logFunc($"Could not load '{cFile2}'. Reason: {e.GetType().Name}: {e.Message}");
                return null;
            }

            // Build dict of resources
            var capsule1Resources = new Dictionary<string, CSharpKOTOR.Formats.Capsule.CapsuleResource>();
            var capsule2Resources = new Dictionary<string, CSharpKOTOR.Formats.Capsule.CapsuleResource>();

            foreach (var res in file1Capsule)
            {
                string resName = res.ResName;
                if (!capsule1Resources.ContainsKey(resName))
                {
                    capsule1Resources[resName] = res;
                }
            }

            foreach (var res in file2Capsule)
            {
                string resName = res.ResName;
                if (!capsule2Resources.ContainsKey(resName))
                {
                    capsule2Resources[resName] = res;
                }
            }

            // Identify missing resources
            var missingInCapsule1 = new HashSet<string>(capsule2Resources.Keys);
            missingInCapsule1.ExceptWith(capsule1Resources.Keys);

            var missingInCapsule2 = new HashSet<string>(capsule1Resources.Keys);
            missingInCapsule2.ExceptWith(capsule2Resources.Keys);

            // Report missing resources
            foreach (string resref in missingInCapsule1.OrderBy(r => r))
            {
                var res = capsule2Resources[resref];
                string resExt = res.ResType.Extension.ToUpperInvariant();
                logFunc($"Resource missing:\t{cFile1Rel}\t{resref}\t{resExt}");

                // TODO: Add to install folders if modificationsByType is provided
            }

            foreach (string resref in missingInCapsule2.OrderBy(r => r))
            {
                var res = capsule1Resources[resref];
                string resExt = res.ResType.Extension.ToUpperInvariant();
                logFunc($"Resource missing:\t{cFile2Rel}\t{resref}\t{resExt}");
            }

            // Check for differences in common resources
            bool? isSameResult = true;
            var commonResrefs = new HashSet<string>(capsule1Resources.Keys);
            commonResrefs.IntersectWith(capsule2Resources.Keys);

            foreach (string resref in commonResrefs.OrderBy(r => r))
            {
                var res1 = capsule1Resources[resref];
                var res2 = capsule2Resources[resref];
                string ext = res1.ResType.Extension.ToLowerInvariant();
                var ctx = new DiffContext(cFile1Rel, cFile2Rel, ext, resref);

                byte[] data1 = res1.Data;
                byte[] data2 = res2.Data;

                bool? result = DiffData(data1, data2, ctx, compareHashes, modificationsByType, logFunc);
                if (result == null)
                {
                    isSameResult = null;
                }
                else if (result == false)
                {
                    isSameResult = false;
                }
            }

            return isSameResult;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2296-2442
        // Original: def diff_directories(...): ...
        public static bool? DiffDirectories(
            string dir1,
            string dir2,
            List<string> filters = null,
            Action<string> logFunc = null,
            Func<string, string, bool?> diffFilesFunc = null,
            DiffCache diffCache = null,
            ModificationsByType modificationsByType = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            if (diffFilesFunc == null)
            {
                diffFilesFunc = (f1, f2) => DiffFiles(f1, f2, logFunc, true, modificationsByType);
            }

            logFunc($"Finding differences in the '{Path.GetFileName(dir1)}' folders...");

            // Store relative paths instead of just filenames
            var filesPath1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filesPath2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(dir1))
            {
                foreach (string file in Directory.GetFiles(dir1, "*", SearchOption.AllDirectories))
                {
                    string relPath = Path.GetRelativePath(dir1, file).Replace('\\', '/');
                    filesPath1.Add(relPath);
                }
            }

            if (Directory.Exists(dir2))
            {
                foreach (string file in Directory.GetFiles(dir2, "*", SearchOption.AllDirectories))
                {
                    string relPath = Path.GetRelativePath(dir2, file).Replace('\\', '/');
                    filesPath2.Add(relPath);
                }
            }

            // Merge both sets
            var allFiles = new HashSet<string>(filesPath1, StringComparer.OrdinalIgnoreCase);
            allFiles.UnionWith(filesPath2);

            // Apply filters if provided
            if (filters != null && filters.Count > 0)
            {
                var filteredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string file in allFiles)
                {
                    if (ShouldIncludeInFilteredDiff(file, filters))
                    {
                        filteredFiles.Add(file);
                    }
                }
                if (filteredFiles.Count != allFiles.Count)
                {
                    logFunc($"Applying filters: {string.Join(", ", filters)}");
                    logFunc($"Filtered from {allFiles.Count} to {filteredFiles.Count} files");
                }
                allFiles = filteredFiles;
            }

            // Special handling for modules directories
            var filesToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool? isSameResult = true;

            if (IsModulesDirectory(dir1) || IsModulesDirectory(dir2))
            {
                var moduleResult = DiffModuleDirectories(
                    dir1,
                    dir2,
                    filesPath1,
                    filesPath2,
                    logFunc,
                    (c1, c2, r1, r2) => DiffCapsuleFiles(c1, c2, r1, r2, logFunc, true, modificationsByType));
                isSameResult = moduleResult.Item1;
                filesToSkip = moduleResult.Item2;
            }

            // Progress tracking
            const int PROGRESS_FILE_THRESHOLD = 100;
            var remainingFiles = new HashSet<string>(allFiles, StringComparer.OrdinalIgnoreCase);
            remainingFiles.ExceptWith(filesToSkip);

            // Separate files into existing and missing
            var existingInBoth = new List<string>();
            var missingFiles = new List<Tuple<string, bool>>(); // (rel_path, is_missing_from_dir1)

            foreach (string relPath in remainingFiles.OrderBy(r => r))
            {
                string file1Path = Path.Combine(dir1, relPath);
                string file2Path = Path.Combine(dir2, relPath);

                if (!File.Exists(file1Path))
                {
                    missingFiles.Add(Tuple.Create(relPath, true));
                }
                else if (!File.Exists(file2Path))
                {
                    missingFiles.Add(Tuple.Create(relPath, false));
                }
                else
                {
                    existingInBoth.Add(relPath);
                }
            }

            int totalFiles = remainingFiles.Count;
            logFunc($"Comparing {totalFiles} files...");

            // Process files that exist in both directories
            for (int idx = 0; idx < existingInBoth.Count; idx++)
            {
                string relPath = existingInBoth[idx];
                if (totalFiles > PROGRESS_FILE_THRESHOLD)
                {
                    logFunc($"Progress: {idx + 1}/{totalFiles} files processed...");
                }

                string file1Path = Path.Combine(dir1, relPath);
                string file2Path = Path.Combine(dir2, relPath);

                bool? result = diffFilesFunc(file1Path, file2Path);
                if (result == null)
                {
                    isSameResult = null;
                }
                else if (isSameResult.HasValue)
                {
                    isSameResult = result.Value && isSameResult.Value;
                }
                else
                {
                    isSameResult = result.Value;
                }

                // Record in cache if caching enabled
                if (diffCache != null && diffCache.Files != null)
                {
                    string ext = DiffEngineUtils.ExtOf(relPath);
                    string status = result == true ? "identical" : "modified";
                    diffCache.Files.Add(new CachedFileComparison
                    {
                        RelPath = relPath,
                        Status = status,
                        Ext = ext,
                        LeftExists = true,
                        RightExists = true
                    });
                }
            }

            // Report all missing files after comparisons
            foreach (var missing in missingFiles.OrderBy(m => m.Item1))
            {
                string relPath = missing.Item1;
                bool isMissingFromDir1 = missing.Item2;

                if (isMissingFromDir1)
                {
                    string file1Path = Path.Combine(dir1, relPath);
                    string file2Path = Path.Combine(dir2, relPath);
                    string cFile1Rel = RelativePathFromTo(dir2, file1Path);
                    logFunc($"Missing file:\t{cFile1Rel}");

                    // Add to install folders - file exists in modded (dir2) but not vanilla (dir1)
                    if (modificationsByType != null)
                    {
                        AddMissingFileToInstall(
                            modificationsByType,
                            relPath,
                            logFunc,
                            file2Path,
                            incrementalWriter);
                    }

                    if (diffCache != null && diffCache.Files != null)
                    {
                        string ext = DiffEngineUtils.ExtOf(relPath);
                        diffCache.Files.Add(new CachedFileComparison
                        {
                            RelPath = relPath,
                            Status = "missing_left",
                            Ext = ext,
                            LeftExists = false,
                            RightExists = true
                        });
                    }
                }
                else
                {
                    string file2Path = Path.Combine(dir2, relPath);
                    string cFile2Rel = RelativePathFromTo(dir1, file2Path);
                    logFunc($"Missing file:\t{cFile2Rel}");

                    if (diffCache != null && diffCache.Files != null)
                    {
                        string ext = DiffEngineUtils.ExtOf(relPath);
                        diffCache.Files.Add(new CachedFileComparison
                        {
                            RelPath = relPath,
                            Status = "missing_right",
                            Ext = ext,
                            LeftExists = true,
                            RightExists = false
                        });
                    }
                }
                isSameResult = false;
            }

            if (totalFiles > PROGRESS_FILE_THRESHOLD)
            {
                logFunc($"Completed: {totalFiles}/{totalFiles} files processed.");
            }

            return isSameResult;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2010-2012
        // Original: def is_modules_directory(dir_path: Path) -> bool: ...
        private static bool IsModulesDirectory(string dirPath)
        {
            if (string.IsNullOrEmpty(dirPath))
            {
                return false;
            }
            string dirName = Path.GetFileName(dirPath).ToLowerInvariant();
            return dirName == "modules" || dirName == "module" || dirName == "mods";
        }


        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2109-2250
        // Original: def diff_module_directories(...): ...
        private static Tuple<bool?, HashSet<string>> DiffModuleDirectories(
            string cDir1,
            string cDir2,
            HashSet<string> filesPath1,
            HashSet<string> filesPath2,
            Action<string> logFunc,
            Func<string, string, string, string, bool?> diffCapsuleFilesFunc)
        {
            bool? isSameResult = true;
            var filesToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // For now, simplified implementation - just compare files normally
            // Full composite module loading logic would be very complex
            // TODO: Implement full composite module loading if needed

            return Tuple.Create(isSameResult, filesToSkip);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1159-1165
        // Original: def relative_path_from_to(...): ...
        private static string RelativePathFromTo(string fromDir, string toPath)
        {
            try
            {
                return Path.GetRelativePath(fromDir, toPath).Replace('\\', '/');
            }
            catch (Exception)
            {
                return Path.GetFileName(toPath);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:383-450
        // Original: def _add_missing_file_to_install(...): ...
        private static void AddMissingFileToInstall(
            ModificationsByType modificationsByType,
            string relPath,
            Action<string> logFunc,
            string file2Path,
            IncrementalTSLPatchDataWriter incrementalWriter)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            // Determine the install folder based on the relative path
            string filename = Path.GetFileName(relPath);

            // Check if this is a capsule file (.mod/.rim/.erf)
            if (DiffEngineUtils.IsCapsuleFile(filename))
            {
                logFunc($"  Extracting resources from capsule: {filename}");
                // TODO: Implement capsule extraction logic
                return;
            }

            // Determine destination folder
            string destination = "Override";
            string lowerPath = relPath.ToLowerInvariant();
            if (lowerPath.Contains("modules"))
            {
                destination = "modules";
            }
            else if (lowerPath.Contains("lips"))
            {
                destination = "Lips";
            }
            else if (lowerPath.Contains("streamwaves") || lowerPath.Contains("streamvoice"))
            {
                destination = "StreamWaves";
            }

            // Add to InstallList
            if (modificationsByType.Install == null)
            {
                modificationsByType.Install = new List<CSharpKOTOR.Mods.InstallFile>();
            }

            var installFile = new CSharpKOTOR.Mods.InstallFile(filename, destination, file2Path);
            modificationsByType.Install.Add(installFile);

            // Write immediately if incremental writer is provided
            // Note: IncrementalTSLPatchDataWriter will handle InstallList generation at the end
            // No immediate write needed for InstallFile
        }
    }
}

