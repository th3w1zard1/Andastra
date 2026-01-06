// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:539-1226
// Original: def diff_installations_with_resolution(...): ...
// This file contains the main n-way installation comparison function with proper resource resolution order handling.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Mods;
using KotorDiff.Diff;
using Andastra.Parsing.TSLPatcher;
using JetBrains.Annotations;

namespace KotorDiff.Resolution
{
    /// <summary>
    /// Main installation diff functions with resource resolution order handling.
    /// 1:1 port of diff_installations_with_resolution from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:539-1226
    /// </summary>
    public static class InstallationDiffWithResolution
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:539-615
        // Original: def diff_installations_with_resolution(...) -> bool | None: ...
        /// <summary>
        /// Compare N installations/paths using proper resource resolution order (N-way wrapper).
        /// This is the n-way comparison function that accepts an arbitrary number of paths.
        /// Each path can be an Installation object, a folder Path, or a file Path.
        /// All paths are treated as interchangeable - no hardcoded "vanilla" vs "modded" assumptions.
        /// </summary>
        public static bool? DiffInstallationsWithResolution(
            List<object> filesAndFoldersAndInstallations,
            List<string> filters = null,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null,
            Func<byte[], byte[], DiffContext, bool, ModificationsByType, Action<string>, IncrementalTSLPatchDataWriter, bool?> diffDataFunc = null)
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

            if (diffDataFunc == null)
            {
                throw new ArgumentNullException(nameof(diffDataFunc), "diffDataFunc parameter is required");
            }

            // Convert Path objects to Installations if they are KOTOR installations
            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:539-615
            // Original: def diff_installations_with_resolution(...): ...
            var convertedPaths = new List<object>();
            foreach (var path in filesAndFoldersAndInstallations)
            {
                if (path is Installation installation)
                {
                    convertedPaths.Add(installation);
                }
                else
                {
                    // Try to convert Path to Installation if it's a KOTOR installation
                    string pathStr = null;
                    if (path is string str)
                    {
                        pathStr = str;
                    }
                    else if (path is DirectoryInfo dirInfo)
                    {
                        pathStr = dirInfo.FullName;
                    }
                    else if (path is FileInfo fileInfo)
                    {
                        pathStr = fileInfo.DirectoryName;
                    }

                    if (pathStr != null && ResolutionUtils.IsKotorInstallDir(pathStr))
                    {
                        try
                        {
                            var newInstallation = new Installation(pathStr);
                            convertedPaths.Add(newInstallation);
                            logFunc?.Invoke($"Converted path to Installation: {pathStr}");
                        }
                        catch (Exception e)
                        {
                            logFunc?.Invoke($"Warning: Could not convert path to Installation: {pathStr} - {e.Message}");
                            // Fall back to regular comparison by returning null
                            // This will cause RunDifferFromArgsImpl to use non-resolution comparison
                            return null;
                        }
                    }
                    else
                    {
                        // Not a KOTOR installation - fall back to regular comparison
                        logFunc?.Invoke($"Path is not a KOTOR installation, falling back to regular comparison: {pathStr ?? path.ToString()}");
                        return null;
                    }
                }
            }

            // Implement true N-way resource resolution comparison
            // For each resource, find the winning version across all installations
            // according to KOTOR resource resolution order, then compare against baseline
            return DiffInstallationsWithResolutionNWayImpl(
                convertedPaths,
                filters,
                logFunc,
                compareHashes,
                modificationsByType,
                incrementalWriter,
                diffDataFunc);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:618-1226
        // Original: def _diff_installations_with_resolution_impl(...) -> bool | None: ...
        /// <summary>
        /// Compare installations using proper resource resolution order (2-way implementation).
        /// This function respects the game's actual file priority:
        /// - Override (highest)
        /// - Modules (.mod)
        /// - Modules (.rim/_s.rim/_dlg.erf)
        /// - Chitin/BIFs (lowest)
        /// </summary>
        public static bool? DiffInstallationsWithResolutionImpl(
            Installation install1,
            Installation install2,
            List<string> filters = null,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null,
            List<Installation> additionalInstalls = null,
            Func<byte[], byte[], DiffContext, bool, ModificationsByType, Action<string>, IncrementalTSLPatchDataWriter, bool?> diffDataFunc = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            // Build list of all installations (base, target, and any additional)
            var allInstallations = new List<Installation> { install1, install2 };
            if (additionalInstalls != null)
            {
                allInstallations.AddRange(additionalInstalls);
            }

            int installCount = allInstallations.Count;

            // Get display names for logging
            string install1Name = Path.GetFileName(install1.Path);
            string install2Name = Path.GetFileName(install2.Path);

            logFunc("");
            logFunc("=".PadRight(80, '='));
            logFunc("RESOURCE-AWARE INSTALLATION COMPARISON");
            logFunc("=".PadRight(80, '='));
            logFunc($"Base installation (index 0):   {install1.Path}");
            logFunc($"Target installation (index 1): {install2.Path}");
            if (additionalInstalls != null)
            {
                for (int idx = 0; idx < additionalInstalls.Count; idx++)
                {
                    logFunc($"Additional installation (index {idx + 2}): {additionalInstalls[idx].Path}");
                }
            }
            logFunc($"Total installations: {installCount}");
            logFunc("");
            logFunc("Using KOTOR resource resolution order (highest to lowest priority):");
            logFunc("  1. Override");
            logFunc("  2. Modules (.mod)");
            logFunc("  3. Modules (.rim/_s.rim/_dlg.erf)");
            logFunc("  4. Chitin (BIFs)");
            logFunc("=".PadRight(80, '='));
            logFunc("");

            // Build resource indices for O(1) lookups (massive performance improvement)
            logFunc("Building resource indices for fast lookups...");

            // Build indices for all installations
            var installIndices = new Dictionary<int, Dictionary<ResourceIdentifier, List<Andastra.Parsing.Extract.FileResource>>>();
            var allIdentifiersSet = new HashSet<ResourceIdentifier>();

            for (int idx = 0; idx < allInstallations.Count; idx++)
            {
                Installation installation = allInstallations[idx];
                installIndices[idx] = ResourceResolver.BuildResourceIndex(installation);
                foreach (var identifier in installIndices[idx].Keys)
                {
                    allIdentifiersSet.Add(identifier);
                }
                logFunc($"  Installation {idx}: {installIndices[idx].Count} unique resources indexed");
            }

            // Convenience aliases for primary installations
            var index1 = installIndices[0];
            var index2 = installIndices[1];

            var allIdentifiers = allIdentifiersSet.ToList();
            logFunc($"  Total unique resources across all installations: {allIdentifiers.Count}");
            logFunc("  Index build complete - ready for O(1) lookups");
            logFunc("");

            // Early exit optimization: if no resources found, installations are identical
            if (allIdentifiers.Count == 0)
            {
                logFunc("No resources found in any installation - installations are identical");
                return true;
            }

            // PROCESS TLK FILES FIRST AND IMMEDIATELY (before StrRef cache building)
            // TLKList must come immediately after [Settings] per TSLPatcher design
            logFunc("Processing TLK files first for immediate TLKList generation...");
            logFunc("");

            var tlkIdentifiers = allIdentifiers.Where(ident => ident.ResType.Extension.Equals("tlk", StringComparison.OrdinalIgnoreCase)).ToList();
            var filteredTlkIdentifiers = new List<ResourceIdentifier>();
            if (tlkIdentifiers.Count > 0)
            {
                logFunc($"Found {tlkIdentifiers.Count} TLK file(s) to process first");

                // Apply filtering for irrelevant TLK files when both paths are installations
                foreach (var identifier in tlkIdentifiers)
                {
                    // Resolve in install2 to check filepath for filtering
                    var resolved2 = ResourceResolver.ResolveResourceInInstallation(install2, identifier, logFunc: logFunc, verbose: false, resourceIndex: index2);

                    if (ResourceResolver.ShouldProcessTlkFile(resolved2))
                    {
                        filteredTlkIdentifiers.Add(identifier);
                        logFunc($"  Processing TLK: {identifier.ResName}.{identifier.ResType.Extension}");
                    }
                    else
                    {
                        logFunc($"  Skipping irrelevant TLK: {identifier.ResName}.{identifier.ResType.Extension} (not dialog.tlk/dialog_f.tlk in root)");
                    }
                }
            }

            // Process filtered TLK files
            if (filteredTlkIdentifiers.Count > 0)
            {
                logFunc($"Processing {filteredTlkIdentifiers.Count} TLK files...");
                logFunc("");
                for (int idx = 0; idx < filteredTlkIdentifiers.Count; idx++)
                {
                    var identifier = filteredTlkIdentifiers[idx];
                    logFunc($"Processing TLK {idx + 1}/{filteredTlkIdentifiers.Count}: {identifier.ResName}.{identifier.ResType.Extension}");
                    // Resolve in both installations using indices (O(1) lookups)
                    var resolved1 = ResourceResolver.ResolveResourceInInstallation(install1, identifier, logFunc: logFunc, verbose: false, resourceIndex: index1);
                    var resolved2 = ResourceResolver.ResolveResourceInInstallation(install2, identifier, logFunc: logFunc, verbose: false, resourceIndex: index2);

                    // Check if resource exists in both
                    if (resolved1.Data == null && resolved2.Data == null)
                    {
                        logFunc($"    TLK {identifier.ResName} missing in both installations, skipping");
                        continue;
                    }

                    if (resolved1.Data == null)
                    {
                        // Only in target installation (new resource) - add to [InstallList] and create patch
                        logFunc($"    TLK {identifier.ResName} only in target installation - adding to InstallList");
                        string destination = ResourceResolver.DetermineTslpatcherDestination(null, resolved2.LocationType, resolved2.Filepath);
                        string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";
                        if (modificationsByType != null)
                        {
                            // Create context for patch creation
                            string file1Rel = Path.Combine("base", filename); // Base (missing)
                            string file2Rel = Path.Combine("target", filename); // Target (exists)
                            var context = new DiffContext(file1Rel, file2Rel, identifier.ResType.Extension.ToLowerInvariant());
                            InstallationDiffHelpers.AddToInstallFolder(
                                modificationsByType,
                                destination,
                                filename,
                                logFunc: logFunc,
                                moddedData: resolved2.Data, // Data from target path
                                moddedPath: resolved2.Filepath, // Path from target
                                context: context,
                                incrementalWriter: incrementalWriter);
                        }
                        if (incrementalWriter != null)
                        {
                            incrementalWriter.AddInstallFile(destination, filename, resolved2.Filepath);
                        }
                        continue;
                    }

                    if (resolved2.Data == null)
                    {
                        // Only in base installation (removed resource) - no action needed
                        logFunc($"    TLK {identifier.ResName} only in base installation - no action needed");
                        continue;
                    }

                    // Both exist - compare them using proper format-aware comparison
                    logFunc($"    Comparing TLK {identifier.ResName} between installations");
                    // For TLK files, DON'T set resname - they are loose files, not in containers
                    string file1RelTlk = resolved1.Filepath != null
                        ? Path.Combine(install1Name, Path.GetFileName(resolved1.Filepath))
                        : Path.Combine(install1Name, "unknown");
                    string file2RelTlk = resolved2.Filepath != null
                        ? Path.Combine(install2Name, Path.GetFileName(resolved2.Filepath))
                        : Path.Combine(install2Name, "unknown");

                    var ctx = new DiffContext(
                        file1RelTlk,
                        file2RelTlk,
                        identifier.ResType.Extension.ToLowerInvariant(),
                        resRef: null); // TLK files are loose files, not in containers
                    ctx.File1LocationType = resolved1.LocationType;
                    ctx.File2LocationType = resolved2.LocationType;
                    ctx.File1Filepath = resolved1.Filepath;
                    ctx.File2Filepath = resolved2.Filepath;

                    // Create a temporary log buffer to capture diff_data output
                    var tlkDiffOutputLines = new List<string>();

                    Action<string> tlkBufferedLogFunc = msg => tlkDiffOutputLines.Add(msg);

                    bool? result = diffDataFunc(
                        resolved1.Data,
                        resolved2.Data,
                        ctx,
                        compareHashes,
                        modificationsByType,
                        tlkBufferedLogFunc,
                        incrementalWriter);

                    // Output the buffered diff results
                    foreach (string line in tlkDiffOutputLines)
                    {
                        logFunc(line);
                    }
                }
            }

            // Remove TLK identifiers from the main list since we've processed them
            allIdentifiers = allIdentifiers.Where(ident => !ident.ResType.Extension.Equals("tlk", StringComparison.OrdinalIgnoreCase)).ToList();
            logFunc("TLK processing complete.");
            logFunc("");

            // Ensure complete [TLKList] section is written before StrRef cache building
            if (incrementalWriter != null)
            {
                incrementalWriter.WritePendingTlkModifications();
                logFunc("");
            }

            // Sort remaining identifiers to process 2DA before GFF (TSLPatcher-compliant order)
            // Order: InstallList entries (handled in main loop), then 2DA, then GFF, then NCS, then SSF, then others
            int GetProcessingPriority(ResourceIdentifier identifier)
            {
                string ext = identifier.ResType.Extension.ToLowerInvariant();
                if (ext == "2da" || ext == "twoda")
                {
                    return 0;
                }
                // Check if it's a GFF extension
                var gffExtensions = GetGffExtensions();
                if (gffExtensions.Contains(ext))
                {
                    return 1;
                }
                if (ext == "ncs")
                {
                    return 2;
                }
                if (ext == "ssf")
                {
                    return 3;
                }
                return 4; // Other types (handled as InstallList)
            }

            // Sort by priority, then by identifier
            allIdentifiers = allIdentifiers.OrderBy(x => (GetProcessingPriority(x), x.ToString())).ToList();

            // Assert that at least ONE modification entry exists before processing resources
            // This ensures TLK StrRef references are found and linked early (fast fail)
            if (modificationsByType == null)
            {
                throw new InvalidOperationException("modifications_by_type must not be None");
            }
            int totalMods = modificationsByType.Gff.Count + modificationsByType.Twoda.Count + modificationsByType.Ssf.Count +
                           modificationsByType.Ncs.Count + modificationsByType.Tlk.Count;
            if (totalMods == 0)
            {
                throw new InvalidOperationException(
                    $"No modifications found in modifications_by_type before resource processing! " +
                    $"TLK: {modificationsByType.Tlk.Count}, " +
                    $"GFF: {modificationsByType.Gff.Count}, " +
                    $"2DA: {modificationsByType.Twoda.Count}, " +
                    $"SSF: {modificationsByType.Ssf.Count}, " +
                    $"NCS: {modificationsByType.Ncs.Count}");
            }
            logFunc($"Found {totalMods} total modifications to process");

            logFunc("Sorted resources for processing: 2DA -> GFF -> NCS -> SSF -> Others");
            logFunc("");

            // Apply filters if provided
            if (filters != null && filters.Count > 0)
            {
                var filteredIdentifiersSet = new HashSet<ResourceIdentifier>();
                foreach (var ident in allIdentifiers)
                {
                    string resourceName = $"{ident.ResName}.{ident.ResType.Extension}".ToLowerInvariant();
                    foreach (string filterPattern in filters)
                    {
                        if (resourceName.Contains(filterPattern.ToLowerInvariant()))
                        {
                            filteredIdentifiersSet.Add(ident);
                            break;
                        }
                    }
                }
                logFunc($"Applied filters: {string.Join(", ", filters)}");
                logFunc($"  Filtered to {filteredIdentifiersSet.Count} resources");
                allIdentifiers = filteredIdentifiersSet.ToList();
                logFunc("");
            }

            // Cache for resolved resources to avoid re-resolution
            var resolutionCache = new Dictionary<(int, ResourceIdentifier), ResolvedResource>();

            // Compare each resource
            bool? isSameResult = true;
            int processedCount = 0;
            int diffCount = 0;
            int errorCount = 0;
            int identicalCount = 0;

            logFunc($"Comparing {allIdentifiers.Count} resources using resolution order...");
            logFunc("");

            // Compare each resource
            foreach (var identifier in allIdentifiers)
            {
                processedCount++;

                // Progress update every 100 resources
                if (processedCount % 100 == 0)
                {
                    logFunc($"Progress: {processedCount}/{allIdentifiers.Count} resources processed...");
                }

                // Resolve in both installations using indices (O(1) lookups instead of O(n) scans)
                // Use cache to avoid re-resolving the same resource
                var cacheKey1 = (0, identifier);
                var cacheKey2 = (1, identifier);
                if (!resolutionCache.ContainsKey(cacheKey1))
                {
                    resolutionCache[cacheKey1] = ResourceResolver.ResolveResourceInInstallation(install1, identifier, logFunc: logFunc, verbose: false, resourceIndex: index1);
                }
                if (!resolutionCache.ContainsKey(cacheKey2))
                {
                    resolutionCache[cacheKey2] = ResourceResolver.ResolveResourceInInstallation(install2, identifier, logFunc: logFunc, verbose: false, resourceIndex: index2);
                }
                var resolved1 = resolutionCache[cacheKey1];
                var resolved2 = resolutionCache[cacheKey2];

                // Check if resource exists in both
                if (resolved1.Data == null && resolved2.Data == null)
                {
                    // Both missing - this shouldn't happen but handle it
                    continue;
                }

                if (resolved1.Data == null)
                {
                    // Only in target installation (new resource) - add to [InstallList] and create patch
                    logFunc($"\nProcessing resource: {identifier.ResName}.{identifier.ResType.Extension}");

                    // Re-resolve with verbose logging to show where it was found
                    logFunc($"Installation 1 (target - {install2Name}):");
                    ResourceResolver.ResolveResourceInInstallation(install2, identifier, logFunc: logFunc, verbose: true, resourceIndex: index2);

                    logFunc($"\n[NEW RESOURCE] {identifier}");
                    logFunc($"  Source (target/install1): {resolved2.SourceLocation}");
                    logFunc($"  Missing from base (install0 - {install1Name})");

                    // Add to InstallList with correct destination based on resolution order
                    // Also create patch modifications
                    if (modificationsByType != null)
                    {
                        // Determine destination based on where it was found in target installation
                        string destination = ResourceResolver.DetermineTslpatcherDestination(null, resolved2.LocationType, resolved2.Filepath);
                        string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";

                        // Create context for patch creation
                        string file1Rel = Path.Combine("base", filename); // Base (missing)
                        string file2Rel = Path.Combine("target", filename); // Target (exists)
                        var context = new DiffContext(file1Rel, file2Rel, identifier.ResType.Extension.ToLowerInvariant());

                        InstallationDiffHelpers.AddToInstallFolder(
                            modificationsByType,
                            destination,
                            filename,
                            logFunc: logFunc,
                            moddedData: resolved2.Data, // Data from target path
                            moddedPath: resolved2.Filepath, // Path from target
                            context: context,
                            incrementalWriter: incrementalWriter);
                        logFunc($"  → [InstallList] destination: {destination}");
                        logFunc("  → File will be INSTALLED, then PATCHED");
                        // Write immediately if using incremental writer
                        if (incrementalWriter != null)
                        {
                            // Get the source file from target installation
                            string sourcePath = resolved2.Filepath;
                            incrementalWriter.AddInstallFile(destination, filename, sourcePath);
                        }
                    }

                    diffCount++;
                    isSameResult = false;
                    continue;
                }

                if (resolved2.Data == null)
                {
                    // Resource exists in install1 but not install2
                    // In n-way comparison, we treat this as needing a patch from install1's data
                    // The resource should be installable from install1's version
                    logFunc($"\nProcessing resource: {identifier.ResName}.{identifier.ResType.Extension}");

                    // Re-resolve with verbose logging to show where it was found
                    logFunc($"Installation 0 ({install1Name}):");
                    ResourceResolver.ResolveResourceInInstallation(install1, identifier, logFunc: logFunc, verbose: true, resourceIndex: index1);

                    logFunc($"\n[RESOURCE IN INSTALL0 ONLY] {identifier}");
                    logFunc($"  Source (install0 - {install1Name}): {resolved1.SourceLocation}");
                    logFunc($"  Missing from install1 ({install2Name})");
                    logFunc("  → Creating patch from install0's version");

                    // Add to InstallList and create patch modifications
                    if (modificationsByType != null)
                    {
                        // Determine destination based on where it was found
                        string destination = ResourceResolver.DetermineTslpatcherDestination(resolved1.LocationType, null, resolved1.Filepath);
                        string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";

                        // Create context for patch creation
                        string file1Rel = Path.Combine($"install0_{install1Name}", filename);
                        string file2Rel = Path.Combine($"install1_{install2Name}", filename); // Missing
                        var context = new DiffContext(file1Rel, file2Rel, identifier.ResType.Extension.ToLowerInvariant());

                        InstallationDiffHelpers.AddToInstallFolder(
                            modificationsByType,
                            destination,
                            filename,
                            logFunc: logFunc,
                            moddedData: resolved1.Data, // Data from first path (install0)
                            moddedPath: resolved1.Filepath, // Path from first (install0)
                            context: context,
                            incrementalWriter: incrementalWriter);
                        logFunc($"  → [InstallList] destination: {destination}");
                        logFunc("  → File will be INSTALLED from install0");

                        // Write immediately if using incremental writer
                        if (incrementalWriter != null)
                        {
                            string sourcePath = resolved1.Filepath;
                            incrementalWriter.AddInstallFile(destination, filename, sourcePath);
                        }
                    }

                    diffCount++;
                    isSameResult = false;
                    continue;
                }

                // Both exist - check if both are from BIFs (read-only, skip comparison)
                bool bothFromBif = resolved1.LocationType == "Chitin BIFs" && resolved2.LocationType == "Chitin BIFs";
                if (bothFromBif)
                {
                    // Both from read-only BIFs - skip comparison (can't be patched anyway)
                    identicalCount++;
                    continue;
                }

                // Compare them using proper format-aware comparison
                // Build full context path: install_name/relative_path_to_container
                string file1Path;
                if (!string.IsNullOrEmpty(resolved1.Filepath))
                {
                    try
                    {
                        string rel1 = Path.GetRelativePath(install1.Path, resolved1.Filepath);
                        file1Path = Path.Combine(install1Name, rel1);
                    }
                    catch (ArgumentException)
                    {
                        file1Path = Path.Combine(install1Name, Path.GetFileName(resolved1.Filepath));
                    }
                }
                else
                {
                    file1Path = Path.Combine(install1Name, "unknown");
                }

                string file2Path;
                if (!string.IsNullOrEmpty(resolved2.Filepath))
                {
                    try
                    {
                        string rel2 = Path.GetRelativePath(install2.Path, resolved2.Filepath);
                        file2Path = Path.Combine(install2Name, rel2);
                    }
                    catch (ArgumentException)
                    {
                        file2Path = Path.Combine(install2Name, Path.GetFileName(resolved2.Filepath));
                    }
                }
                else
                {
                    file2Path = Path.Combine(install2Name, "unknown");
                }

                // Only set resname for resources inside containers (BIFs/capsules)
                // For loose files, resname should be None to avoid duplication in 'where' property
                bool isInContainer = resolved2.LocationType == "Chitin BIFs" ||
                                     (!string.IsNullOrEmpty(resolved2.Filepath) &&
                                      new[] { ".bif", ".rim", ".erf", ".mod", ".sav" }.Contains(Path.GetExtension(resolved2.Filepath).ToLowerInvariant()));
                string resnameForContext = isInContainer ? identifier.ResName : null;

                var ctx = new DiffContext(
                    file1Path,
                    file2Path,
                    identifier.ResType.Extension.ToLowerInvariant(),
                    resRef: resnameForContext);
                ctx.File1LocationType = resolved1.LocationType;
                ctx.File2LocationType = resolved2.LocationType;
                ctx.File2Filepath = resolved2.Filepath;

                // Store original modifications count to detect if diff_data added any
                int originalModCount = 0;
                if (modificationsByType != null)
                {
                    originalModCount = modificationsByType.Gff.Count + modificationsByType.Twoda.Count +
                                      modificationsByType.Ssf.Count + modificationsByType.Tlk.Count +
                                      modificationsByType.Ncs.Count;
                }

                // Create a temporary log buffer to capture diff_data output
                var diffOutputLines = new List<string>();

                Action<string> bufferedLogFunc = msg => diffOutputLines.Add(msg);

                bool? result = diffDataFunc(
                    resolved1.Data,
                    resolved2.Data,
                    ctx,
                    compareHashes,
                    modificationsByType,
                    bufferedLogFunc,
                    incrementalWriter);

                if (result == false)
                {
                    // Resources differ - NOW show consolidated resolution logging BEFORE replaying diff output
                    logFunc($"\nProcessing resource: {identifier.ResName}.{identifier.ResType.Extension}");

                    // Show consolidated resolution for both installations
                    ResourceResolver.LogConsolidatedResolution(
                        install1,
                        install2,
                        identifier,
                        resolved1,
                        resolved2,
                        logFunc);

                    // Now replay the diff output
                    foreach (string line in diffOutputLines)
                    {
                        logFunc(line);
                    }

                    // Add summary
                    diffCount++;
                    logFunc($"\n[MODIFIED] {identifier}");
                    logFunc($"  Base source (install0 - {install1Name}): {resolved1.SourceLocation}");
                    logFunc($"  Target source (install1 - {install2Name}): {resolved2.SourceLocation}");

                    // Log priority explanation if sources are different
                    if (resolved1.LocationType != resolved2.LocationType)
                    {
                        string priority1 = ResourceResolver.GetLocationDisplayName(resolved1.LocationType);
                        string priority2 = ResourceResolver.GetLocationDisplayName(resolved2.LocationType);
                        logFunc($"  Priority changed: {priority1} → {priority2}");

                        if (resolved2.LocationType == "Override folder")
                        {
                            logFunc($"  → Resource moved to Override (will override base {priority1})");
                        }
                        else if (resolved1.LocationType == "Chitin BIFs" && resolved2.LocationType != null && resolved2.LocationType.Contains("Modules"))
                        {
                            logFunc("  → Resource moved from BIF to Modules (now modifiable)");
                        }
                    }

                    // Validate TSLPatcher destination was set correctly
                    if (modificationsByType != null)
                    {
                        int newModCount = modificationsByType.Gff.Count + modificationsByType.Twoda.Count +
                                         modificationsByType.Ssf.Count + modificationsByType.Tlk.Count +
                                         modificationsByType.Ncs.Count;

                        if (newModCount > originalModCount)
                        {
                            // A modification was added - validate its destination is correct
                            string expectedDestination = ResourceResolver.DetermineTslpatcherDestination(
                                resolved1.LocationType,
                                resolved2.LocationType,
                                resolved2.Filepath);

                            // Check the most recently added modification(s)
                            // Note: TLK is excluded because it's append-only and goes to game root
                            CheckAndCorrectDestination(modificationsByType.Gff, expectedDestination, logFunc);
                            CheckAndCorrectDestination(modificationsByType.Twoda, expectedDestination, logFunc);
                            CheckAndCorrectDestination(modificationsByType.Ssf, expectedDestination, logFunc);
                            CheckAndCorrectDestination(modificationsByType.Ncs, expectedDestination, logFunc);
                        }

                        // ALWAYS also add an InstallList entry so the file exists before patching
                        try
                        {
                            string destinationForInstall = ResourceResolver.DetermineTslpatcherDestination(
                                resolved1.LocationType,
                                resolved2.LocationType,
                                resolved2.Filepath);
                            string filenameForInstall = $"{identifier.ResName}.{identifier.ResType.Extension}";

                            InstallationDiffHelpers.AddToInstallFolder(
                                modificationsByType,
                                destinationForInstall,
                                filenameForInstall,
                                logFunc: logFunc,
                                moddedData: resolved2.Data,
                                moddedPath: resolved2.Filepath,
                                context: ctx,
                                incrementalWriter: incrementalWriter);
                        }
                        catch (Exception e)
                        {
                            logFunc($"Error adding install entry for {identifier.ResName}.{identifier.ResType.Extension}: {e.GetType().Name}: {e.Message}");
                            logFunc("Full traceback:");
                            logFunc(e.StackTrace);
                        }
                    }

                    isSameResult = false;
                }
                else if (result == null)
                {
                    // Error occurred
                    errorCount++;
                    logFunc($"\n[ERROR] {identifier}");
                    logFunc($"  Base source (install0 - {install1Name}): {resolved1.SourceLocation}");
                    logFunc($"  Target source (install1 - {install2Name}): {resolved2.SourceLocation}");
                    isSameResult = null;
                }
                else
                {
                    // Identical
                    identicalCount++;
                }
            }

            // Summary
            logFunc("");
            logFunc("=".PadRight(80, '='));
            logFunc("COMPARISON SUMMARY");
            logFunc("=".PadRight(80, '='));
            logFunc($"Total resources processed: {processedCount}");
            logFunc($"  Identical: {identicalCount}");
            logFunc($"  Modified: {diffCount}");
            logFunc($"  Errors: {errorCount}");
            logFunc("=".PadRight(80, '='));

            return isSameResult;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:539-1226 (N-way version)
        // Original: def _diff_installations_with_resolution_nway_impl(...) -> bool | None: ...
        /// <summary>
        /// Compare N installations using proper resource resolution order (true N-way implementation).
        /// This function respects the game's actual file priority:
        /// - Override (highest)
        /// - Modules (.mod)
        /// - Modules (.rim/_s.rim/_dlg.erf)
        /// - Chitin/BIFs (lowest)
        /// </summary>
        public static bool? DiffInstallationsWithResolutionNWayImpl(
            List<object> installations,
            List<string> filters = null,
            Action<string> logFunc = null,
            bool compareHashes = true,
            ModificationsByType modificationsByType = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null,
            Func<byte[], byte[], DiffContext, bool, ModificationsByType, Action<string>, IncrementalTSLPatchDataWriter, bool?> diffDataFunc = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            if (diffDataFunc == null)
            {
                throw new ArgumentNullException(nameof(diffDataFunc), "diffDataFunc parameter is required");
            }

            // Convert all paths to installations
            var allInstallations = new List<Installation>();
            foreach (var path in installations)
            {
                if (path is Installation installation)
                {
                    allInstallations.Add(installation);
                }
                else
                {
                    // Try to convert Path to Installation if it's a KOTOR installation
                    string pathStr = null;
                    if (path is string str)
                    {
                        pathStr = str;
                    }
                    else if (path is DirectoryInfo dirInfo)
                    {
                        pathStr = dirInfo.FullName;
                    }
                    else if (path is FileInfo fileInfo)
                    {
                        pathStr = fileInfo.DirectoryName;
                    }

                    if (pathStr != null && ResolutionUtils.IsKotorInstallDir(pathStr))
                    {
                        try
                        {
                            var newInstallation = new Installation(pathStr);
                            allInstallations.Add(newInstallation);
                            logFunc?.Invoke($"Converted path to Installation: {pathStr}");
                        }
                        catch (Exception e)
                        {
                            logFunc?.Invoke($"Warning: Could not convert path to Installation: {pathStr} - {e.Message}");
                            return null;
                        }
                    }
                    else
                    {
                        logFunc?.Invoke($"Path is not a KOTOR installation: {pathStr ?? path.ToString()}");
                        return null;
                    }
                }
            }

            int installCount = allInstallations.Count;

            // Get display names for logging
            var installNames = new List<string>();
            for (int idx = 0; idx < allInstallations.Count; idx++)
            {
                installNames.Add(Path.GetFileName(allInstallations[idx].Path));
            }

            logFunc("");
            logFunc("=".PadRight(80, '='));
            logFunc("N-WAY RESOURCE-AWARE INSTALLATION COMPARISON");
            logFunc("=".PadRight(80, '='));
            for (int idx = 0; idx < allInstallations.Count; idx++)
            {
                string label = idx == 0 ? "Base installation (index 0):" :
                               idx == 1 ? "Target installation (index 1):" :
                               $"Additional installation (index {idx}):";
                logFunc($"{label} {allInstallations[idx].Path}");
            }
            logFunc($"Total installations: {installCount}");
            logFunc("");
            logFunc("Using KOTOR resource resolution order (highest to lowest priority):");
            logFunc("  1. Override");
            logFunc("  2. Modules (.mod)");
            logFunc("  3. Modules (.rim/_s.rim/_dlg.erf)");
            logFunc("  4. Chitin (BIFs)");
            logFunc("=".PadRight(80, '='));
            logFunc("");

            // Build resource indices for O(1) lookups (massive performance improvement)
            logFunc("Building resource indices for fast lookups...");

            // Build indices for all installations
            var installIndices = new Dictionary<int, Dictionary<ResourceIdentifier, List<Andastra.Parsing.Extract.FileResource>>>();
            var allIdentifiersSet = new HashSet<ResourceIdentifier>();

            for (int idx = 0; idx < allInstallations.Count; idx++)
            {
                Installation installation = allInstallations[idx];
                installIndices[idx] = ResourceResolver.BuildResourceIndex(installation);
                foreach (var identifier in installIndices[idx].Keys)
                {
                    allIdentifiersSet.Add(identifier);
                }
                logFunc($"  Installation {idx}: {installIndices[idx].Count} unique resources indexed");
            }

            var allIdentifiers = allIdentifiersSet.ToList();
            logFunc($"  Total unique resources across all installations: {allIdentifiers.Count}");
            logFunc("  Index build complete - ready for O(1) lookups");
            logFunc("");

            // Early exit optimization: if no resources found, installations are identical
            if (allIdentifiers.Count == 0)
            {
                logFunc("No resources found in any installation - installations are identical");
                return true;
            }

            // PROCESS TLK FILES FIRST AND IMMEDIATELY (before StrRef cache building)
            // TLKList must come immediately after [Settings] per TSLPatcher design
            logFunc("Processing TLK files first for immediate TLKList generation...");
            logFunc("");

            var tlkIdentifiers = allIdentifiers.Where(ident => ident.ResType.Extension.Equals("tlk", StringComparison.OrdinalIgnoreCase)).ToList();
            var filteredTlkIdentifiers = new List<ResourceIdentifier>();
            if (tlkIdentifiers.Count > 0)
            {
                logFunc($"Found {tlkIdentifiers.Count} TLK file(s) to process first");

                // Apply filtering for irrelevant TLK files
                foreach (var identifier in tlkIdentifiers)
                {
                    // Resolve in first installation to check filepath for filtering
                    var resolvedInFirst = ResourceResolver.ResolveResourceInInstallation(allInstallations[0], identifier, logFunc: logFunc, verbose: false, resourceIndex: installIndices[0]);

                    if (ResourceResolver.ShouldProcessTlkFile(resolvedInFirst))
                    {
                        filteredTlkIdentifiers.Add(identifier);
                        logFunc($"  Processing TLK: {identifier.ResName}.{identifier.ResType.Extension}");
                    }
                    else
                    {
                        logFunc($"  Skipping irrelevant TLK: {identifier.ResName}.{identifier.ResType.Extension} (not dialog.tlk/dialog_f.tlk in root)");
                    }
                }
            }

            // Process filtered TLK files - compare all installations for each TLK
            if (filteredTlkIdentifiers.Count > 0)
            {
                logFunc($"Processing {filteredTlkIdentifiers.Count} TLK files...");
                logFunc("");
                for (int idx = 0; idx < filteredTlkIdentifiers.Count; idx++)
                {
                    var identifier = filteredTlkIdentifiers[idx];
                    logFunc($"Processing TLK {idx + 1}/{filteredTlkIdentifiers.Count}: {identifier.ResName}.{identifier.ResType.Extension}");

                    // Resolve in ALL installations
                    var resolvedInAll = new List<ResolvedResource>();
                    for (int installIdx = 0; installIdx < allInstallations.Count; installIdx++)
                    {
                        var resolved = ResourceResolver.ResolveResourceInInstallation(allInstallations[installIdx], identifier, logFunc: logFunc, verbose: false, resourceIndex: installIndices[installIdx]);
                        resolvedInAll.Add(resolved);
                    }

                    // Find the winning TLK according to resolution order
                    var winningTlk = FindWinningResource(resolvedInAll, allInstallations, logFunc);

                    // For TLK files, compare the winning version against the base (index 0)
                    var baseResolved = resolvedInAll[0];

                    if (winningTlk.Data == null && baseResolved.Data == null)
                    {
                        logFunc($"    TLK {identifier.ResName} missing in both winning and base installations, skipping");
                        continue;
                    }

                    if (winningTlk.Data == null)
                    {
                        // Only in base installation - no action needed for TLK
                        logFunc($"    TLK {identifier.ResName} only in base installation - no action needed");
                        continue;
                    }

                    if (baseResolved.Data == null)
                    {
                        // TLK exists in winning installation but not in base - add to InstallList
                        logFunc($"    TLK {identifier.ResName} missing in base installation - adding to InstallList");
                        string destination = ResourceResolver.DetermineTslpatcherDestination(null, winningTlk.LocationType, winningTlk.Filepath);
                        string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";
                        if (modificationsByType != null)
                        {
                            string file1Rel = Path.Combine("base", filename); // Base (missing)
                            string file2Rel = Path.Combine("target", filename); // Target (exists)
                            var context = new DiffContext(file1Rel, file2Rel, identifier.ResType.Extension.ToLowerInvariant());
                            InstallationDiffHelpers.AddToInstallFolder(
                                modificationsByType,
                                destination,
                                filename,
                                logFunc: logFunc,
                                moddedData: winningTlk.Data,
                                moddedPath: winningTlk.Filepath,
                                context: context,
                                incrementalWriter: incrementalWriter);
                        }
                        if (incrementalWriter != null)
                        {
                            incrementalWriter.AddInstallFile(destination, filename, winningTlk.Filepath);
                        }
                        continue;
                    }

                    // Both exist - compare them
                    logFunc($"    Comparing TLK {identifier.ResName} between winning and base installations");
                    string file1RelTlk = baseResolved.Filepath != null
                        ? Path.Combine(installNames[0], Path.GetFileName(baseResolved.Filepath))
                        : Path.Combine(installNames[0], "unknown");
                    string file2RelTlk = winningTlk.Filepath != null
                        ? Path.Combine("winning", Path.GetFileName(winningTlk.Filepath))
                        : Path.Combine("winning", "unknown");

                    var ctx = new DiffContext(
                        file1RelTlk,
                        file2RelTlk,
                        identifier.ResType.Extension.ToLowerInvariant(),
                        resRef: null);
                    ctx.File1LocationType = baseResolved.LocationType;
                    ctx.File2LocationType = winningTlk.LocationType;
                    ctx.File1Filepath = baseResolved.Filepath;
                    ctx.File2Filepath = winningTlk.Filepath;

                    var tlkDiffOutputLines = new List<string>();
                    Action<string> tlkBufferedLogFunc = msg => tlkDiffOutputLines.Add(msg);

                    bool? result = diffDataFunc(
                        baseResolved.Data,
                        winningTlk.Data,
                        ctx,
                        compareHashes,
                        modificationsByType,
                        tlkBufferedLogFunc,
                        incrementalWriter);

                    foreach (string line in tlkDiffOutputLines)
                    {
                        logFunc(line);
                    }
                }
            }

            // Remove TLK identifiers from the main list
            allIdentifiers = allIdentifiers.Where(ident => !ident.ResType.Extension.Equals("tlk", StringComparison.OrdinalIgnoreCase)).ToList();
            logFunc("TLK processing complete.");
            logFunc("");

            // Ensure complete [TLKList] section is written before StrRef cache building
            if (incrementalWriter != null)
            {
                incrementalWriter.WritePendingTlkModifications();
                logFunc("");
            }

            // Sort remaining identifiers for processing
            int GetProcessingPriority(ResourceIdentifier identifier)
            {
                string ext = identifier.ResType.Extension.ToLowerInvariant();
                if (ext == "2da" || ext == "twoda")
                {
                    return 0;
                }
                var gffExtensions = GetGffExtensions();
                if (gffExtensions.Contains(ext))
                {
                    return 1;
                }
                if (ext == "ncs")
                {
                    return 2;
                }
                if (ext == "ssf")
                {
                    return 3;
                }
                return 4;
            }

            allIdentifiers = allIdentifiers.OrderBy(x => (GetProcessingPriority(x), x.ToString())).ToList();

            // Assert that modifications exist
            if (modificationsByType == null)
            {
                throw new InvalidOperationException("modifications_by_type must not be None");
            }
            int totalMods = modificationsByType.Gff.Count + modificationsByType.Twoda.Count + modificationsByType.Ssf.Count +
                           modificationsByType.Ncs.Count + modificationsByType.Tlk.Count + modificationsByType.Install.Count;
            if (totalMods == 0)
            {
                throw new InvalidOperationException(
                    $"No modifications found in modifications_by_type before resource processing! " +
                    $"TLK: {modificationsByType.Tlk.Count}, " +
                    $"GFF: {modificationsByType.Gff.Count}, " +
                    $"2DA: {modificationsByType.Twoda.Count}, " +
                    $"SSF: {modificationsByType.Ssf.Count}, " +
                    $"NCS: {modificationsByType.Ncs.Count}, " +
                    $"Install: {modificationsByType.Install.Count}");
            }
            logFunc($"Found {totalMods} total modifications to process ({modificationsByType.Install.Count} install entries, {modificationsByType.Tlk.Count + modificationsByType.Gff.Count + modificationsByType.Twoda.Count + modificationsByType.Ssf.Count + modificationsByType.Ncs.Count} patch entries)");

            // Apply filters if provided
            if (filters != null && filters.Count > 0)
            {
                var filteredIdentifiersSet = new HashSet<ResourceIdentifier>();
                foreach (var ident in allIdentifiers)
                {
                    string resourceName = $"{ident.ResName}.{ident.ResType.Extension}".ToLowerInvariant();
                    foreach (string filterPattern in filters)
                    {
                        if (resourceName.Contains(filterPattern.ToLowerInvariant()))
                        {
                            filteredIdentifiersSet.Add(ident);
                            break;
                        }
                    }
                }
                logFunc($"Applied filters: {string.Join(", ", filters)}");
                logFunc($"  Filtered to {filteredIdentifiersSet.Count} resources");
                allIdentifiers = filteredIdentifiersSet.ToList();
                logFunc("");
            }

            // Cache for resolved resources
            var resolutionCache = new Dictionary<(int, ResourceIdentifier), ResolvedResource>();

            // Compare each resource using N-way resolution
            bool? isSameResult = true;
            int processedCount = 0;
            int diffCount = 0;
            int errorCount = 0;
            int identicalCount = 0;

            logFunc($"Comparing {allIdentifiers.Count} resources using N-way resolution order...");
            logFunc("");

            foreach (var identifier in allIdentifiers)
            {
                processedCount++;

                if (processedCount % 100 == 0)
                {
                    logFunc($"Progress: {processedCount}/{allIdentifiers.Count} resources processed...");
                }

                // Resolve in ALL installations
                var resolvedInAll = new List<ResolvedResource>();
                for (int installIdx = 0; installIdx < allInstallations.Count; installIdx++)
                {
                    var cacheKey = (installIdx, identifier);
                    if (!resolutionCache.ContainsKey(cacheKey))
                    {
                        resolutionCache[cacheKey] = ResourceResolver.ResolveResourceInInstallation(
                            allInstallations[installIdx], identifier, logFunc: logFunc, verbose: false, resourceIndex: installIndices[installIdx]);
                    }
                    resolvedInAll.Add(resolutionCache[cacheKey]);
                }

                // Find the winning resource according to KOTOR resolution order
                var winningResource = FindWinningResource(resolvedInAll, allInstallations, logFunc);
                var baseResource = resolvedInAll[0]; // Base installation (index 0)

                // Log which installation won
                int winningIndex = FindWinningIndex(resolvedInAll, allInstallations);
                if (winningIndex >= 0)
                {
                    logFunc($"\nProcessing resource: {identifier.ResName}.{identifier.ResType.Extension}");
                    logFunc($"  Winning installation: {installNames[winningIndex]} (index {winningIndex}) - {winningResource.SourceLocation}");
                }

                // Check if resource exists in winning vs base
                if (winningResource.Data == null && baseResource.Data == null)
                {
                    continue;
                }

                if (winningResource.Data == null)
                {
                    // Exists in base but not in winning - no action needed (resource would be removed)
                    logFunc($"  [RESOURCE IN BASE ONLY] {identifier}");
                    logFunc($"    Source (base - {installNames[0]}): {baseResource.SourceLocation}");
                    logFunc("    → No action needed (resource removal not supported)");
                    identicalCount++;
                    continue;
                }

                if (baseResource.Data == null)
                {
                    // Exists in winning but not in base - add to InstallList
                    logFunc($"  [NEW RESOURCE] {identifier}");
                    logFunc($"    Source (winning - {installNames[winningIndex]}): {winningResource.SourceLocation}");
                    logFunc($"    Missing from base ({installNames[0]})");

                    if (modificationsByType != null)
                    {
                        string destination = ResourceResolver.DetermineTslpatcherDestination(null, winningResource.LocationType, winningResource.Filepath);
                        string filename = $"{identifier.ResName}.{identifier.ResType.Extension}";

                        string file1Rel = Path.Combine("base", filename);
                        string file2Rel = Path.Combine("winning", filename);
                        var context = new DiffContext(file1Rel, file2Rel, identifier.ResType.Extension.ToLowerInvariant());

                        InstallationDiffHelpers.AddToInstallFolder(
                            modificationsByType,
                            destination,
                            filename,
                            logFunc: logFunc,
                            moddedData: winningResource.Data,
                            moddedPath: winningResource.Filepath,
                            context: context,
                            incrementalWriter: incrementalWriter);
                        logFunc($"    → [InstallList] destination: {destination}");

                        if (incrementalWriter != null)
                        {
                            incrementalWriter.AddInstallFile(destination, filename, winningResource.Filepath);
                        }
                    }

                    diffCount++;
                    isSameResult = false;
                    continue;
                }

                // Both exist - check if both are from BIFs
                bool bothFromBif = baseResource.LocationType == "Chitin BIFs" && winningResource.LocationType == "Chitin BIFs";
                if (bothFromBif)
                {
                    identicalCount++;
                    continue;
                }

                // Compare winning resource against base resource
                string file1Path = baseResource.Filepath != null
                    ? Path.Combine(installNames[0], Path.GetFileName(baseResource.Filepath))
                    : Path.Combine(installNames[0], "unknown");

                string file2Path = winningResource.Filepath != null
                    ? Path.Combine("winning", Path.GetFileName(winningResource.Filepath))
                    : Path.Combine("winning", "unknown");

                bool isInContainer = winningResource.LocationType == "Chitin BIFs" ||
                                   (!string.IsNullOrEmpty(winningResource.Filepath) &&
                                    new[] { ".bif", ".rim", ".erf", ".mod", ".sav" }.Contains(Path.GetExtension(winningResource.Filepath).ToLowerInvariant()));
                string resnameForContext = isInContainer ? identifier.ResName : null;

                var ctx = new DiffContext(
                    file1Path,
                    file2Path,
                    identifier.ResType.Extension.ToLowerInvariant(),
                    resRef: resnameForContext);
                ctx.File1LocationType = baseResource.LocationType;
                ctx.File2LocationType = winningResource.LocationType;
                ctx.File2Filepath = winningResource.Filepath;

                int originalModCount = modificationsByType.Gff.Count + modificationsByType.Twoda.Count +
                                     modificationsByType.Ssf.Count + modificationsByType.Tlk.Count +
                                     modificationsByType.Ncs.Count;

                var diffOutputLines = new List<string>();
                Action<string> bufferedLogFunc = msg => diffOutputLines.Add(msg);

                bool? result = diffDataFunc(
                    baseResource.Data,
                    winningResource.Data,
                    ctx,
                    compareHashes,
                    modificationsByType,
                    bufferedLogFunc,
                    incrementalWriter);

                if (result == false)
                {
                    // Resources differ
                    foreach (string line in diffOutputLines)
                    {
                        logFunc(line);
                    }

                    logFunc($"\n  [MODIFIED] {identifier}");
                    logFunc($"    Base source ({installNames[0]}): {baseResource.SourceLocation}");
                    logFunc($"    Winning source ({installNames[winningIndex]}): {winningResource.SourceLocation}");

                    // Log priority change if applicable
                    if (baseResource.LocationType != winningResource.LocationType)
                    {
                        string priority1 = ResourceResolver.GetLocationDisplayName(baseResource.LocationType);
                        string priority2 = ResourceResolver.GetLocationDisplayName(winningResource.LocationType);
                        logFunc($"    Priority changed: {priority1} → {priority2}");

                        if (winningResource.LocationType == "Override folder")
                        {
                            logFunc("    → Resource moved to Override (will override base)");
                        }
                        else if (baseResource.LocationType == "Chitin BIFs" && winningResource.LocationType.Contains("Modules"))
                        {
                            logFunc("    → Resource moved from BIF to Modules (now modifiable)");
                        }
                    }

                    // Validate destination and add InstallList entry
                    if (modificationsByType != null)
                    {
                        int newModCount = modificationsByType.Gff.Count + modificationsByType.Twoda.Count +
                                         modificationsByType.Ssf.Count + modificationsByType.Tlk.Count +
                                         modificationsByType.Ncs.Count;

                        if (newModCount > originalModCount)
                        {
                            string expectedDestination = ResourceResolver.DetermineTslpatcherDestination(
                                baseResource.LocationType,
                                winningResource.LocationType,
                                winningResource.Filepath);

                            CheckAndCorrectDestination(modificationsByType.Gff, expectedDestination, logFunc);
                            CheckAndCorrectDestination(modificationsByType.Twoda, expectedDestination, logFunc);
                            CheckAndCorrectDestination(modificationsByType.Ssf, expectedDestination, logFunc);
                            CheckAndCorrectDestination(modificationsByType.Ncs, expectedDestination, logFunc);
                        }

                        // Always add InstallList entry
                        try
                        {
                            string destinationForInstall = ResourceResolver.DetermineTslpatcherDestination(
                                baseResource.LocationType,
                                winningResource.LocationType,
                                winningResource.Filepath);
                            string filenameForInstall = $"{identifier.ResName}.{identifier.ResType.Extension}";

                            InstallationDiffHelpers.AddToInstallFolder(
                                modificationsByType,
                                destinationForInstall,
                                filenameForInstall,
                                logFunc: logFunc,
                                moddedData: winningResource.Data,
                                moddedPath: winningResource.Filepath,
                                context: ctx,
                                incrementalWriter: incrementalWriter);
                        }
                        catch (Exception e)
                        {
                            logFunc($"Error adding install entry for {identifier.ResName}.{identifier.ResType.Extension}: {e.GetType().Name}: {e.Message}");
                        }
                    }

                    diffCount++;
                    isSameResult = false;
                }
                else if (result == null)
                {
                    errorCount++;
                    logFunc($"\n  [ERROR] {identifier}");
                    isSameResult = null;
                }
                else
                {
                    identicalCount++;
                }
            }

            // Summary
            logFunc("");
            logFunc("=".PadRight(80, '='));
            logFunc("N-WAY COMPARISON SUMMARY");
            logFunc("=".PadRight(80, '='));
            logFunc($"Total resources processed: {processedCount}");
            logFunc($"  Identical: {identicalCount}");
            logFunc($"  Modified: {diffCount}");
            logFunc($"  Errors: {errorCount}");
            logFunc("=".PadRight(80, '='));

            return isSameResult;
        }

        // Helper method to find the winning resource according to KOTOR resolution order
        private static ResolvedResource FindWinningResource(List<ResolvedResource> resolvedResources, List<Installation> installations, Action<string> logFunc)
        {
            // KOTOR resource resolution priority (highest to lowest):
            // 1. Override folder
            // 2. Modules (.mod)
            // 3. Modules (.rim/_s.rim/_dlg.erf)
            // 4. Chitin BIFs

            int GetPriority(string locationType)
            {
                if (locationType == null) return 999;
                if (locationType.Contains("Override")) return 0;
                if (locationType.Contains("Modules") && locationType.Contains(".mod")) return 1;
                if (locationType.Contains("Modules")) return 2;
                if (locationType.Contains("Chitin BIFs")) return 3;
                return 999;
            }

            ResolvedResource winning = null;
            int bestPriority = int.MaxValue;

            for (int i = 0; i < resolvedResources.Count; i++)
            {
                var resolved = resolvedResources[i];
                if (resolved.Data != null)
                {
                    int priority = GetPriority(resolved.LocationType);
                    if (priority < bestPriority)
                    {
                        bestPriority = priority;
                        winning = resolved;
                    }
                }
            }

            return winning ?? new ResolvedResource(); // Return empty if none found
        }

        // Helper method to find which installation index has the winning resource
        private static int FindWinningIndex(List<ResolvedResource> resolvedResources, List<Installation> installations)
        {
            var winning = FindWinningResource(resolvedResources, installations, null);
            for (int i = 0; i < resolvedResources.Count; i++)
            {
                if (resolvedResources[i].Data == winning.Data)
                {
                    return i;
                }
            }
            return -1;
        }

        // Helper method to get GFF extensions
        private static HashSet<string> GetGffExtensions()
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Add all GFF content types as extensions
            extensions.Add("utc");
            extensions.Add("uti");
            extensions.Add("utp");
            extensions.Add("ute");
            extensions.Add("utm");
            extensions.Add("utd");
            extensions.Add("utw");
            extensions.Add("dlg");
            extensions.Add("are");
            extensions.Add("git");
            extensions.Add("ifo");
            extensions.Add("gui");
            extensions.Add("jrl");
            extensions.Add("fac");
            extensions.Add("gff");
            extensions.Add("res"); // For PTH, NFO, PT, GVT, INV
            return extensions;
        }

        // Helper method to check and correct destination for a list of modifications
        private static void CheckAndCorrectDestination<T>(List<T> modList, string expectedDestination, Action<string> logFunc) where T : PatcherModifications
        {
            if (modList != null && modList.Count > 0)
            {
                var mostRecent = modList[modList.Count - 1];
                string actualDest = mostRecent.Destination;
                if (actualDest != expectedDestination)
                {
                    // Destination mismatch - log warning and correct it
                    logFunc($"  ⚠ Warning: Destination mismatch! Expected '{expectedDestination}', got '{actualDest}'. Correcting...");
                    mostRecent.Destination = expectedDestination;
                }
                else
                {
                    // Destination is correct - log for confirmation
                    logFunc($"  ✓ TSLPatcher destination: {actualDest}");
                }
            }
        }
    }
}

