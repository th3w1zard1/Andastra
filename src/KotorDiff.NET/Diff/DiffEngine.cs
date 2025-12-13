// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:3294-3386
// Original: def run_differ_from_args_impl(...): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Common;
using CSharpKOTOR.Installation;

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
            ModificationsByType modificationsByType = null)
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
                    modificationsByType: modificationsByType);

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
            ModificationsByType modificationsByType = null)
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
                    // Resource exists in multiple paths - compare them
                    // TODO: Implement full n-way comparison logic
                    // For now, just check if all are identical
                    var resources = pathData.Values.ToList();
                    bool allIdentical = true;

                    for (int i = 0; i < resources.Count - 1; i++)
                    {
                        for (int j = i + 1; j < resources.Count; j++)
                        {
                            var res1 = resources[i];
                            var res2 = resources[j];

                            // Simple byte comparison for now
                            if (res1.Data.Length != res2.Data.Length)
                            {
                                allIdentical = false;
                                break;
                            }

                            bool bytesEqual = true;
                            for (int k = 0; k < res1.Data.Length; k++)
                            {
                                if (res1.Data[k] != res2.Data[k])
                                {
                                    bytesEqual = false;
                                    break;
                                }
                            }

                            if (!bytesEqual)
                            {
                                allIdentical = false;
                                logFunc?.Invoke($"\n[DIFFERENT] {resourceId}");
                                logFunc?.Invoke($"  Differences found between paths");
                                diffCount++;
                                break;
                            }
                        }

                        if (!allIdentical)
                        {
                            break;
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
    }
}

