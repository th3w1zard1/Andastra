using System;
using System.Numerics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpKOTOR.Common;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Formats.BWM;
using CSharpKOTOR.Formats.ERF;
using CSharpKOTOR.Formats.RIM;
using CSharpKOTOR.Formats.TPC;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Logger;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CSharpKOTOR.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py
    // Original: Kit generation utilities for extracting kit resources from module RIM files
    public static class Kit
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:74-110
        // Original: def _get_resource_priority(location: LocationResult, installation: Installation) -> int:
        private static int GetResourcePriority(LocationResult location, Installation.Installation installation)
        {
            string filepath = location.FilePath;
            string[] pathParts = filepath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] parentNamesLower = pathParts.Take(pathParts.Length - 1).Select(p => p.ToLower()).ToArray();

            if (parentNamesLower.Any(p => p == "override"))
            {
                return 0;
            }
            if (parentNamesLower.Any(p => p == "modules"))
            {
                string nameLower = Path.GetFileName(filepath).ToLower();
                if (nameLower.EndsWith(".mod"))
                {
                    return 1;
                }
                return 2; // .rim/_s.rim/_dlg.erf
            }
            if (parentNamesLower.Any(p => p == "data") || Path.GetExtension(filepath).ToLower() == ".bif")
            {
                return 3;
            }
            // Files directly in installation root treated as Override priority
            if (Path.GetDirectoryName(filepath) == installation.Path)
            {
                return 0;
            }
            // Default to lowest priority if unknown
            return 3;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:262-288
        // Original: def find_module_file(installation: Installation, module_name: str) -> Path | None:
        public static string FindModuleFile(Installation.Installation installation, string moduleName)
        {
            string rimsPath = CSharpKOTOR.Installation.Installation.GetRimsPath(installation.Path);
            string modulesPath = installation.ModulePath();

            // Check rimsPath first, then modulesPath
            if (!string.IsNullOrEmpty(rimsPath) && Directory.Exists(rimsPath))
            {
                string mainRim = Path.Combine(rimsPath, $"{moduleName}.rim");
                if (File.Exists(mainRim))
                {
                    return mainRim;
                }
            }
            if (!string.IsNullOrEmpty(modulesPath) && Directory.Exists(modulesPath))
            {
                string mainRim = Path.Combine(modulesPath, $"{moduleName}.rim");
                if (File.Exists(mainRim))
                {
                    return mainRim;
                }
            }
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:186-259
        // Original: def _get_component_name_mapping(kit_id: str | None, model_names: list[str]) -> dict[str, str]:
        private static Dictionary<string, string> GetComponentNameMapping(string kitId, List<string> modelNames)
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>();

            // Kit-specific mappings for known kits
            if (kitId == "sithbase")
            {
                Dictionary<string, string> sithbaseMapping = new Dictionary<string, string>
                {
                    { "m09aa_01a", "armory_1" },
                    { "m09aa_02a", "barracks_1" },
                    { "m09aa_03a", "control_1" },
                    { "m09aa_05a", "control_2" },
                    { "m09aa_06a", "hall_1" },
                    { "m09aa_07a", "hall_2" },
                };
                // Apply mapping for known models
                foreach (string modelName in modelNames)
                {
                    string modelLower = modelName.ToLower();
                    if (sithbaseMapping.ContainsKey(modelLower))
                    {
                        mapping[modelLower] = sithbaseMapping[modelLower];
                    }
                    else
                    {
                        // For unmapped models, use a sanitized version of the model name
                        string cleanName = modelLower;
                        if (cleanName.Contains("_"))
                        {
                            string[] parts = cleanName.Split(new[] { '_' }, 2);
                            if (parts.Length > 1)
                            {
                                string firstPart = parts[0];
                                // Only check length: typical KOTOR module prefixes are 4-6 characters
                                if (firstPart.Length >= 4 && firstPart.Length <= 6)
                                {
                                    // Remove module prefix
                                    cleanName = $"component_{parts[1]}";
                                }
                                else
                                {
                                    // Keep full name with component_ prefix
                                    cleanName = $"component_{modelLower}";
                                }
                            }
                        }
                        else
                        {
                            cleanName = $"component_{modelLower}";
                        }
                        mapping[modelLower] = cleanName;
                    }
                }
            }

            // Default: use model names as-is (sanitized)
            if (mapping.Count == 0)
            {
                foreach (string modelName in modelNames)
                {
                    string modelLower = modelName.ToLower();
                    // Sanitize model name for use as component ID
                    string cleanName = modelLower;
                    if (cleanName.Contains("_"))
                    {
                        string[] parts = cleanName.Split(new[] { '_' }, 2);
                        if (parts.Length > 1)
                        {
                            string firstPart = parts[0];
                            // Only check length: typical KOTOR module prefixes are 4-6 characters
                            if (firstPart.Length >= 4 && firstPart.Length <= 6)
                            {
                                // Remove module prefix (e.g., "m09aa" from "m09aa_01a")
                                cleanName = parts[1];
                            }
                        }
                    }
                    mapping[modelLower] = cleanName;
                }
            }

            return mapping;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:1538-1588
        // Original: def _recenter_bwm(bwm: BWM) -> BWM:
        private static BWM RecenterBwm(BWM bwm)
        {
            List<Vector3> vertices = bwm.Vertices();
            if (vertices.Count == 0)
            {
                return bwm;
            }

            // Calculate current center
            float minX = vertices.Min(v => v.X);
            float maxX = vertices.Max(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxY = vertices.Max(v => v.Y);
            float minZ = vertices.Min(v => v.Z);
            float maxZ = vertices.Max(v => v.Z);

            float centerX = (minX + maxX) / 2.0f;
            float centerY = (minY + maxY) / 2.0f;
            float centerZ = (minZ + maxZ) / 2.0f;

            // Translate all vertices to center around origin
            // Use BWM.translate() which handles all vertices in faces
            bwm.Translate(-centerX, -centerY, -centerZ);

            return bwm;
        }

        // Note: The full extract_kit function is very large (1589 lines in Python)
        // This is a placeholder that will need to be fully implemented
        // The complete implementation requires:
        // - Archive loading (RIM/ERF)
        // - Resource extraction and organization
        // - Component identification from LYT
        // - Texture/lightmap extraction
        // - Door/placeable walkmesh extraction
        // - JSON generation
        // - Minimap generation (requires image library - Qt/PIL equivalent)
        // - Doorhook extraction from BWM edges
        
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:291-1346
        // Original: def extract_kit(...)
        public static void ExtractKit(
            Installation.Installation installation,
            string moduleName,
            string outputPath,
            string kitId = null,
            RobustLogger logger = null)
        {
            if (logger == null)
            {
                logger = new RobustLogger();
            }

            Directory.CreateDirectory(outputPath);

            // Sanitize module name and extract clean name
            string moduleNameClean = Path.GetFileNameWithoutExtension(moduleName).ToLower();
            logger.Info($"Processing module: {moduleNameClean}");

            if (string.IsNullOrEmpty(kitId))
            {
                kitId = moduleNameClean;
            }

            // Sanitize kit_id (remove invalid filename characters)
            kitId = Regex.Replace(kitId, @"[<>:""/\\|?*]", "_");
            kitId = kitId.Trim('.', ' ');
            if (string.IsNullOrEmpty(kitId))
            {
                kitId = moduleNameClean;
            }
            kitId = kitId.ToLower();

            // TODO: Full implementation of extract_kit function
            // This requires extensive work to match the 1589-line Python implementation
            // Key components:
            // 1. Archive loading (RIM/ERF detection and loading)
            // 2. Resource collection from archives
            // 3. Module instance creation for LYT/GIT access
            // 4. Component identification from LYT room models
            // 5. Texture/lightmap extraction with batch lookups
            // 6. Door/placeable walkmesh extraction (DWK/PWK)
            // 7. Component name mapping
            // 8. BWM re-centering
            // 9. Minimap generation (requires image library)
            // 10. Doorhook extraction
            // 11. JSON file generation
            // 12. File writing (components, textures, lightmaps, doors, etc.)
            
            logger.Warning("ExtractKit is not fully implemented yet. This is a placeholder.");
            throw new NotImplementedException("ExtractKit full implementation is in progress. This requires porting 1589 lines of Python code including image generation, batch resource lookups, and complex resource organization logic.");
        }
    }
}

