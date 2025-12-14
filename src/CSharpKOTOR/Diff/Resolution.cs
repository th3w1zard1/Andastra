using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;

namespace CSharpKOTOR.Diff
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:34-63
    // Original: @dataclass class TLKModificationWithSource:
    /// <summary>
    /// Wrapper that associates a TLK modification with its source path.
    /// </summary>
    public class TLKModificationWithSource
    {
        public Mods.TLK.ModificationsTLK Modification { get; set; }
        public object SourcePath { get; set; } // Installation or Path
        public int SourceIndex { get; set; }
        public bool IsInstallation { get; set; }
        public Dictionary<int, int> StrrefMappings { get; set; } = new Dictionary<int, int>(); // old_strref -> token_id
        public string SourceFilepath { get; set; } // Base installation TLK path for reference finding
        public Installation.Installation SourceInstallation { get; set; } // Base installation for reference finding

        public TLKModificationWithSource(
            Mods.TLK.ModificationsTLK modification,
            object sourcePath,
            int sourceIndex,
            bool isInstallation = false)
        {
            Modification = modification;
            SourcePath = sourcePath;
            SourceIndex = sourceIndex;
            IsInstallation = isInstallation;
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:65-75
    // Original: @dataclass class ResolvedResource:
    /// <summary>
    /// A resource resolved through the game's priority order.
    /// </summary>
    public class ResolvedResource
    {
        public ResourceIdentifier Identifier { get; set; }
        public byte[] Data { get; set; }
        public string SourceLocation { get; set; } // Human-readable description
        public string LocationType { get; set; } // Type of location (Override, Modules, Chitin, etc.)
        public string Filepath { get; set; } // Full path to the file
        public Dictionary<string, List<string>> AllLocations { get; set; } // All locations where resource was found

        public ResolvedResource(
            ResourceIdentifier identifier,
            byte[] data,
            string sourceLocation,
            string locationType,
            string filepath,
            Dictionary<string, List<string>> allLocations = null)
        {
            Identifier = identifier;
            Data = data;
            SourceLocation = sourceLocation;
            LocationType = locationType;
            Filepath = filepath;
            AllLocations = allLocations ?? new Dictionary<string, List<string>>();
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:77-83
    // Original: def get_location_display_name(location_type: str | None) -> str:
    /// <summary>
    /// Get human-readable name for a location type.
    /// </summary>
    public static class Resolution
    {
        public static string GetLocationDisplayName(string locationType)
        {
            if (string.IsNullOrEmpty(locationType))
            {
                return "Not Found";
            }
            return locationType;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:431-446
        // Original: def collect_all_resource_identifiers(installation: Installation) -> set[ResourceIdentifier]:
        /// <summary>
        /// Collect all unique resource identifiers from an installation.
        /// </summary>
        public static HashSet<ResourceIdentifier> CollectAllResourceIdentifiers(Installation.Installation installation)
        {
            HashSet<ResourceIdentifier> identifiers = new HashSet<ResourceIdentifier>();
            foreach (FileResource fileResource in installation)
            {
                identifiers.Add(fileResource.Identifier);
            }
            return identifiers;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:449-465
        // Original: def build_resource_index(installation: Installation) -> dict[ResourceIdentifier, list[FileResource]]:
        /// <summary>
        /// Build an index mapping ResourceIdentifier to all FileResource instances.
        /// This dramatically improves performance by avoiding O(n) scans for each resource.
        /// </summary>
        public static Dictionary<ResourceIdentifier, List<FileResource>> BuildResourceIndex(Installation.Installation installation)
        {
            Dictionary<ResourceIdentifier, List<FileResource>> index = new Dictionary<ResourceIdentifier, List<FileResource>>();
            foreach (FileResource fileResource in installation)
            {
                ResourceIdentifier ident = fileResource.Identifier;
                if (!index.ContainsKey(ident))
                {
                    index[ident] = new List<FileResource>();
                }
                index[ident].Add(fileResource);
            }
            return index;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:496-536
        // Original: def determine_tslpatcher_destination(location_a: str | None, location_b: str | None, filepath_b: Path | None) -> str:
        /// <summary>
        /// Determine the appropriate TSLPatcher destination based on source locations.
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
                    string moduleName = System.IO.Path.GetFileName(filepathB);
                    return $"modules\\{moduleName}";
                }

                // It's in a .rim - need to redirect to corresponding .mod
                if (filepathStr.Contains(".rim"))
                {
                    // TODO: Implement get_module_root equivalent
                    // For now, extract from filename
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(filepathB);
                    // Remove _s suffix if present
                    if (fileName.EndsWith("_s", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = fileName.Substring(0, fileName.Length - 2);
                    }
                    return $"modules\\{fileName}.mod";
                }
            }

            // Default to Override for safety
            return "Override";
        }
    }
}
