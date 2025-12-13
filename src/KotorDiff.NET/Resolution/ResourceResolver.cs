// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:86-310
// Original: def resolve_resource_in_installation(...): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using KotorDiff.NET.Diff;
using KotorDiff.NET.Logger;
using JetBrains.Annotations;

namespace KotorDiff.NET.Resolution
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
            Dictionary<ResourceIdentifier, List<FileResource>> resourceIndex = null)
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
            var resourceInstances = new Dictionary<string, FileResource>();

            try
            {
                // Use index if provided (O(1) lookup), otherwise iterate (O(n) scan)
                List<FileResource> fileResources;
                if (resourceIndex != null)
                {
                    fileResources = resourceIndex.ContainsKey(identifier) ? resourceIndex[identifier] : new List<FileResource>();
                }
                else
                {
                    // Fallback to iteration if no index provided
                    // TODO: Implement Installation iteration if needed
                    fileResources = new List<FileResource>();
                }

                // Categorize all instances by location
                string installRoot = installation.Path;

                // Group module files by their basename for proper composite handling
                var moduleGroups = new Dictionary<string, List<(string filepath, FileResource resource)>>();

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
                            string moduleRoot = DiffEngineUtils.GetModuleRoot(filepath);
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
    }
}

