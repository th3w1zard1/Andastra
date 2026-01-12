using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Resource.Formats.BWM;
using Andastra.Parsing.Resource.Formats.ERF;
using Andastra.Parsing.Resource.Formats.RIM;
using Andastra.Parsing.Resource.Formats.TPC;
using Andastra.Parsing.Resource.Formats.TwoDA;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Resource.Formats.GFF.Generics;
using Andastra.Parsing.Common.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vector2 = System.Numerics.Vector2;

namespace Andastra.Parsing.Tools
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
            // Files directly in Andastra.Parsing.Installation.Installation root treated as Override priority
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
            string rimsPath = Installation.Installation.GetRimsPath(installation.Path);
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

        /// <summary>
        /// Re-centers a walkmesh around the origin (0, 0, 0).
        /// This is CRITICAL for proper component alignment in kit extraction.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method moves all vertices in the walkmesh so that the walkmesh's center point
        /// is at (0, 0, 0). The center is calculated as the midpoint between the minimum and
        /// maximum X, Y, and Z coordinates of all vertices.
        ///
        /// WHY THIS IS CRITICAL:
        ///
        /// When extracting kits from game modules, the walkmeshes are in "world coordinates" -
        /// they have absolute positions in the game world. However, kits need walkmeshes centered
        /// at (0, 0, 0) because:
        ///
        /// 1. PREVIEW IMAGE ALIGNMENT:
        ///    - The preview image is generated from the walkmesh and drawn CENTERED at the
        ///      component's position in the builder UI
        ///    - If the walkmesh isn't centered, the image and walkmesh won't align visually
        ///    - Users will see the image in one place but the walkmesh hitbox in another
        ///
        /// 2. POSITIONING LOGIC:
        ///    - When a component is placed at position (50, 50, 0), the walkmesh is translated
        ///      by that amount from its ORIGINAL coordinates
        ///    - If the walkmesh starts at (100, 200, 0), translating by (50, 50, 0) gives
        ///      (150, 250, 0) - which is NOT where the user expects it
        ///    - If the walkmesh is centered at (0, 0, 0), translating by (50, 50, 0) gives
        ///      (50, 50, 0) - which IS where the user expects it
        ///
        /// 3. TRANSFORMATION CONSISTENCY:
        ///    - Components can be rotated and flipped
        ///    - Rotations and flips are applied around the origin (0, 0, 0)
        ///    - If the walkmesh isn't centered, rotations/flips will move it unexpectedly
        ///
        /// HOW IT WORKS:
        ///
        /// 1. Get all vertices from the walkmesh
        /// 2. Find the minimum and maximum X, Y, Z values across all vertices
        /// 3. Calculate the center: center = (min + max) / 2
        /// 4. Translate all vertices by -center (move walkmesh so center is at origin)
        ///
        /// Example:
        /// - Vertices range from (100, 200, 0) to (200, 300, 0)
        /// - Center = ((100+200)/2, (200+300)/2, (0+0)/2) = (150, 250, 0)
        /// - Translate by (-150, -250, 0)
        /// - New vertices range from (-50, -50, 0) to (50, 50, 0)
        /// - Walkmesh is now centered at (0, 0, 0)
        ///
        /// BUG PREVENTION:
        ///
        /// Without this fix, the following bugs occur:
        /// - Preview images don't match walkmesh positions in the builder
        /// - Components appear in wrong locations when placed
        /// - Rotations/flips move components unexpectedly
        /// - Walkmesh hitboxes don't align with visual representation
        ///
        /// This method is called during kit extraction (ExtractKit) after loading walkmeshes
        /// from game modules, ensuring all components are properly centered before use.
        ///
        /// ORIGINAL IMPLEMENTATION:
        ///
        /// Based on PyKotor's _recenter_bwm() method. This fix addresses a critical alignment
        /// issue where game walkmeshes (in world coordinates) don't match the builder's expectations
        /// (centered coordinates).
        /// </remarks>
        /// <param name="bwm">The walkmesh to re-center</param>
        /// <returns>The re-centered walkmesh (same instance, modified in place)</returns>
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
            // Find the bounding box of all vertices
            float minX = vertices.Min(v => v.X);
            float maxX = vertices.Max(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxY = vertices.Max(v => v.Y);
            float minZ = vertices.Min(v => v.Z);
            float maxZ = vertices.Max(v => v.Z);

            // Calculate center point (midpoint of bounding box)
            float centerX = (minX + maxX) / 2.0f;
            float centerY = (minY + maxY) / 2.0f;
            float centerZ = (minZ + maxZ) / 2.0f;

            // Translate all vertices to center around origin
            // Use BWM.translate() which handles all vertices in faces
            // This moves the walkmesh so its center is at (0, 0, 0)
            bwm.Translate(-centerX, -centerY, -centerZ);

            return bwm;
        }

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

            // Determine file type from extension
            string extension = Path.GetExtension(moduleName)?.ToLower();
            bool isErf = extension != null && (extension == ".erf" || extension == ".mod" || extension == ".hak" || extension == ".sav");
            bool isRim = extension == ".rim";

            string rimsPath = Installation.Installation.GetRimsPath(installation.Path);
            string modulesPath = installation.ModulePath();

            RIM mainArchive = null;
            RIM dataArchive = null;
            ERF mainErfArchive = null;
            bool usingDotMod = false;

            if (isErf)
            {
                // ERF file specified - try to load it directly or search for it
                logger.Info($"Detected ERF format from extension: {extension}");
                string erfPath = null;

                // If it's a full path, use it directly
                if (Path.IsPathRooted(moduleName) || File.Exists(moduleName))
                {
                    erfPath = moduleName;
                }
                else
                {
                    // Search in modules directory - prioritize .mod files (they override .rim files)
                    string[] erfExtensions = { ".mod", ".erf", ".hak", ".sav" };
                    foreach (string ext in erfExtensions)
                    {
                        string candidate = Path.Combine(modulesPath, $"{moduleNameClean}{ext}");
                        if (File.Exists(candidate))
                        {
                            erfPath = candidate;
                            if (ext == ".mod")
                            {
                                usingDotMod = true;
                                logger.Info($"Found .mod file: {candidate} - will use .mod format");
                            }
                            break;
                        }
                    }
                }

                if (erfPath != null && File.Exists(erfPath))
                {
                    logger.Info($"Loading ERF file: {erfPath}");
                    if (extension == ".mod" || Path.GetExtension(erfPath).ToLower() == ".mod")
                    {
                        usingDotMod = true;
                        logger.Info("Detected .mod file - will use .mod format for Module class");
                    }
                    try
                    {
                        mainErfArchive = ERFAuto.ReadErf(erfPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Failed to read ERF file '{erfPath}': {e}");
                        throw;
                    }
                }
                else
                {
                    throw new FileNotFoundException($"ERF file not found: {moduleName}");
                }
            }
            else if (isRim)
            {
                // RIM file specified - try to load it directly or search for it
                logger.Info("Detected RIM format from extension");
                string rimPath = null;

                // If it's a full path, use it directly
                if (Path.IsPathRooted(moduleName) || File.Exists(moduleName))
                {
                    rimPath = moduleName;
                }
                else
                {
                    // Search in rims and modules directories
                    foreach (string searchPath in new[] { rimsPath, modulesPath })
                    {
                        if (!string.IsNullOrEmpty(searchPath) && Directory.Exists(searchPath))
                        {
                            string candidate = Path.Combine(searchPath, $"{moduleNameClean}.rim");
                            if (File.Exists(candidate))
                            {
                                rimPath = candidate;
                                break;
                            }
                        }
                    }
                }

                if (rimPath != null && File.Exists(rimPath))
                {
                    logger.Info($"Loading RIM file: {rimPath}");
                    try
                    {
                        mainArchive = RIMAuto.ReadRim(rimPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Failed to read RIM file '{rimPath}': {e}");
                        throw;
                    }
                }
                else
                {
                    throw new FileNotFoundException($"RIM file not found: {moduleName}");
                }
            }
            else
            {
                // No extension - search for both RIM and ERF files
                // PRIORITY: .mod files take precedence over .rim files (as per KOTOR resolution order)
                logger.Info("No extension detected, searching for RIM or ERF files...");

                // FIRST: Check for .mod file (highest priority - .mod files override .rim files)
                string erfPath = null;
                if (!string.IsNullOrEmpty(modulesPath) && Directory.Exists(modulesPath))
                {
                    // Check for .mod first (highest priority)
                    string modCandidate = Path.Combine(modulesPath, $"{moduleNameClean}.mod");
                    if (File.Exists(modCandidate))
                    {
                        erfPath = modCandidate;
                        usingDotMod = true;
                        logger.Info($"Found .mod file: {erfPath} (using .mod format, will ignore .rim files)");
                    }
                    else
                    {
                        // Try other ERF files (but not .mod, already checked)
                        string[] erfExtensions = { ".erf", ".hak", ".sav" };
                        foreach (string ext in erfExtensions)
                        {
                            string candidate = Path.Combine(modulesPath, $"{moduleNameClean}{ext}");
                            if (File.Exists(candidate))
                            {
                                erfPath = candidate;
                                break;
                            }
                        }
                    }
                }

                // If .mod file found, use it (don't check for .rim files)
                if (erfPath != null && File.Exists(erfPath) && usingDotMod)
                {
                    logger.Info($"Loading .mod file: {erfPath}");
                    try
                    {
                        mainErfArchive = ERFAuto.ReadErf(erfPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Failed to read ERF file '{erfPath}': {e}");
                        throw;
                    }
                }
                else if (erfPath != null && File.Exists(erfPath))
                {
                    // Other ERF file (not .mod)
                    logger.Info($"Found ERF file: {erfPath}");
                    try
                    {
                        mainErfArchive = ERFAuto.ReadErf(erfPath);
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Failed to read ERF file '{erfPath}': {e}");
                        throw;
                    }
                }
                else
                {
                    // No ERF file found, try RIM files
                    string mainRimPath = null;
                    string dataRimPath = null;

                    foreach (string searchPath in new[] { rimsPath, modulesPath })
                    {
                        if (!string.IsNullOrEmpty(searchPath) && Directory.Exists(searchPath))
                        {
                            string candidateMain = Path.Combine(searchPath, $"{moduleNameClean}.rim");
                            string candidateData = Path.Combine(searchPath, $"{moduleNameClean}_s.rim");
                            if (File.Exists(candidateMain))
                            {
                                mainRimPath = candidateMain;
                            }
                            if (File.Exists(candidateData))
                            {
                                dataRimPath = candidateData;
                            }
                        }
                    }

                    if (mainRimPath != null || dataRimPath != null)
                    {
                        logger.Info($"Found RIM files: main={mainRimPath}, data={dataRimPath}");
                        if (mainRimPath != null && File.Exists(mainRimPath))
                        {
                            try
                            {
                                mainArchive = RIMAuto.ReadRim(mainRimPath);
                            }
                            catch (Exception e)
                            {
                                logger.Error($"Failed to read RIM file '{mainRimPath}': {e}");
                                throw;
                            }
                        }
                        if (dataRimPath != null && File.Exists(dataRimPath))
                        {
                            try
                            {
                                dataArchive = RIMAuto.ReadRim(dataRimPath);
                            }
                            catch (Exception e)
                            {
                                logger.Error($"Failed to read RIM file '{dataRimPath}': {e}");
                                throw;
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException($"Neither RIM nor ERF files found for module '{moduleNameClean}'");
                    }
                }
            }

            if (mainArchive == null && dataArchive == null && mainErfArchive == null)
            {
                throw new FileNotFoundException($"No valid archive files found for module '{moduleNameClean}'");
            }

            // Collect all resources from archive files
            Dictionary<(string resname, ResourceType restype), byte[]> allResources = new Dictionary<(string, ResourceType), byte[]>();
            logger.Info("Collecting resources from archive files...");

            // Process RIM archives
            foreach (RIM archive in new[] { mainArchive, dataArchive })
            {
                if (archive == null)
                {
                    continue;
                }
                int resourceCount = 0;
                foreach (var resource in archive)
                {
                    string keyResname = resource.ResRef.ToString().ToLower();
                    var key = (keyResname, resource.ResType);
                    if (!allResources.ContainsKey(key))
                    {
                        allResources[key] = resource.Data;
                        resourceCount++;
                    }
                }
                logger.Info($"  Extracted {resourceCount} resources from archive");
            }

            // Process ERF archive
            if (mainErfArchive != null)
            {
                int resourceCount = 0;
                foreach (var resource in mainErfArchive)
                {
                    string keyResname = resource.ResRef.ToString().ToLower();
                    var key = (keyResname, resource.ResType);
                    if (!allResources.ContainsKey(key))
                    {
                        allResources[key] = resource.Data;
                        resourceCount++;
                    }
                }
                logger.Info($"  Extracted {resourceCount} resources from ERF archive");
            }

            logger.Info($"Total unique resources collected: {allResources.Count}");

            // Organize resources by type
            Dictionary<string, Dictionary<string, byte[]>> components = new Dictionary<string, Dictionary<string, byte[]>>(); // component_id -> {mdl, mdx, wok}
            Dictionary<string, byte[]> textures = new Dictionary<string, byte[]>(); // texture_name -> tga_data
            Dictionary<string, byte[]> textureTxis = new Dictionary<string, byte[]>(); // texture_name -> txi_data
            Dictionary<string, byte[]> lightmaps = new Dictionary<string, byte[]>(); // lightmap_name -> tga_data
            Dictionary<string, byte[]> lightmapTxis = new Dictionary<string, byte[]>(); // lightmap_name -> txi_data
            Dictionary<string, byte[]> doors = new Dictionary<string, byte[]>(); // door_name -> utd_data
            Dictionary<string, Dictionary<string, byte[]>> doorWalkmeshes = new Dictionary<string, Dictionary<string, byte[]>>(); // door_name -> {dwk0, dwk1, dwk2}
            Dictionary<string, byte[]> placeables = new Dictionary<string, byte[]>(); // placeable_name -> utp_data
            Dictionary<string, byte[]> placeableWalkmeshes = new Dictionary<string, byte[]>(); // placeable_model_name -> pwk_data
            Dictionary<string, Dictionary<string, byte[]>> skyboxes = new Dictionary<string, Dictionary<string, byte[]>>(); // skybox_name -> {mdl, mdx}
            Dictionary<string, Dictionary<string, byte[]>> allModels = new Dictionary<string, Dictionary<string, byte[]>>(); // model_name -> {mdl, mdx} (all models, not just components)

            // Create a Module instance to access all module resources (including from chitin)
            // This is needed to get LYT room models and GIT placeables/doors
            // Use use_dot_mod=True if we detected a .mod file (it takes priority over .rim files)
            Module module = new Module(moduleNameClean, installation, useDotMod: usingDotMod);
            if (usingDotMod)
            {
                logger.Info($"Using .mod format for Module class (module_name: {moduleNameClean})");
            }
            else
            {
                logger.Info($"Using .rim format for Module class (module_name: {moduleNameClean})");
            }

            // Get LYT to find all room models (which have WOK files)
            // Reference: vendor/reone/src/libs/game/object/area.cpp (room loading)
            // Reference: vendor/KotOR.js/src/module/ModuleRoom.ts:331-342 (loadWalkmesh)
            ModuleResource lytResource = module.Layout();
            HashSet<string> lytRoomModels = new HashSet<string>(); // Store lowercase room model names
            List<string> lytRoomModelNames = new List<string>(); // Store original room model names (for resource lookup)
            LYT lyt = null;
            if (lytResource != null)
            {
                object lytObj = lytResource.Resource();
                if (lytObj is LYT lytData)
                {
                    lyt = lytData;
                    // Store original room model names for resource lookup
                    lytRoomModelNames = lyt.Rooms.Select(r => r.Model.ToString()).ToList();
                    // Normalize all room model names to lowercase for consistent comparison
                    lytRoomModels = new HashSet<string>(lytRoomModelNames.Select(m => m.ToLower()));
                    // Extract WOK files for all LYT room models (even if not in RIM)
                    // Batch lookup to avoid repeated installation.locations() calls (performance optimization)
                    // Reference: LYT.iter_resource_identifiers() yields WOK for each room
                    List<ResourceIdentifier> wokIdentifiers = lytRoomModelNames.Select(roomModel => new ResourceIdentifier(roomModel, ResourceType.WOK)).ToList();
                    if (wokIdentifiers.Count > 0)
                    {
                        Dictionary<ResourceIdentifier, List<LocationResult>> wokLocations = installation.Locations(
                            wokIdentifiers,
                            new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.CHITIN }
                        );
                        // Pre-sort all WOK location lists by priority once to avoid repeated sorting
                        foreach (var kvp in wokLocations)
                        {
                            if (kvp.Value != null && kvp.Value.Count > 0)
                            {
                                wokLocations[kvp.Key] = kvp.Value.OrderBy(loc => GetResourcePriority(loc, installation)).ToList();
                            }
                        }
                        foreach (string roomModel in lytRoomModelNames)
                        {
                            ResourceIdentifier wokIdent = new ResourceIdentifier(roomModel, ResourceType.WOK);
                            if (wokLocations.ContainsKey(wokIdent) && wokLocations[wokIdent].Count > 0)
                            {
                                // Location list is already sorted by priority (done in batch lookup)
                                LocationResult location = wokLocations[wokIdent][0];
                                try
                                {
                                    using (FileStream f = File.OpenRead(location.FilePath))
                                    {
                                        f.Seek(location.Offset, SeekOrigin.Begin);
                                        byte[] wokData = new byte[location.Size];
                                        f.Read(wokData, 0, location.Size);
                                        // Add WOK to all_resources if not already present (use lowercase key)
                                        string wokKeyResname = roomModel.ToLower();
                                        var wokKey = (wokKeyResname, ResourceType.WOK);
                                        if (!allResources.ContainsKey(wokKey))
                                        {
                                            allResources[wokKey] = wokData;
                                        }
                                    }
                                }
                                catch
                                {
                                    // WOK not found or couldn't be read
                                }
                            }
                        }
                    }
                }
            }

            // Identify components (MDL files that have corresponding WOK files)
            // Components are room models from LYT that have WOK walkmeshes
            logger.Info($"Found {lytRoomModels.Count} room models in LYT: {string.Join(", ", lytRoomModels.Take(10))}...");

            // First, identify components directly from LYT room models
            // Batch lookup MDL/MDX/WOK for all room models to avoid repeated installation.locations() calls (performance optimization)
            // Use installation-wide resolution to respect priority order: Override > Modules (.mod) > Modules (.rim) > Chitin
            List<ResourceIdentifier> componentResourceIdentifiers = new List<ResourceIdentifier>();
            foreach (string roomModel in lytRoomModelNames)
            {
                componentResourceIdentifiers.Add(new ResourceIdentifier(roomModel, ResourceType.MDL));
                componentResourceIdentifiers.Add(new ResourceIdentifier(roomModel, ResourceType.MDX));
                componentResourceIdentifiers.Add(new ResourceIdentifier(roomModel, ResourceType.WOK));
            }

            // Batch lookup all component resources
            Dictionary<ResourceIdentifier, List<LocationResult>> componentLocations = new Dictionary<ResourceIdentifier, List<LocationResult>>();
            if (componentResourceIdentifiers.Count > 0)
            {
                componentLocations = installation.Locations(
                    componentResourceIdentifiers,
                    new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.CHITIN }
                );
                // Pre-sort all component location lists by priority once to avoid repeated sorting
                foreach (var kvp in componentLocations)
                {
                    if (kvp.Value != null && kvp.Value.Count > 0)
                    {
                        componentLocations[kvp.Key] = kvp.Value.OrderBy(loc => GetResourcePriority(loc, installation)).ToList();
                    }
                }
            }

            foreach (string roomModel in lytRoomModelNames)
            {
                string roomModelLower = roomModel.ToLower();

                // Get MDL, MDX, and WOK from batched results
                // This ensures we get the highest priority version (e.g., from Override if it exists)
                ResourceIdentifier mdlIdent = new ResourceIdentifier(roomModel, ResourceType.MDL);
                ResourceIdentifier mdxIdent = new ResourceIdentifier(roomModel, ResourceType.MDX);
                ResourceIdentifier wokIdent = new ResourceIdentifier(roomModel, ResourceType.WOK);

                byte[] mdlData = null;
                byte[] mdxDataRaw = null;
                byte[] wokData = null;

                // Resolve MDL
                if (componentLocations.ContainsKey(mdlIdent) && componentLocations[mdlIdent].Count > 0)
                {
                    // Location list is already sorted by priority (done in batch lookup)
                    LocationResult mdlLocation = componentLocations[mdlIdent][0];
                    try
                    {
                        using (FileStream f = File.OpenRead(mdlLocation.FilePath))
                        {
                            f.Seek(mdlLocation.Offset, SeekOrigin.Begin);
                            mdlData = new byte[mdlLocation.Size];
                            f.Read(mdlData, 0, mdlLocation.Size);
                        }
                    }
                    catch
                    {
                        // MDL not found or couldn't be read
                    }
                }

                // Resolve MDX
                if (componentLocations.ContainsKey(mdxIdent) && componentLocations[mdxIdent].Count > 0)
                {
                    // Location list is already sorted by priority (done in batch lookup)
                    LocationResult mdxLocation = componentLocations[mdxIdent][0];
                    try
                    {
                        using (FileStream f = File.OpenRead(mdxLocation.FilePath))
                        {
                            f.Seek(mdxLocation.Offset, SeekOrigin.Begin);
                            mdxDataRaw = new byte[mdxLocation.Size];
                            f.Read(mdxDataRaw, 0, mdxLocation.Size);
                        }
                    }
                    catch
                    {
                        // MDX not found or couldn't be read
                    }
                }

                // Resolve WOK
                if (componentLocations.ContainsKey(wokIdent) && componentLocations[wokIdent].Count > 0)
                {
                    // Location list is already sorted by priority (done in batch lookup)
                    LocationResult wokLocation = componentLocations[wokIdent][0];
                    try
                    {
                        using (FileStream f = File.OpenRead(wokLocation.FilePath))
                        {
                            f.Seek(wokLocation.Offset, SeekOrigin.Begin);
                            wokData = new byte[wokLocation.Size];
                            f.Read(wokData, 0, wokLocation.Size);
                        }
                    }
                    catch
                    {
                        // WOK not found or couldn't be read
                    }
                }

                if (mdlData != null && wokData != null)
                {
                    // Ensure mdx_data is bytes (not None)
                    byte[] mdxData = mdxDataRaw ?? new byte[0];

                    // Store in components
                    components[roomModelLower] = new Dictionary<string, byte[]>
                    {
                        { "mdl", mdlData },
                        { "mdx", mdxData },
                        { "wok", wokData }
                    };

                    // Also store in all_models
                    if (!allModels.ContainsKey(roomModelLower))
                    {
                        allModels[roomModelLower] = new Dictionary<string, byte[]>
                        {
                            { "mdl", mdlData },
                            { "mdx", mdxData }
                        };
                    }

                    // Ensure WOK is in all_resources for consistency
                    var wokKey = (roomModelLower, ResourceType.WOK);
                    if (!allResources.ContainsKey(wokKey))
                    {
                        allResources[wokKey] = wokData;
                    }

                    logger.Debug($"Identified component: {roomModelLower} (room model with WOK, resolved with priority)");
                }
                else
                {
                    logger.Debug($"Skipping room model {roomModelLower}: MDL or WOK resource not found");
                }
            }

            // Also check resources from RIM/ERF archives for components (in case some are there)
            foreach (var kvp in allResources)
            {
                string resname = kvp.Key.resname;
                ResourceType restype = kvp.Key.restype;
                byte[] data = kvp.Value;

                if (restype == ResourceType.MDL)
                {
                    // All keys in all_resources are lowercase, so use lowercase for lookups
                    string resnameLower = resname.ToLower(); // resname is already lowercase from all_resources keys
                    var wokKey = (resnameLower, ResourceType.WOK);
                    var mdxKey = (resnameLower, ResourceType.MDX);

                    // Store all models (for comprehensive extraction)
                    if (!allModels.ContainsKey(resnameLower))
                    {
                        allModels[resnameLower] = new Dictionary<string, byte[]>
                        {
                            { "mdl", data },
                            { "mdx", allResources.ContainsKey(mdxKey) ? allResources[mdxKey] : new byte[0] }
                        };
                    }

                    // Components are room models (from LYT) with WOK files
                    // Only add if not already added from LYT room models above
                    bool isRoomModel = lytRoomModels.Contains(resnameLower);
                    bool hasWok = allResources.ContainsKey(wokKey);
                    if (isRoomModel && hasWok && !components.ContainsKey(resnameLower))
                    {
                        components[resnameLower] = new Dictionary<string, byte[]>
                        {
                            { "mdl", data },
                            { "mdx", allResources.ContainsKey(mdxKey) ? allResources[mdxKey] : new byte[0] },
                            { "wok", allResources[wokKey] }
                        };
                        logger.Debug($"Identified component: {resnameLower} (room model with WOK from RIM)");
                    }
                }
                else if (restype == ResourceType.UTD)
                {
                    doors[resname] = data;
                }
                else if (restype == ResourceType.UTP)
                {
                    placeables[resname] = data;
                }
                else if (restype == ResourceType.MDX)
                {
                    // All keys in all_resources are lowercase, so use lowercase for lookups
                    string resnameLower = resname.ToLower(); // resname is already lowercase from all_resources keys
                    var mdlKey = (resnameLower, ResourceType.MDL);
                    var wokKey = (resnameLower, ResourceType.WOK);
                    if (allResources.ContainsKey(mdlKey) && !allResources.ContainsKey(wokKey))
                    {
                        // Likely a skybox
                        skyboxes[resnameLower] = new Dictionary<string, byte[]>
                        {
                            { "mdl", allResources[mdlKey] },
                            { "mdx", data }
                        };
                    }
                    // Also store MDX in all_models if MDL exists
                    if (allResources.ContainsKey(mdlKey) && !allModels.ContainsKey(resnameLower))
                    {
                        allModels[resnameLower] = new Dictionary<string, byte[]>
                        {
                            { "mdl", allResources[mdlKey] },
                            { "mdx", data }
                        };
                    }
                }
            }

            logger.Info($"Identified {components.Count} components: {string.Join(", ", components.Keys.Take(10))}...");

            // Map component model names to friendly component IDs
            Dictionary<string, string> componentNameMapping = GetComponentNameMapping(kitId, components.Keys.ToList());

            // Create new components dict with mapped names
            Dictionary<string, Dictionary<string, byte[]>> mappedComponents = new Dictionary<string, Dictionary<string, byte[]>>();
            foreach (var kvp in components)
            {
                string modelName = kvp.Key;
                Dictionary<string, byte[]> componentData = kvp.Value;
                string modelLower = modelName.ToLower();
                string mappedId = componentNameMapping.ContainsKey(modelLower) ? componentNameMapping[modelLower] : modelLower;
                mappedComponents[mappedId] = componentData;
                if (mappedId != modelLower)
                {
                    logger.Debug($"Mapped component '{modelName}' -> '{mappedId}'");
                }
            }

            // Replace components with mapped versions
            components = mappedComponents;

            // Extract textures and lightmaps from MDL files using iterate_textures/iterate_lightmaps
            // This is the same approach used in main.py _extract_mdl_textures
            // Use Module class to get all models (including from chitin) that reference textures/lightmaps
            HashSet<string> allTextureNames = new HashSet<string>();
            HashSet<string> allLightmapNames = new HashSet<string>();

            // Get all models from the module (including those loaded from chitin)
            foreach (ModuleResource modelResource in module.Models())
            {
                try
                {
                    object modelDataObj = modelResource.Resource();
                    if (modelDataObj is byte[] modelData)
                    {
                        allTextureNames.UnionWith(ModelTools.IterateTextures(modelData));
                        allLightmapNames.UnionWith(ModelTools.IterateLightmaps(modelData));
                    }
                }
                catch
                {
                    // Skip models that can't be loaded
                }
            }

            // Also extract all TPC/TGA files from RIM that might be textures/lightmaps
            // Some kits (like jedienclave) only have textures/lightmaps without components
            foreach (var kvp in allResources)
            {
                string resname = kvp.Key.resname;
                ResourceType restype = kvp.Key.restype;
                if (restype == ResourceType.TPC)
                {
                    // Determine if it's a texture or lightmap based on naming
                    string resnameLower = resname.ToLower();
                    if (resnameLower.Contains("_lm") || resnameLower.EndsWith("_lm"))
                    {
                        allLightmapNames.Add(resname);
                    }
                    else
                    {
                        allTextureNames.Add(resname);
                    }
                }
                else if (restype == ResourceType.TGA)
                {
                    // Determine if it's a texture or lightmap based on naming
                    string resnameLower = resname.ToLower();
                    if (resnameLower.Contains("_lm") || resnameLower.EndsWith("_lm"))
                    {
                        allLightmapNames.Add(resname);
                    }
                    else
                    {
                        allTextureNames.Add(resname);
                    }
                }
            }

            // Also check module resources for textures that might not be in RIM files
            // This catches textures that are in the module but not directly in the RIM
            foreach (var kvp in module.Resources)
            {
                ResourceIdentifier resIdent = kvp.Key;
                if (resIdent.ResType == ResourceType.TPC || resIdent.ResType == ResourceType.TGA)
                {
                    string resnameLower = resIdent.ResName.ToLower();
                    if (resnameLower.Contains("_lm") || resnameLower.EndsWith("_lm") || resnameLower.StartsWith("l_"))
                    {
                        allLightmapNames.Add(resIdent.ResName);
                    }
                    else
                    {
                        allTextureNames.Add(resIdent.ResName);
                    }
                }
            }

            // Batch all texture/lightmap lookups to avoid checking all files multiple times
            // This is a major performance optimization - instead of calling installation.locations()
            // 136+ times (once per texture), we call it once with all textures
            List<ResourceIdentifier> allTextureIdentifiers = new List<ResourceIdentifier>();
            foreach (string name in allTextureNames)
            {
                allTextureIdentifiers.Add(new ResourceIdentifier(name, ResourceType.TPC));
                allTextureIdentifiers.Add(new ResourceIdentifier(name, ResourceType.TGA));
            }
            foreach (string name in allLightmapNames)
            {
                allTextureIdentifiers.Add(new ResourceIdentifier(name, ResourceType.TPC));
                allTextureIdentifiers.Add(new ResourceIdentifier(name, ResourceType.TGA));
            }

            // Single batch lookup for all textures/lightmaps
            // Include MODULES in search order to respect resolution priority: Override > Modules > Textures > Chitin
            logger.Info($"Batch looking up {allTextureIdentifiers.Count} texture/lightmap resources...");
            Dictionary<ResourceIdentifier, List<LocationResult>> batchLocationResults = installation.Locations(
                allTextureIdentifiers,
                new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.TEXTURES_GUI, SearchLocation.TEXTURES_TPA, SearchLocation.CHITIN }
            );
            logger.Info($"Found locations for {batchLocationResults.Values.Count(r => r != null && r.Count > 0)} resources");

            // Pre-sort all location lists by priority once to avoid repeated sorting
            // This is a major performance optimization - sort once instead of sorting every time we access a location
            foreach (var kvp in batchLocationResults)
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    batchLocationResults[kvp.Key] = kvp.Value.OrderBy(loc => GetResourcePriority(loc, installation)).ToList();
                }
            }

            // Batch all TXI lookups upfront to avoid expensive individual calls
            // This is a major performance optimization - instead of calling installation.locations()
            // individually for each texture/lightmap (potentially 100+ times), we call it once
            List<ResourceIdentifier> allTxiIdentifiers = new List<ResourceIdentifier>();
            foreach (string name in allTextureNames)
            {
                allTxiIdentifiers.Add(new ResourceIdentifier(name, ResourceType.TXI));
            }
            foreach (string name in allLightmapNames)
            {
                allTxiIdentifiers.Add(new ResourceIdentifier(name, ResourceType.TXI));
            }

            logger.Info($"Batch looking up {allTxiIdentifiers.Count} TXI resources...");
            Dictionary<ResourceIdentifier, List<LocationResult>> batchTxiLocationResults = installation.Locations(
                allTxiIdentifiers,
                new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.TEXTURES_GUI, SearchLocation.TEXTURES_TPA, SearchLocation.CHITIN }
            );
            logger.Info($"Found locations for {batchTxiLocationResults.Values.Count(r => r != null && r.Count > 0)} TXI resources");

            // Pre-sort all TXI location lists by priority once to avoid repeated sorting
            foreach (var kvp in batchTxiLocationResults)
            {
                if (kvp.Value != null && kvp.Value.Count > 0)
                {
                    batchTxiLocationResults[kvp.Key] = kvp.Value.OrderBy(loc => GetResourcePriority(loc, installation)).ToList();
                }
            }

            // Batch extract all textures and lightmaps for better performance
            // Group by location file to minimize file I/O operations
            List<(string name, bool isLightmap, ResourceIdentifier resIdent, LocationResult location)> textureExtractionQueue = new List<(string, bool, ResourceIdentifier, LocationResult)>();
            List<(string name, bool isLightmap, ResourceIdentifier resIdent, LocationResult location)> lightmapExtractionQueue = new List<(string, bool, ResourceIdentifier, LocationResult)>();

            // Build extraction queues with location information
            foreach (string name in allTextureNames)
            {
                string nameLower = name.ToLower();
                if (textures.ContainsKey(nameLower))
                {
                    continue; // Already extracted
                }
                // Look up in batch results
                foreach (ResourceType rt in new[] { ResourceType.TPC, ResourceType.TGA })
                {
                    ResourceIdentifier resIdent = new ResourceIdentifier(name, rt);
                    if (batchLocationResults.ContainsKey(resIdent) && batchLocationResults[resIdent].Count > 0)
                    {
                        textureExtractionQueue.Add((name, false, resIdent, batchLocationResults[resIdent][0]));
                        break;
                    }
                }
            }

            foreach (string name in allLightmapNames)
            {
                string nameLower = name.ToLower();
                if (lightmaps.ContainsKey(nameLower))
                {
                    continue; // Already extracted
                }
                // Look up in batch results
                foreach (ResourceType rt in new[] { ResourceType.TPC, ResourceType.TGA })
                {
                    ResourceIdentifier resIdent = new ResourceIdentifier(name, rt);
                    if (batchLocationResults.ContainsKey(resIdent) && batchLocationResults[resIdent].Count > 0)
                    {
                        lightmapExtractionQueue.Add((name, true, resIdent, batchLocationResults[resIdent][0]));
                        break;
                    }
                }
            }

            // Process TPC files in batches grouped by file to minimize I/O
            // Group by filepath to read multiple resources from the same file in one pass
            Dictionary<string, List<(string name, bool isLightmap, LocationResult location)>> tpcFiles = new Dictionary<string, List<(string, bool, LocationResult)>>();
            Dictionary<string, List<(string name, bool isLightmap, LocationResult location)>> tgaFiles = new Dictionary<string, List<(string, bool, LocationResult)>>();

            foreach (var item in textureExtractionQueue.Concat(lightmapExtractionQueue))
            {
                if (item.resIdent.ResType == ResourceType.TPC)
                {
                    if (!tpcFiles.ContainsKey(item.location.FilePath))
                    {
                        tpcFiles[item.location.FilePath] = new List<(string, bool, LocationResult)>();
                    }
                    tpcFiles[item.location.FilePath].Add((item.name, item.isLightmap, item.location));
                }
                else
                {
                    if (!tgaFiles.ContainsKey(item.location.FilePath))
                    {
                        tgaFiles[item.location.FilePath] = new List<(string, bool, LocationResult)>();
                    }
                    tgaFiles[item.location.FilePath].Add((item.name, item.isLightmap, item.location));
                }
            }

            // Process TPC files in batches
            foreach (var kvp in tpcFiles)
            {
                string filepath = kvp.Key;
                List<(string name, bool isLightmap, LocationResult location)> items = kvp.Value;
                using (FileStream f = File.OpenRead(filepath))
                {
                    foreach (var item in items)
                    {
                        string name = item.name;
                        bool isLightmap = item.isLightmap;
                        LocationResult location = item.location;
                        string nameLower = name.ToLower();
                        Dictionary<string, byte[]> targetDict = isLightmap ? lightmaps : textures;
                        Dictionary<string, byte[]> targetTxisDict = isLightmap ? lightmapTxis : textureTxis;

                        if (targetDict.ContainsKey(nameLower))
                        {
                            continue; // Already extracted
                        }

                        try
                        {
                            f.Seek(location.Offset, SeekOrigin.Begin);
                            byte[] tpcData = new byte[location.Size];
                            f.Read(tpcData, 0, location.Size);
                            TPC tpc = TPCAuto.ReadTpc(tpcData);
                            // Convert TPC to TGA
                            byte[] tgaData = TPCAuto.BytesTpc(tpc, ResourceType.TGA);
                            targetDict[nameLower] = tgaData;
                            // Extract TXI if present
                            if (tpc.Txi != null && !string.IsNullOrWhiteSpace(tpc.Txi))
                            {
                                targetTxisDict[nameLower] = System.Text.Encoding.ASCII.GetBytes(tpc.Txi);
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            // Process TGA files in batches
            foreach (var kvp in tgaFiles)
            {
                string filepath = kvp.Key;
                List<(string name, bool isLightmap, LocationResult location)> items = kvp.Value;
                using (FileStream f = File.OpenRead(filepath))
                {
                    foreach (var item in items)
                    {
                        string name = item.name;
                        bool isLightmap = item.isLightmap;
                        LocationResult location = item.location;
                        string nameLower = name.ToLower();
                        Dictionary<string, byte[]> targetDict = isLightmap ? lightmaps : textures;

                        if (targetDict.ContainsKey(nameLower))
                        {
                            continue; // Already extracted
                        }

                        try
                        {
                            f.Seek(location.Offset, SeekOrigin.Begin);
                            byte[] tgaData = new byte[location.Size];
                            f.Read(tgaData, 0, location.Size);
                            targetDict[nameLower] = tgaData;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            // Batch extract TXI files grouped by filepath to minimize I/O
            // Group TXI files by filepath for batch reading
            Dictionary<string, List<(string nameLower, bool isLightmap, LocationResult location)>> txiFiles = new Dictionary<string, List<(string, bool, LocationResult)>>();

            // Collect all TXI files that need to be extracted
            foreach (string origName in allTextureNames)
            {
                string nameLower = origName.ToLower();
                if (textures.ContainsKey(nameLower) && !textureTxis.ContainsKey(nameLower))
                {
                    ResourceIdentifier txiResIdent = new ResourceIdentifier(origName, ResourceType.TXI);
                    if (batchTxiLocationResults.ContainsKey(txiResIdent) && batchTxiLocationResults[txiResIdent].Count > 0)
                    {
                        LocationResult txiLocation = batchTxiLocationResults[txiResIdent][0];
                        if (!txiFiles.ContainsKey(txiLocation.FilePath))
                        {
                            txiFiles[txiLocation.FilePath] = new List<(string, bool, LocationResult)>();
                        }
                        txiFiles[txiLocation.FilePath].Add((nameLower, false, txiLocation));
                    }
                }
            }

            foreach (string origName in allLightmapNames)
            {
                string nameLower = origName.ToLower();
                if (lightmaps.ContainsKey(nameLower) && !lightmapTxis.ContainsKey(nameLower))
                {
                    ResourceIdentifier txiResIdent = new ResourceIdentifier(origName, ResourceType.TXI);
                    if (batchTxiLocationResults.ContainsKey(txiResIdent) && batchTxiLocationResults[txiResIdent].Count > 0)
                    {
                        LocationResult txiLocation = batchTxiLocationResults[txiResIdent][0];
                        if (!txiFiles.ContainsKey(txiLocation.FilePath))
                        {
                            txiFiles[txiLocation.FilePath] = new List<(string, bool, LocationResult)>();
                        }
                        txiFiles[txiLocation.FilePath].Add((nameLower, true, txiLocation));
                    }
                }
            }

            // Process TXI files in batches
            foreach (var kvp in txiFiles)
            {
                string filepath = kvp.Key;
                List<(string nameLower, bool isLightmap, LocationResult location)> items = kvp.Value;
                using (FileStream f = File.OpenRead(filepath))
                {
                    foreach (var item in items)
                    {
                        string nameLower = item.nameLower;
                        bool isLightmap = item.isLightmap;
                        LocationResult location = item.location;
                        Dictionary<string, byte[]> targetTxisDict = isLightmap ? lightmapTxis : textureTxis;
                        if (targetTxisDict.ContainsKey(nameLower))
                        {
                            continue; // Already extracted
                        }
                        try
                        {
                            f.Seek(location.Offset, SeekOrigin.Begin);
                            byte[] txiData = new byte[location.Size];
                            f.Read(txiData, 0, location.Size);
                            targetTxisDict[nameLower] = txiData;
                        }
                        catch
                        {
                            // TXI not found or couldn't be read
                        }
                    }
                }
            }

            // Create empty TXI placeholders for textures/lightmaps that don't have TXI files
            // This matches the expected kit structure where textures have corresponding TXI files
            foreach (string textureNameLower in textures.Keys)
            {
                if (!textureTxis.ContainsKey(textureNameLower))
                {
                    textureTxis[textureNameLower] = new byte[0];
                }
            }

            foreach (string lightmapNameLower in lightmaps.Keys)
            {
                if (!lightmapTxis.ContainsKey(lightmapNameLower))
                {
                    lightmapTxis[lightmapNameLower] = new byte[0];
                }
            }

            // Create kit directory structure
            string kitDir = Path.Combine(outputPath, kitId);
            Directory.CreateDirectory(kitDir);

            string texturesDir = Path.Combine(kitDir, "textures");
            Directory.CreateDirectory(texturesDir);
            string lightmapsDir = Path.Combine(kitDir, "lightmaps");
            Directory.CreateDirectory(lightmapsDir);
            string skyboxesDir = Path.Combine(kitDir, "skyboxes");
            Directory.CreateDirectory(skyboxesDir);

            // Write component files
            List<Dictionary<string, object>> componentList = new List<Dictionary<string, object>>();
            foreach (var kvp in components)
            {
                string componentId = kvp.Key;
                Dictionary<string, byte[]> componentData = kvp.Value;
                // Write component files directly in kit_dir (not in subdirectory)
                File.WriteAllBytes(Path.Combine(kitDir, $"{componentId}.mdl"), componentData["mdl"]);
                if (componentData["mdx"] != null && componentData["mdx"].Length > 0)
                {
                    File.WriteAllBytes(Path.Combine(kitDir, $"{componentId}.mdx"), componentData["mdx"]);
                }

                // CRITICAL: Re-center BWM around (0, 0) before saving!
                // Game WOKs are in world coordinates, but Indoor Map Builder expects
                // centered BWMs so that the preview image aligns with the walkmesh hitbox.
                // Without this, images render in one place but hitboxes are elsewhere.
                BWM bwm = BWMAuto.ReadBwm(componentData["wok"]);
                bwm = RecenterBwm(bwm);

                // Write the re-centered WOK file
                File.WriteAllBytes(Path.Combine(kitDir, $"{componentId}.wok"), BWMAuto.BytesBwm(bwm));

                // Generate minimap PNG from re-centered BWM
                Bitmap minimapImage = GenerateComponentMinimap(bwm);
                string minimapPath = Path.Combine(kitDir, $"{componentId}.png");
                minimapImage.Save(minimapPath, ImageFormat.Png);
                minimapImage.Dispose();

                // Extract doorhooks from re-centered BWM edges with transitions
                List<Dictionary<string, object>> doorhooks = ExtractDoorhooksFromBwm(bwm, doors.Count);

                // Create component entry with extracted doorhooks
                componentList.Add(new Dictionary<string, object>
                {
                    { "name", componentId.Replace("_", " ").Replace("-", " ").Split(' ').Select(s => s.Length > 0 ? char.ToUpper(s[0]) + (s.Length > 1 ? s.Substring(1) : "") : "").Aggregate((a, b) => a + " " + b).Trim() },
                    { "id", componentId },
                    { "native", 1 },
                    { "doorhooks", doorhooks }
                });
            }

            // Write texture files
            foreach (var kvp in textures)
            {
                string textureName = kvp.Key;
                byte[] textureData = kvp.Value;
                File.WriteAllBytes(Path.Combine(texturesDir, $"{textureName}.tga"), textureData);
                // Always write TXI file (even if empty) to match expected kit structure
                if (textureTxis.ContainsKey(textureName))
                {
                    File.WriteAllBytes(Path.Combine(texturesDir, $"{textureName}.txi"), textureTxis[textureName]);
                }
                else
                {
                    // Create empty TXI placeholder if not found
                    File.WriteAllBytes(Path.Combine(texturesDir, $"{textureName}.txi"), new byte[0]);
                }
            }

            // Write lightmap files
            foreach (var kvp in lightmaps)
            {
                string lightmapName = kvp.Key;
                byte[] lightmapData = kvp.Value;
                File.WriteAllBytes(Path.Combine(lightmapsDir, $"{lightmapName}.tga"), lightmapData);
                // Always write TXI file (even if empty) to match expected kit structure
                if (lightmapTxis.ContainsKey(lightmapName))
                {
                    File.WriteAllBytes(Path.Combine(lightmapsDir, $"{lightmapName}.txi"), lightmapTxis[lightmapName]);
                }
                else
                {
                    // Create empty TXI placeholder if not found
                    File.WriteAllBytes(Path.Combine(lightmapsDir, $"{lightmapName}.txi"), new byte[0]);
                }
            }

            // Extract door walkmeshes (DWK files)
            // Reference: vendor/reone/src/libs/game/object/door.cpp:80-94
            // Doors have 3 walkmesh states: closed (0), open1 (1), open2 (2)
            // Format: <modelname>0.dwk, <modelname>1.dwk, <modelname>2.dwk
            // Batch DWK lookups to avoid repeated installation.locations() calls (performance optimization)
            TwoDA genericdoors2DAForDwk = Door.LoadGenericDoors2DA(installation, logger);
            List<string> doorModelNames = new List<string>();
            Dictionary<string, string> doorModelMap = new Dictionary<string, string>(); // model_name -> door_name

            // Get all door model names first
            foreach (var kvp in doors)
            {
                string doorName = kvp.Key;
                byte[] doorData = kvp.Value;
                try
                {
                    UTD utd = ResourceAutoHelpers.ReadUtd(doorData);
                    if (genericdoors2DAForDwk != null)
                    {
                        string doorModelName = Door.GetModel(utd, installation, genericdoors: genericdoors2DAForDwk);
                        if (!string.IsNullOrEmpty(doorModelName))
                        {
                            doorModelNames.Add(doorModelName);
                            doorModelMap[doorModelName] = doorName;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Batch lookup all DWK files (3 states per door: 0, 1, 2)
            List<ResourceIdentifier> dwkIdentifiers = new List<ResourceIdentifier>();
            Dictionary<ResourceIdentifier, (string modelName, string doorName)> dwkModelMap = new Dictionary<ResourceIdentifier, (string, string)>();
            foreach (string modelName in doorModelNames)
            {
                foreach (string suffix in new[] { "0", "1", "2" })
                {
                    string dwkResname = $"{modelName}{suffix}";
                    ResourceIdentifier dwkIdent = new ResourceIdentifier(dwkResname, ResourceType.DWK);
                    dwkIdentifiers.Add(dwkIdent);
                    dwkModelMap[dwkIdent] = (modelName, doorModelMap[modelName]);
                }
            }

            Dictionary<ResourceIdentifier, List<LocationResult>> dwkLocations = new Dictionary<ResourceIdentifier, List<LocationResult>>();
            if (dwkIdentifiers.Count > 0)
            {
                dwkLocations = installation.Locations(
                    dwkIdentifiers,
                    new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.CHITIN }
                );
            }

            // Extract DWK files using batched results
            foreach (var kvp in doors)
            {
                string doorName = kvp.Key;
                byte[] doorData = kvp.Value;
                doorWalkmeshes[doorName] = new Dictionary<string, byte[]>();
                try
                {
                    UTD utd = ResourceAutoHelpers.ReadUtd(doorData);
                    if (genericdoors2DAForDwk == null)
                    {
                        continue;
                    }
                    string doorModelName = Door.GetModel(utd, installation, genericdoors: genericdoors2DAForDwk);
                    if (string.IsNullOrEmpty(doorModelName))
                    {
                        continue;
                    }

                    // Try module first (fastest), then fall back to batched Andastra.Parsing.Installation.Installation locations
                    foreach (string suffix in new[] { "0", "1", "2" })
                    {
                        string dwkKey = $"dwk{suffix}";
                        string dwkResname = $"{doorModelName}{suffix}";
                        bool dwkFound = false;

                        // Try module first
                        if (module != null)
                        {
                            ModuleResource dwkResource = module.Resource(dwkResname, ResourceType.DWK);
                            if (dwkResource != null)
                            {
                                object dwkDataObj = dwkResource.Resource();
                                if (dwkDataObj is byte[] dwkData)
                                {
                                    doorWalkmeshes[doorName][dwkKey] = dwkData;
                                    logger.Debug($"Found DWK '{dwkResname}' (state: {dwkKey}) from module");
                                    dwkFound = true;
                                }
                            }
                        }

                        // Try batched Andastra.Parsing.Installation.Installation locations if not found in module
                        if (!dwkFound)
                        {
                            ResourceIdentifier dwkIdent = new ResourceIdentifier(dwkResname, ResourceType.DWK);
                            if (dwkLocations.ContainsKey(dwkIdent) && dwkLocations[dwkIdent].Count > 0)
                            {
                                LocationResult dwkLoc = dwkLocations[dwkIdent][0];
                                try
                                {
                                    using (FileStream f = File.OpenRead(dwkLoc.FilePath))
                                    {
                                        f.Seek(dwkLoc.Offset, SeekOrigin.Begin);
                                        byte[] dwkData = new byte[dwkLoc.Size];
                                        f.Read(dwkData, 0, dwkLoc.Size);
                                        doorWalkmeshes[doorName][dwkKey] = dwkData;
                                        logger.Debug($"Found DWK '{dwkResname}' (state: {dwkKey}) from installation");
                                    }
                                }
                                catch
                                {
                                    // DWK not found or couldn't be read
                                }
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Extract placeable walkmeshes (PWK files)
            // Reference: vendor/reone/src/libs/game/object/placeable.cpp:73
            // Format: <modelname>.pwk
            // Load placeables.2da once for all placeables (performance optimization)
            TwoDA placeables2DA = Placeable.LoadPlaceables2DA(installation, logger);

            // First, try to get all placeable model names and batch PWK lookups
            // This avoids calling installation.locations() individually for each placeable
            List<string> placeableModelNames = new List<string>();
            Dictionary<string, string> placeableModelMap = new Dictionary<string, string>(); // model_name -> placeable_name

            foreach (var kvp in placeables)
            {
                string placeableName = kvp.Key;
                byte[] placeableData = kvp.Value;
                try
                {
                    UTP utp = ResourceAutoHelpers.ReadUtp(placeableData);
                    if (placeables2DA != null)
                    {
                        string placeableModelName = Placeable.GetModel(utp, installation, placeables: placeables2DA);
                        if (!string.IsNullOrEmpty(placeableModelName))
                        {
                            placeableModelNames.Add(placeableModelName);
                            placeableModelMap[placeableModelName] = placeableName;
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Batch lookup all PWK files at once
            List<ResourceIdentifier> pwkIdentifiers = placeableModelNames.Select(modelName => new ResourceIdentifier(modelName, ResourceType.PWK)).ToList();
            Dictionary<ResourceIdentifier, List<LocationResult>> pwkLocations = new Dictionary<ResourceIdentifier, List<LocationResult>>();
            if (pwkIdentifiers.Count > 0)
            {
                pwkLocations = installation.Locations(
                    pwkIdentifiers,
                    new[] { SearchLocation.OVERRIDE, SearchLocation.MODULES, SearchLocation.CHITIN }
                );
            }

            // Extract PWK files using batched results
            foreach (var kvp in placeables)
            {
                string placeableName = kvp.Key;
                byte[] placeableData = kvp.Value;
                try
                {
                    UTP utp = ResourceAutoHelpers.ReadUtp(placeableData);
                    if (placeables2DA == null)
                    {
                        continue;
                    }
                    string placeableModelName = Placeable.GetModel(utp, installation, placeables: placeables2DA);
                    if (string.IsNullOrEmpty(placeableModelName))
                    {
                        continue;
                    }

                    // Try module first (fastest)
                    if (module != null)
                    {
                        ModuleResource pwkResource = module.Resource(placeableModelName, ResourceType.PWK);
                        if (pwkResource != null)
                        {
                            object pwkDataObj = pwkResource.Resource();
                            if (pwkDataObj is byte[] pwkData)
                            {
                                placeableWalkmeshes[placeableModelName] = pwkData;
                                logger.Debug($"Found PWK '{placeableModelName}' for placeable '{placeableName}' from module");
                                continue;
                            }
                        }
                    }

                    // Try batched Andastra.Parsing.Installation.Installation locations
                    ResourceIdentifier pwkIdent = new ResourceIdentifier(placeableModelName, ResourceType.PWK);
                    if (pwkLocations.ContainsKey(pwkIdent) && pwkLocations[pwkIdent].Count > 0)
                    {
                        LocationResult pwkLoc = pwkLocations[pwkIdent][0];
                        try
                        {
                            using (FileStream f = File.OpenRead(pwkLoc.FilePath))
                            {
                                f.Seek(pwkLoc.Offset, SeekOrigin.Begin);
                                byte[] pwkData = new byte[pwkLoc.Size];
                                f.Read(pwkData, 0, pwkLoc.Size);
                                placeableWalkmeshes[placeableModelName] = pwkData;
                                logger.Debug($"Found PWK '{placeableModelName}' for placeable '{placeableName}' from installation");
                            }
                        }
                        catch
                        {
                            // PWK not found or couldn't be read
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Write door files
            // Use simple door identifiers (door0, door1, etc.) for file names and JSON
            // This matches the expected kit format from the examples
            // Ensure kit_dir exists before writing door files (in case no components/textures were written)
            Directory.CreateDirectory(kitDir);

            // Load genericdoors.2da once for all doors (major performance optimization)
            // This avoids reloading the same file for each door
            TwoDA genericdoors2DA = Door.LoadGenericDoors2DA(installation, logger);

            List<Dictionary<string, object>> doorList = new List<Dictionary<string, object>>();
            int doorIdx = 0;
            foreach (var kvp in doors)
            {
                string doorName = kvp.Key;
                byte[] doorData = kvp.Value;
                // Use simple identifier: door0, door1, door2, etc.
                string doorId = $"door{doorIdx}";

                // Write UTD files using the simple identifier
                File.WriteAllBytes(Path.Combine(kitDir, $"{doorId}_k1.utd"), doorData);
                // For K1, we use the same UTD for K2 (in real kits, these might differ)
                File.WriteAllBytes(Path.Combine(kitDir, $"{doorId}_k2.utd"), doorData);

                // Write door walkmeshes (DWK files) if found
                if (doorWalkmeshes.ContainsKey(doorName))
                {
                    foreach (var dwkKvp in doorWalkmeshes[doorName])
                    {
                        string dwkKey = dwkKvp.Key;
                        byte[] dwkData = dwkKvp.Value;
                        // Extract door model name to determine DWK filename
                        try
                        {
                            UTD utd = ResourceAutoHelpers.ReadUtd(doorData);
                            if (genericdoors2DA != null)
                            {
                                string doorModelName = Door.GetModel(utd, installation, genericdoors: genericdoors2DA);
                                if (!string.IsNullOrEmpty(doorModelName))
                                {
                                    // Map dwk_key (dwk0, dwk1, dwk2) to filename suffix (0, 1, 2)
                                    string dwkSuffix = dwkKey.Replace("dwk", "");
                                    string dwkFilename = $"{doorModelName}{dwkSuffix}.dwk";
                                    File.WriteAllBytes(Path.Combine(kitDir, dwkFilename), dwkData);
                                    logger.Debug($"Wrote door walkmesh '{dwkFilename}' for door '{doorId}' (resname: '{doorName}')");
                                }
                            }
                        }
                        catch
                        {
                            // Skip if we can't determine model name
                        }
                    }
                }

                // Use fast defaults for door dimensions to avoid expensive extraction
                // Dimensions are metadata and not critical for kit functionality
                // This avoids 2+ seconds per door for texture/model extraction
                float doorWidth = 2.0f;
                float doorHeight = 3.0f;

                doorList.Add(new Dictionary<string, object>
                {
                    { "utd_k1", $"{doorId}_k1" },
                    { "utd_k2", $"{doorId}_k2" },
                    { "width", doorWidth },
                    { "height", doorHeight }
                });

                doorIdx++;
            }

            // Write placeable walkmeshes (PWK files)
            foreach (var kvp in placeableWalkmeshes)
            {
                string placeableModelName = kvp.Key;
                byte[] pwkData = kvp.Value;
                File.WriteAllBytes(Path.Combine(kitDir, $"{placeableModelName}.pwk"), pwkData);
                logger.Debug($"Wrote placeable walkmesh '{placeableModelName}.pwk'");
            }

            // Write all models (MDL/MDX) that aren't components or skyboxes
            // This ensures we extract all models referenced by the module, not just room components
            // Reference: Tools/HolocronToolset/src/toolset/gui/windows/main.py extractAllModuleModels
            string modelsDir = Path.Combine(kitDir, "models");
            Directory.CreateDirectory(modelsDir);
            foreach (var kvp in allModels)
            {
                string modelName = kvp.Key;
                Dictionary<string, byte[]> modelData = kvp.Value;
                // Skip if already written as component or skybox
                if (components.ContainsKey(modelName) || skyboxes.ContainsKey(modelName))
                {
                    continue;
                }
                // Write MDL and MDX files
                File.WriteAllBytes(Path.Combine(modelsDir, $"{modelName}.mdl"), modelData["mdl"]);
                if (modelData.ContainsKey("mdx") && modelData["mdx"] != null && modelData["mdx"].Length > 0)
                {
                    File.WriteAllBytes(Path.Combine(modelsDir, $"{modelName}.mdx"), modelData["mdx"]);
                }
                logger.Debug($"Wrote model '{modelName}' (MDL/MDX)");
            }

            // Write skybox files
            foreach (var kvp in skyboxes)
            {
                string skyboxName = kvp.Key;
                Dictionary<string, byte[]> skyboxData = kvp.Value;
                File.WriteAllBytes(Path.Combine(skyboxesDir, $"{skyboxName}.mdl"), skyboxData["mdl"]);
                File.WriteAllBytes(Path.Combine(skyboxesDir, $"{skyboxName}.mdx"), skyboxData["mdx"]);
            }

            // Generate JSON file
            // Format kit name from kit_id (e.g., "enclavesurface" -> "Enclave Surface")
            string kitName = kitId.Replace("_", " ").Replace("-", " ").Split(' ').Select(s => s.Length > 0 ? char.ToUpper(s[0]) + (s.Length > 1 ? s.Substring(1) : "") : "").Aggregate((a, b) => a + " " + b).Trim();
            Dictionary<string, object> kitJson = new Dictionary<string, object>
            {
                { "name", kitName },
                { "id", kitId },
                { "ht", "2.0.2" },
                { "version", 1 },
                { "components", componentList },
                { "doors", doorList }
            };

            string jsonPath = Path.Combine(outputPath, $"{kitId}.json");
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(kitJson, Formatting.Indented));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:1348-1464
        // Original: def _generate_component_minimap(bwm: BWM) -> QImage | PIL.Image
        private static Bitmap GenerateComponentMinimap(BWM bwm)
        {
            // Calculate bounding box
            List<Vector3> vertices = bwm.Vertices();
            if (vertices.Count == 0)
            {
                // Empty walkmesh - return small blank image
                Bitmap image = new Bitmap(256, 256);
                using (Graphics g = Graphics.FromImage(image))
                {
                    g.Clear(System.Drawing.Color.Black);
                }
                return image;
            }

            float minX = vertices.Min(v => v.X);
            float maxX = vertices.Max(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxY = vertices.Max(v => v.Y);
            float minZ = vertices.Min(v => v.Z);
            float maxZ = vertices.Max(v => v.Z);

            Vector3 bbmin = new Vector3(minX, minY, minZ);
            Vector3 bbmax = new Vector3(maxX, maxY, maxZ);

            // Add padding
            float padding = 5.0f;
            bbmin.X -= padding;
            bbmin.Y -= padding;
            bbmax.X += padding;
            bbmax.Y += padding;

            // Calculate image dimensions (scale: 10 pixels per unit)
            int width = (int)((bbmax.X - bbmin.X) * 10);
            int height = (int)((bbmax.Y - bbmin.Y) * 10);

            // Ensure minimum size
            width = Math.Max(width, 256);
            height = Math.Max(height, 256);

            // Transform to image coordinates (flip Y, scale, translate)
            Func<Vector2, (float x, float y)> toImageCoords = (v) =>
            {
                float x = (v.X - bbmin.X) * 10;
                float y = height - (v.Y - bbmin.Y) * 10; // Flip Y
                return (x, y);
            };

            // Create image
            Bitmap image2 = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(image2))
            {
                g.Clear(System.Drawing.Color.Black);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw walkable faces in white, non-walkable in gray
                // Matching Python line 1412: is_walkable = face.material.value in (1, 3, 4, 5, 6, 9, 10, 11, 12, 13, 14, 16, 18, 20, 21, 22)
                foreach (BWMFace face in bwm.Faces)
                {
                    // Determine if face is walkable based on material (matching Python hardcoded list)
                    int materialValue = (int)face.Material;
                    bool isWalkable = materialValue == 1 || materialValue == 3 || materialValue == 4 || materialValue == 5 ||
                                     materialValue == 6 || materialValue == 9 || materialValue == 10 || materialValue == 11 ||
                                     materialValue == 12 || materialValue == 13 || materialValue == 14 || materialValue == 16 ||
                                     materialValue == 18 || materialValue == 20 || materialValue == 21 || materialValue == 22;
                    ParsingColor parsingColor = isWalkable ? ParsingColor.WHITE : new ParsingColor(0.5f, 0.5f, 0.5f);
                    System.Drawing.Color color = isWalkable ? System.Drawing.Color.White : System.Drawing.Color.Gray;

                    // Get face vertices
                    Vector2 v1 = new Vector2(face.V1.X, face.V1.Y);
                    Vector2 v2 = new Vector2(face.V2.X, face.V2.Y);
                    Vector2 v3 = new Vector2(face.V3.X, face.V3.Y);

                    (float x1, float y1) = toImageCoords(v1);
                    (float x2, float y2) = toImageCoords(v2);
                    (float x3, float y3) = toImageCoords(v3);

                    // Draw polygon (triangle)
                    using (SolidBrush brush = new SolidBrush(color))
                    using (Pen pen = new Pen(color))
                    {
                        PointF[] points = new PointF[]
                        {
                            new PointF(x1, y1),
                            new PointF(x2, y2),
                            new PointF(x3, y3)
                        };
                        g.FillPolygon(brush, points);
                        g.DrawPolygon(pen, points);
                    }
                }
            }

            return image2;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:1467-1536
        // Original: def _extract_doorhooks_from_bwm(bwm: BWM, num_doors: int) -> list[dict[str, float | int]]
        private static List<Dictionary<string, object>> ExtractDoorhooksFromBwm(BWM bwm, int numDoors)
        {
            List<Dictionary<string, object>> doorhooks = new List<Dictionary<string, object>>();

            // Get all perimeter edges (these are the edges with transitions)
            List<BWMEdge> edges = bwm.Edges();

            // Process edges with valid transitions
            foreach (BWMEdge edge in edges)
            {
                if (edge.Transition < 0) // Skip edges without transitions
                {
                    continue;
                }

                BWMFace face = edge.Face;
                // Get edge vertices based on local edge index (0, 1, or 2)
                // edge.index is the global edge index (face_index * 3 + local_edge_index)
                int faceIndex = edge.Index / 3;
                int localEdgeIndex = edge.Index % 3;

                // Get vertices for this edge
                Vector3 v1, v2;
                if (localEdgeIndex == 0)
                {
                    v1 = face.V1;
                    v2 = face.V2;
                }
                else if (localEdgeIndex == 1)
                {
                    v1 = face.V2;
                    v2 = face.V3;
                }
                else // localEdgeIndex == 2
                {
                    v1 = face.V3;
                    v2 = face.V1;
                }

                // Calculate midpoint of edge
                float midX = (v1.X + v2.X) / 2.0f;
                float midY = (v1.Y + v2.Y) / 2.0f;
                float midZ = (v1.Z + v2.Z) / 2.0f;

                // Calculate rotation (angle of edge in XY plane, in degrees)
                float dx = v2.X - v1.X;
                float dy = v2.Y - v1.Y;
                float rotation = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                // Normalize to 0-360
                rotation = rotation % 360;
                if (rotation < 0)
                {
                    rotation += 360;
                }

                // Map transition index to door index
                // Transition indices typically map directly to door indices, but clamp to valid range
                int doorIndex = numDoors > 0 ? Math.Min(edge.Transition, numDoors - 1) : 0;

                doorhooks.Add(new Dictionary<string, object>
                {
                    { "x", midX },
                    { "y", midY },
                    { "z", midZ },
                    { "rotation", rotation },
                    { "door", doorIndex },
                    { "edge", edge.Index } // Global edge index
                });
            }

            return doorhooks;
        }
    }
}
