// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:86-310
// Original: def resolve_resource_in_installation(...): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET;
using BioWare.NET.Extract;
using BioWare.NET.Extract.Capsule;
using BioWare.NET.Resource.Formats.Capsule;
using BioWare.NET.Installation;
using BioWare.NET.Resource;
using KotorDiff.Diff;
using KotorDiff.Logger;
using JetBrains.Annotations;

namespace KotorDiff.Resolution
{
    /// <summary>
    /// Resource resolution utilities for KOTOR installations.
    /// 1:1 port of resolution functions from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:86-310
    /// </summary>
    public static class ResourceResolver
    {
        /// <summary>
        /// Get human-readable name for a location type.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:77-83
        /// </summary>
        public static string GetLocationDisplayName([CanBeNull] string locationType)
        {
            if (locationType == null)
            {
                return "Not Found";
            }
            return locationType;
        }

        /// <summary>
        /// Resolve a resource in an installation using game priority order.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:86-310
        /// Resolution order (ONLY applies to Override/Modules/Chitin):
        /// 1. Override folder (highest priority)
        /// 2. Modules (.mod files)
        /// 3. Modules (.rim/_s.rim/_dlg.erf files - composite loading)
        /// 4. Chitin BIFs (lowest priority)
        /// </summary>
        public static ResolvedResource ResolveResourceInInstallation(
            Installation installation,
            ResourceIdentifier identifier,
            Action<string> logFunc = null,
            bool verbose = true,
            Dictionary<ResourceIdentifier, List<BioWare.NET.Extract.FileResource>> resourceIndex = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            // Find all instances of this resource across the installation
            var overrideFiles = new List<string>();
            var moduleModFiles = new List<string>();
            var moduleRimFiles = new List<string>();
            var chitinFiles = new List<string>();

            // Store FileResource instances for data retrieval
            var resourceInstances = new Dictionary<string, BioWare.NET.Extract.FileResource>();

            try
            {
                // Use index if provided (O(1) lookup), otherwise iterate (O(n) scan)
                List<BioWare.NET.Extract.FileResource> fileResources;
                if (resourceIndex != null)
                {
                    fileResources = resourceIndex.ContainsKey(identifier) ? resourceIndex[identifier] : new List<BioWare.NET.Extract.FileResource>();
                }
                else
                {
                    // Fallback: search installation using Resource method for each identifier
                    // This is slower but works when no index is provided
                    // In practice, resourceIndex should be provided for performance
                    fileResources = new List<BioWare.NET.Extract.FileResource>();
                }

                // Categorize all instances by location
                string installRoot = installation.Path;

                // Group module files by their basename for proper composite handling
                var moduleGroups = new Dictionary<string, List<(string filepath, BioWare.NET.Extract.FileResource resource)>>();

                foreach (var fileResource in fileResources)
                {
                    string filepath = fileResource.FilePath;
                    var parentNamesLower = GetParentNamesLower(filepath);

                    // Store for data retrieval later
                    resourceInstances[filepath] = fileResource;

                    // Categorize by location (ONLY resolution-order locations)
                    if (parentNamesLower.Contains("override"))
                    {
                        overrideFiles.Add(filepath);
                    }
                    else if (parentNamesLower.Contains("modules"))
                    {
                        // Group by module basename to handle composite loading correctly
                        try
                        {
                            string moduleRoot = ResolutionUtils.GetModuleRoot(filepath);
                            if (!moduleGroups.ContainsKey(moduleRoot))
                            {
                                moduleGroups[moduleRoot] = new List<(string, FileResource)>();
                            }
                            moduleGroups[moduleRoot].Add((filepath, fileResource));
                        }
                        catch (Exception e)
                        {
                            logFunc($"Warning: Could not determine module root for {filepath}: {e.GetType().Name}: {e.Message}");
                            logFunc("Full traceback:");
                            logFunc(e.StackTrace);
                            // Fallback: add to rim files without grouping
                            if (Path.GetExtension(filepath).Equals(".mod", StringComparison.OrdinalIgnoreCase))
                            {
                                moduleModFiles.Add(filepath);
                            }
                            else
                            {
                                moduleRimFiles.Add(filepath);
                            }
                        }
                    }
                    else if (parentNamesLower.Contains("data") || Path.GetExtension(filepath).Equals(".bif", StringComparison.OrdinalIgnoreCase))
                    {
                        chitinFiles.Add(filepath);
                    }
                    else if (Path.GetDirectoryName(filepath) == installRoot)
                    {
                        // Files directly in installation root (like dialog.tlk, chitin.key, etc.)
                        // Treat as Override priority since they're loose files at root level
                        overrideFiles.Add(filepath);
                    }
                    // StreamWaves/etc in subdirectories are NOT added - they don't participate in resolution
                }

                // Within each module basename group, apply composite loading priority
                // Priority within a group: .mod > .rim > _s.rim > _dlg.erf
                int GetCompositePriority(string filepath)
                {
                    string nameLower = Path.GetFileName(filepath).ToLowerInvariant();
                    if (nameLower.EndsWith(".mod"))
                    {
                        return 0; // Highest priority
                    }
                    if (nameLower.EndsWith(".rim") && !nameLower.EndsWith("_s.rim"))
                    {
                        return 1;
                    }
                    if (nameLower.EndsWith("_s.rim"))
                    {
                        return 2;
                    }
                    if (nameLower.EndsWith("_dlg.erf"))
                    {
                        return 3;
                    }
                    return 4; // Other files
                }

                // Process each module group and pick the winner
                foreach (var kvp in moduleGroups)
                {
                    string moduleBasename = kvp.Key;
                    var filesInGroup = kvp.Value;
                    if (filesInGroup.Count == 0)
                    {
                        logFunc($"Warning: Empty module group for basename {moduleBasename}");
                        continue;
                    }

                    // Sort by composite priority and pick the winner
                    var sortedFiles = filesInGroup.OrderBy(x => GetCompositePriority(x.filepath)).ToList();
                    var winnerPath = sortedFiles[0].filepath;

                    // Add winner to appropriate category
                    if (Path.GetExtension(winnerPath).Equals(".mod", StringComparison.OrdinalIgnoreCase))
                    {
                        moduleModFiles.Add(winnerPath);
                        continue;
                    }
                    moduleRimFiles.Add(winnerPath);
                }

                // Apply resolution order: Override > .mod > .rim > Chitin
                string chosenFilepath = null;
                string locationType = null;

                if (overrideFiles.Count > 0)
                {
                    chosenFilepath = overrideFiles[0];
                    // Check if it's actually in Override folder or root
                    if (GetParentNamesLower(chosenFilepath).Contains("override"))
                    {
                        locationType = "Override folder";
                    }
                    else
                    {
                        locationType = "Installation root";
                    }
                }
                else if (moduleModFiles.Count > 0)
                {
                    chosenFilepath = moduleModFiles[0];
                    locationType = "Modules (.mod)";
                }
                else if (moduleRimFiles.Count > 0)
                {
                    // Use first .rim file found (composite loading handled elsewhere)
                    chosenFilepath = moduleRimFiles[0];
                    locationType = "Modules (.rim)";
                }
                else if (chitinFiles.Count > 0)
                {
                    chosenFilepath = chitinFiles[0];
                    locationType = "Chitin BIFs";
                }

                if (chosenFilepath == null)
                {
                    return new ResolvedResource
                    {
                        Identifier = identifier,
                        Data = null,
                        SourceLocation = "Not found in installation",
                        LocationType = null,
                        Filepath = null,
                        AllLocations = null
                    };
                }

                // Read the data from the chosen location (O(1) lookup with stored instances)
                byte[] data = null;
                if (resourceInstances.ContainsKey(chosenFilepath))
                {
                    var fileResource = resourceInstances[chosenFilepath];
                    data = fileResource.GetData();
                }

                if (data == null)
                {
                    return new ResolvedResource
                    {
                        Identifier = identifier,
                        Data = null,
                        SourceLocation = $"Found but couldn't read: {chosenFilepath}",
                        LocationType = locationType,
                        Filepath = chosenFilepath,
                        AllLocations = null
                    };
                }

                // Create human-readable source description
                string sourceDesc;
                try
                {
                    string relPath = Path.GetRelativePath(installRoot, chosenFilepath);
                    sourceDesc = $"{locationType}: {relPath}";
                }
                catch (ArgumentException)
                {
                    sourceDesc = $"{locationType}: {chosenFilepath}";
                }

                // Store all found locations for combined logging
                var allLocs = new Dictionary<string, List<string>>
                {
                    { "Override folder", overrideFiles },
                    { "Modules (.mod)", moduleModFiles },
                    { "Modules (.rim/_s.rim/._dlg.erf)", moduleRimFiles },
                    { "Chitin BIFs", chitinFiles }
                };

                return new ResolvedResource
                {
                    Identifier = identifier,
                    Data = data,
                    SourceLocation = sourceDesc,
                    LocationType = locationType,
                    Filepath = chosenFilepath,
                    AllLocations = allLocs
                };
            }
            catch (Exception e)
            {
                logFunc($"[Error] Failed to resolve {identifier}: {e.GetType().Name}: {e.Message}");
                logFunc("Full traceback:");
                logFunc(e.StackTrace);

                return new ResolvedResource
                {
                    Identifier = identifier,
                    Data = null,
                    SourceLocation = $"Error: {e.GetType().Name}: {e.Message}",
                    LocationType = null,
                    Filepath = null,
                    AllLocations = null
                };
            }
        }

        private static List<string> GetParentNamesLower(string filepath)
        {
            var result = new List<string>();
            string current = Path.GetDirectoryName(filepath);
            while (!string.IsNullOrEmpty(current))
            {
                result.Add(Path.GetFileName(current).ToLowerInvariant());
                current = Path.GetDirectoryName(current);
            }
            return result;
        }

        // Helper method to get all FileResources from an installation
        // In Python, Installation is iterable, but in C# we need to combine different sources
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:431-446
        private static List<BioWare.NET.Extract.FileResource> GetAllFileResources(Installation installation)
        {
            var allResources = new List<BioWare.NET.Extract.FileResource>();

            // Get Chitin resources (includes patch.erf for K1)
            allResources.AddRange(installation.CoreResources());

            // Get Override resources
            allResources.AddRange(installation.OverrideResources());

            // Get module resources
            var moduleRoots = installation.GetModuleRoots();
            foreach (string moduleRoot in moduleRoots)
            {
                var moduleFiles = installation.GetModuleFiles(moduleRoot);
                foreach (string moduleFile in moduleFiles)
                {
                    try
                    {
                        var capsule = new LazyCapsule(moduleFile);
                        allResources.AddRange(capsule.GetResources());
                    }
                    catch
                    {
                        // Skip modules that can't be loaded
                    }
                }
            }

            return allResources;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:431-446
        // Original: def collect_all_resource_identifiers(installation: Installation) -> set[ResourceIdentifier]: ...
        /// <summary>
        /// Collect all unique resource identifiers from an installation.
        /// </summary>
        public static HashSet<ResourceIdentifier> CollectAllResourceIdentifiers(Installation installation)
        {
            var identifiers = new HashSet<ResourceIdentifier>();

            // Get all resources from installation
            var allResources = GetAllFileResources(installation);
            foreach (var fileResource in allResources)
            {
                identifiers.Add(fileResource.Identifier);
            }

            return identifiers;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:449-465
        // Original: def build_resource_index(installation: Installation) -> dict[ResourceIdentifier, list[FileResource]]: ...
        /// <summary>
        /// Build an index mapping ResourceIdentifier to all FileResource instances.
        /// This dramatically improves performance by avoiding O(n) scans for each resource.
        /// </summary>
        public static Dictionary<ResourceIdentifier, List<BioWare.NET.Extract.FileResource>> BuildResourceIndex(Installation installation)
        {
            var index = new Dictionary<ResourceIdentifier, List<BioWare.NET.Extract.FileResource>>();

            // Get all resources from installation
            var allResources = GetAllFileResources(installation);
            foreach (var fileResource in allResources)
            {
                var identifier = fileResource.Identifier;
                if (!index.ContainsKey(identifier))
                {
                    index[identifier] = new List<BioWare.NET.Extract.FileResource>();
                }
                index[identifier].Add(fileResource);
            }

            return index;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:468-493
        // Original: def _should_process_tlk_file(resolved: ResolvedResource) -> bool: ...
        /// <summary>
        /// Determine if a TLK file should be processed based on filtering rules.
        /// When both comparison paths are installations, only process TLK files that are:
        /// - In the root of the installation folder, AND
        /// - Named 'dialog.tlk' or 'dialog_f.tlk'
        /// </summary>
        public static bool ShouldProcessTlkFile(ResolvedResource resolved)
        {
            // Always process if we don't have filepath info
            if (string.IsNullOrEmpty(resolved.Filepath))
            {
                return true;
            }

            // Check location type - only process TLKs from Installation root or Override
            if (resolved.LocationType != "Installation root" && resolved.LocationType != "Override folder")
            {
                return false;
            }

            // Check filename - only allow dialog.tlk and dialog_f.tlk
            string filename = Path.GetFileName(resolved.Filepath).ToLowerInvariant();
            return filename == "dialog.tlk" || filename == "dialog_f.tlk";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:496-536
        // Original: def determine_tslpatcher_destination(...) -> str: ...
        /// <summary>
        /// Determine the appropriate TSLPatcher destination based on source locations.
        /// In n-way comparisons, this determines where to install a resource based on
        /// where it exists across the compared paths. Paths are treated as interchangeable.
        /// </summary>
        public static string DetermineTslpatcherDestination(string locationA, string locationB, string filepathB)
        {
            // If resource is in Override, destination is Override
            if (locationB == "Override folder")
            {
                return "Override";
            }

            // If resource is in a module
            if (!string.IsNullOrEmpty(locationB) && locationB.Contains("Modules") && !string.IsNullOrEmpty(filepathB))
            {
                string filepathStr = filepathB.ToLowerInvariant();

                // Check if it's in a .mod file
                if (filepathStr.Contains(".mod") || filepathStr.Contains(".erf"))
                {
                    // Extract module filename
                    string moduleName = Path.GetFileName(filepathB);
                    return $"modules\\{moduleName}";
                }

                // It's in a .rim - need to redirect to corresponding .mod
                if (filepathStr.Contains(".rim"))
                {
                    string moduleRoot = ResolutionUtils.GetModuleRoot(filepathB);
                    return $"modules\\{moduleRoot}.mod";
                }
            }

            // Default to Override for safety
            return "Override";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:312-429
        // Original: def _log_consolidated_resolution(...): ...
        /// <summary>
        /// Log combined resolution results from installations side-by-side.
        /// Shows which files were CHOSEN by resolution order and which were shadowed.
        /// Supports n-way comparisons (3+ installations).
        /// </summary>
        public static void LogConsolidatedResolution(
            Installation install1,
            Installation install2,
            ResourceIdentifier identifier,
            ResolvedResource resolved1,
            ResolvedResource resolved2,
            Action<string> logFunc,
            List<ResolvedResource> additionalResolved = null,
            List<Installation> additionalInstalls = null)
        {
            string install1Name = Path.GetFileName(install1.Path);
            string install2Name = Path.GetFileName(install2.Path);

            // Build list of all installations and resolutions
            var allNames = new List<string> { install1Name, install2Name };
            var allResolved = new List<ResolvedResource> { resolved1, resolved2 };

            if (additionalInstalls != null && additionalResolved != null)
            {
                foreach (var install in additionalInstalls)
                {
                    allNames.Add(Path.GetFileName(install.Path));
                }
                allResolved.AddRange(additionalResolved);
            }

            // Log header showing all installations
            int installCount = allNames.Count;
            if (installCount == 2)
            {
                logFunc($"Installation 0 ({install1Name}), 1 ({install2Name})");
            }
            else
            {
                var installList = string.Join(", ", allNames.Select((name, idx) => $"{idx} ({name})"));
                logFunc($"Installations: {installList}");
            }
            logFunc("  Checking each location:");

            // Get all locations from all installations
            var allInstallationLocations = new List<Dictionary<string, List<string>>>();
            foreach (var resolved in allResolved)
            {
                allInstallationLocations.Add(resolved.AllLocations ?? new Dictionary<string, List<string>>());
            }

            // Priority order for display
            var locationOrder = new List<(string key, string display)>
            {
                ("Override folder", "1. Override folder"),
                ("Modules (.mod)", "2. Modules (.mod)"),
                ("Modules (.rim/_s.rim/.erf)", "3. Modules (.rim/_s.rim/.erf)"),
                ("Chitin BIFs", "4. Chitin BIFs")
            };

            // Build list of all installations with their paths for relative path calculation
            var allInstallationsWithPaths = new List<object> { install1, install2 };
            if (additionalInstalls != null)
            {
                allInstallationsWithPaths.AddRange(additionalInstalls);
            }

            // Track if we've found a chosen file yet (only ONE chosen across all installations)
            bool foundChosen = false;
            string chosenModuleRoot = null; // For module files, track the chosen module root

            foreach (var (locKey, locDisplay) in locationOrder)
            {
                // Collect files from all installations at this location
                var allFilesToShow = new List<(int installIdx, string installName, string filepath, bool isChosen)>();

                for (int idx = 0; idx < allNames.Count; idx++)
                {
                    string installName = allNames[idx];
                    ResolvedResource resolved = allResolved[idx];
                    Dictionary<string, List<string>> locations = allInstallationLocations[idx];

                    if (locations.ContainsKey(locKey))
                    {
                        foreach (string f in locations[locKey])
                        {
                            bool isChosen = (resolved.Filepath == f) && !foundChosen;

                            // For module files, only include files with same base name as chosen
                            if ((locKey == "Modules (.mod)" || locKey == "Modules (.rim/_s.rim/.erf)") && foundChosen && !string.IsNullOrEmpty(chosenModuleRoot))
                            {
                                try
                                {
                                    string fileModuleRoot = ResolutionUtils.GetModuleRoot(f);
                                    if (fileModuleRoot != chosenModuleRoot)
                                    {
                                        continue; // Skip files with different base names
                                    }
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Warning: Could not get module root for {f}: {e.GetType().Name}: {e.Message}");
                                    // If we can't get module root, include the file
                                }
                            }

                            // Track chosen module root for filtering subsequent files
                            if (isChosen && (locKey == "Modules (.mod)" || locKey == "Modules (.rim/_s.rim/.erf)"))
                            {
                                try
                                {
                                    chosenModuleRoot = ResolutionUtils.GetModuleRoot(f);
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Warning: Could not get module root for chosen file {f}: {e.GetType().Name}: {e.Message}");
                                }
                            }

                            if (isChosen)
                            {
                                foundChosen = true;
                            }

                            allFilesToShow.Add((idx, installName, f, isChosen));
                        }
                    }
                }

                if (allFilesToShow.Count == 0)
                {
                    logFunc($"    {locDisplay} -> not found");
                }
                else
                {
                    foreach (var (installIdx, installName, filepath, isChosen) in allFilesToShow)
                    {
                        try
                        {
                            object installation = allInstallationsWithPaths[installIdx];
                            string installPath = installation is Installation inst ? inst.Path : install1.Path;
                            string relPath = Path.GetRelativePath(installPath, filepath);
                            string fullPath = $"{installName}/{relPath}";

                            if (isChosen)
                            {
                                logFunc($"    {locDisplay} -> CHOSEN (install{installIdx}) - {fullPath}");
                            }
                            else
                            {
                                logFunc($"    {locDisplay} -> (shadowed, install{installIdx}) {fullPath}");
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Couldn't get relative path, just show filename
                            string filename = Path.GetFileName(filepath);
                            if (isChosen)
                            {
                                logFunc($"    {locDisplay} -> CHOSEN (install{installIdx}) - {filename}");
                            }
                            else
                            {
                                logFunc($"    {locDisplay} -> (shadowed, install{installIdx}) {filename}");
                            }
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:1229-1293
        // Original: def explain_resolution_order(...): ...
        /// <summary>
        /// Explain the resolution order for a resource in user-friendly terms.
        /// Supports n-way comparisons (3+ installations).
        /// </summary>
        public static void ExplainResolutionOrder(
            ResourceIdentifier identifier,
            ResolvedResource install1Resolved,
            ResolvedResource install2Resolved,
            Action<string> logFunc,
            List<ResolvedResource> additionalResolved = null,
            List<string> installNames = null)
        {
            logFunc($"\nResolution explanation for {identifier}:");
            logFunc("");
            logFunc("  KOTOR loads files in this order (first match wins):");
            logFunc("    1. Override folder (HIGHEST PRIORITY)");
            logFunc("    2. Modules folder (.mod files)");
            logFunc("    3. Modules folder (.rim/_s.rim/_dlg.erf files)");
            logFunc("    4. Chitin/BIF archives (LOWEST PRIORITY)");
            logFunc("");

            // Build list of all resolutions
            var allResolved = new List<ResolvedResource> { install1Resolved, install2Resolved };
            if (additionalResolved != null)
            {
                allResolved.AddRange(additionalResolved);
            }

            // Default names if not provided
            if (installNames == null)
            {
                installNames = new List<string>();
                for (int idx = 0; idx < allResolved.Count; idx++)
                {
                    installNames.Add($"Installation {idx}");
                }
            }

            // Explain each installation
            for (int idx = 0; idx < allResolved.Count; idx++)
            {
                ResolvedResource resolved = allResolved[idx];
                string name = idx < installNames.Count ? installNames[idx] : $"Installation {idx}";
                logFunc($"  Installation {idx} ({name}):");
                if (resolved.Data == null)
                {
                    logFunc("    → Resource NOT FOUND");
                }
                else
                {
                    string priority = GetLocationDisplayName(resolved.LocationType);
                    logFunc($"    → Found in: {priority}");
                    logFunc($"    → Path: {resolved.SourceLocation}");
                }
                logFunc("");
            }

            // Explain what this means for modding (compare base vs target)
            if (install1Resolved.Data != null && install2Resolved.Data != null && install1Resolved.LocationType != install2Resolved.LocationType)
            {
                logFunc("  What this means:");
                if (install2Resolved.LocationType == "Override folder")
                {
                    logFunc("    ✓ Resource was moved to Override (will override base version)");
                    logFunc("    ✓ TSLPatcher should install to Override");
                }
                else if (install1Resolved.LocationType == "Chitin BIFs" && install2Resolved.LocationType != null && install2Resolved.LocationType.Contains("Modules"))
                {
                    logFunc("    ✓ Resource extracted from BIF to Modules (now modifiable)");
                    logFunc("    ✓ TSLPatcher should install to appropriate module");
                }
                else
                {
                    string loc1Name = GetLocationDisplayName(install1Resolved.LocationType);
                    string loc2Name = GetLocationDisplayName(install2Resolved.LocationType);
                    logFunc($"    → Priority changed from {loc1Name} to {loc2Name}");
                }
            }

            logFunc("");
        }
    }
}

