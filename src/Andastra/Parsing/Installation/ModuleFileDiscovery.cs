using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Installation
{
    /// <summary>
    /// Handles discovery and resolution of module files from the modules directory.
    /// Matches the exact behavior of swkotor.exe/swkotor2.exe module file discovery.
    /// 
    /// Based on reverse engineering of swkotor.exe/swkotor2.exe module loading system.
    /// 
    /// Loading Modes (swkotor.exe: FUN_004094a0, swkotor2.exe: FUN_004096b0):
    /// - Simple Mode (flag at offset 0x54 == 0): Loads .rim file directly, returns immediately
    /// - Complex Mode (flag at offset 0x54 != 0): Checks for area files (_a.rim, _adx.rim), then .mod, then _s.rim/_dlg.erf
    /// 
    /// Complex Mode Priority Rules (exact order from Ghidra decompilation):
    /// 1. Check for {moduleRoot}_a.rim (area-specific RIM) - if found, loads it (REPLACES .rim)
    /// 2. Check for {moduleRoot}_adx.rim (extended area RIM) - if _a.rim not found, loads it (REPLACES .rim)
    /// 3. Check for {moduleRoot}.mod - if found, loads it (REPLACES all other files, skips _s.rim/_dlg.erf)
    /// 4. Check for {moduleRoot}_s.rim - only if .mod NOT found (ADDS to base)
    /// 5. Check for {moduleRoot}_dlg.erf (K2 only) - only if .mod NOT found (ADDS to base)
    /// 
    /// Note: .rim is NOT loaded in complex mode - only _a.rim or _adx.rim are loaded as replacements
    /// 
    /// Reverse engineering evidence:
    /// - swkotor.exe: FUN_004094a0 line 32: `if (*(int *)((int)param_1 + 0x54) == 0)` - simple mode check
    /// - swkotor.exe: FUN_004094a0 line 61: Checks for _a.rim (ARE type 0xbba)
    /// - swkotor.exe: FUN_004094a0 line 74: Checks for _adx.rim (ARE type 0xbba)
    /// - swkotor.exe: FUN_004094a0 line 95: Checks for .mod (MOD type 0x7db)
    /// - swkotor.exe: FUN_004094a0 line 107: Checks for _s.rim (ARE type 0xbba, only if .mod not found)
    /// - swkotor2.exe: FUN_004096b0 line 128: Hardcoded "_dlg" check for _dlg.erf
    /// </summary>
    public static class ModuleFileDiscovery
    {
        private static string TryGetFilePathByName(Dictionary<string, string> fileNameToPath, string fileName)
        {
            if (fileNameToPath == null || string.IsNullOrEmpty(fileName))
            {
                return null;
            }
            fileNameToPath.TryGetValue(fileName, out string path);
            return path;
        }

        /// <summary>
        /// Discovers all module files for a given module root, respecting priority rules.
        /// Implements exact behavior from swkotor.exe: FUN_004094a0 and swkotor2.exe: FUN_004096b0
        /// </summary>
        /// <param name="modulesPath">Path to the modules directory</param>
        /// <param name="moduleRoot">Module root name (e.g., "001EBO")</param>
        /// <param name="game">Game type (K1 or K2)</param>
        /// <param name="useComplexMode">If true, uses complex mode (checks for _a.rim, _adx.rim). If false, uses simple mode (just .rim). Default: auto-detect based on file existence</param>
        /// <returns>ModuleFileGroup containing discovered files, or null if no files found</returns>
        public static ModuleFileGroup DiscoverModuleFiles(string modulesPath, string moduleRoot, BioWareGame game, bool? useComplexMode = null)
        {
            if (string.IsNullOrEmpty(modulesPath) || !Directory.Exists(modulesPath))
            {
                return null;
            }

            if (string.IsNullOrEmpty(moduleRoot))
            {
                return null;
            }

            // Build a case-insensitive file name index so discovery behaves like the Windows engine
            // even on case-sensitive filesystems.
            var fileNameToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string fullPath in Directory.EnumerateFiles(modulesPath))
            {
                string name = Path.GetFileName(fullPath);
                if (!string.IsNullOrEmpty(name) && !fileNameToPath.ContainsKey(name))
                {
                    fileNameToPath[name] = fullPath;
                }
            }

            // Check for area files to determine if we should use complex mode
            // swkotor.exe: FUN_004094a0 line 61: Checks for ARE type 0xbba in _a.rim
            string areaRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + "_a.rim");
            string areaExtendedRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + "_adx.rim");

            // Auto-detect complex mode: if _a.rim or _adx.rim exists, use complex mode
            // Otherwise, check if .rim exists for simple mode
            bool complexMode = useComplexMode ?? (areaRimPath != null || areaExtendedRimPath != null);

            // Simple Mode (swkotor.exe: FUN_004094a0 line 32-42)
            // Just load .rim file directly, return immediately
            if (!complexMode)
            {
                string mainRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + ".rim");
                if (mainRimPath == null)
                {
                    return null;
                }

                return new ModuleFileGroup
                {
                    ModuleRoot = moduleRoot,
                    ModFile = null,
                    MainRimFile = mainRimPath,
                    AreaRimFile = null,
                    AreaExtendedRimFile = null,
                    DataRimFile = null,
                    DlgErfFile = null,
                    UsesModOverride = false,
                    UseComplexMode = false
                };
            }

            // Complex Mode (swkotor.exe: FUN_004094a0 line 49-216)
            // Exact sequence from Ghidra decompilation:

            // Step 1: Check for _a.rim (line 61)
            // If found, load it (line 159) - REPLACES .rim
            // If not found, continue to _adx.rim

            // Step 2: Check for _adx.rim (line 74)
            // If found, load it (line 85) - REPLACES .rim (only if _a.rim not found)

            // Step 3: Check for .mod (line 95)
            // If found, load it (line 136) - REPLACES all other files, SKIPS _s.rim
            string modPath = TryGetFilePathByName(fileNameToPath, moduleRoot + ".mod");
            if (modPath != null)
            {
                // .mod file exists - use only this file, ignore all rim-like files
                // swkotor.exe: FUN_004094a0 line 136: Loads .mod, skips _s.rim check
                return new ModuleFileGroup
                {
                    ModuleRoot = moduleRoot,
                    ModFile = modPath,
                    MainRimFile = null,
                    AreaRimFile = areaRimPath,  // Still discovered but not loaded when .mod exists
                    AreaExtendedRimFile = areaExtendedRimPath,  // Still discovered but not loaded when .mod exists
                    DataRimFile = null,
                    DlgErfFile = null,
                    UsesModOverride = true,
                    UseComplexMode = true
                };
            }

            // Step 4: Check for _s.rim (line 107) - only if .mod NOT found
            // If found, load it (line 118) - ADDS to base
            string dataRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + "_s.rim");

            // Step 5: Check for _dlg.erf (K2 only, line 128) - only if .mod NOT found
            // If found, load it (line 147) - ADDS to base
            string dlgErfPath = null;
            if (game.IsK2())
            {
                dlgErfPath = TryGetFilePathByName(fileNameToPath, moduleRoot + "_dlg.erf");
            }

            // In complex mode, .rim is NOT loaded - only _a.rim or _adx.rim are loaded as replacements
            // swkotor.exe: FUN_004094a0 line 32: Simple mode loads .rim, complex mode does not
            // If neither _a.rim nor _adx.rim exists, we still need at least one file
            if (areaRimPath == null && areaExtendedRimPath == null && dataRimPath == null && dlgErfPath == null)
            {
                // Fallback: check for .rim if no area files exist (shouldn't happen in complex mode, but handle gracefully)
                string mainRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + ".rim");
                if (mainRimPath == null)
                {
                    return null;
                }

                return new ModuleFileGroup
                {
                    ModuleRoot = moduleRoot,
                    ModFile = null,
                    MainRimFile = mainRimPath,
                    AreaRimFile = null,
                    AreaExtendedRimFile = null,
                    DataRimFile = null,
                    DlgErfFile = null,
                    UsesModOverride = false,
                    UseComplexMode = false  // Fallback to simple mode
                };
            }

            return new ModuleFileGroup
            {
                ModuleRoot = moduleRoot,
                ModFile = null,
                MainRimFile = null,  // NOT loaded in complex mode
                AreaRimFile = areaRimPath,  // Loaded if exists (REPLACES .rim)
                AreaExtendedRimFile = areaExtendedRimPath,  // Loaded if _a.rim not found (REPLACES .rim)
                DataRimFile = dataRimPath,  // Loaded if .mod not found (ADDS to base)
                DlgErfFile = dlgErfPath,  // Loaded if .mod not found (K2 only, ADDS to base)
                UsesModOverride = false,
                UseComplexMode = true
            };
        }

        /// <summary>
        /// Discovers all module roots available in the modules directory.
        /// </summary>
        /// <param name="modulesPath">Path to the modules directory</param>
        /// <returns>Set of module roots (case-insensitive)</returns>
        public static HashSet<string> DiscoverAllModuleRoots(string modulesPath)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(modulesPath) || !Directory.Exists(modulesPath))
            {
                return roots;
            }

            foreach (string file in Directory.EnumerateFiles(modulesPath))
            {
                string fileName = Path.GetFileName(file);
                string root = Installation.GetModuleRoot(fileName);

                // Only include if it's a recognized module file type
                if (IsModuleFile(fileName) && !string.IsNullOrEmpty(root))
                {
                    roots.Add(root);
                }
            }

            return roots;
        }

        /// <summary>
        /// Checks if a filename is a recognized module file type.
        /// </summary>
        /// <param name="fileName">Filename to check</param>
        /// <returns>True if the file is a recognized module file type</returns>
        public static bool IsModuleFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            string lowerName = fileName.ToLowerInvariant();

            // Module containers (from swkotor.exe: FUN_004094a0 and swkotor2.exe: FUN_004096b0):
            // - <root>.mod (override archive)
            // - <root>.rim (main archive, simple mode only)
            // - <root>_a.rim (area-specific RIM, complex mode)
            // - <root>_adx.rim (extended area RIM, complex mode)
            // - <root>_s.rim (data archive)
            // - (TSL) <root>_dlg.erf (dialog archive, K2 only)
            if (lowerName.EndsWith(".mod"))
                return true;
            if (lowerName.EndsWith(".rim"))
                return true;
            if (lowerName.EndsWith("_dlg.erf"))
                return true;

            return false;
        }

        /// <summary>
        /// Gets all module file paths for a module root, in priority order (registration order).
        /// Matches exact loading order from swkotor.exe: FUN_004094a0 and swkotor2.exe: FUN_004096b0
        /// </summary>
        /// <param name="modulesPath">Path to the modules directory</param>
        /// <param name="moduleRoot">Module root name</param>
        /// <param name="game">Game type</param>
        /// <returns>List of file paths in registration order (first registered = lowest priority, last registered = highest priority)</returns>
        public static List<string> GetModuleFilePaths(string modulesPath, string moduleRoot, BioWareGame game)
        {
            ModuleFileGroup group = DiscoverModuleFiles(modulesPath, moduleRoot, game);
            if (group == null)
            {
                return new List<string>();
            }

            var paths = new List<string>();

            if (group.UsesModOverride)
            {
                // .mod file overrides all - only this file is loaded
                // swkotor.exe: FUN_004094a0 line 136: Loads .mod, skips _s.rim
                if (group.ModFile != null)
                {
                    paths.Add(group.ModFile);
                }
            }
            else if (group.UseComplexMode)
            {
                // Complex mode: Load in exact order from Ghidra decompilation
                // Order: _a.rim -> _adx.rim -> _s.rim -> _dlg.erf
                // Note: .rim is NOT loaded in complex mode

                // Step 1: _a.rim (swkotor.exe: FUN_004094a0 line 159)
                if (group.AreaRimFile != null)
                {
                    paths.Add(group.AreaRimFile);
                }

                // Step 2: _adx.rim (swkotor.exe: FUN_004094a0 line 85) - only if _a.rim not found
                if (group.AreaRimFile == null && group.AreaExtendedRimFile != null)
                {
                    paths.Add(group.AreaExtendedRimFile);
                }

                // Step 3: _s.rim (swkotor.exe: FUN_004094a0 line 118) - only if .mod not found
                if (group.DataRimFile != null)
                {
                    paths.Add(group.DataRimFile);
                }

                // Step 4: _dlg.erf (swkotor2.exe: FUN_004096b0 line 147) - only if .mod not found (K2 only)
                if (group.DlgErfFile != null)
                {
                    paths.Add(group.DlgErfFile);
                }
            }
            else
            {
                // Simple mode: Just .rim file
                // swkotor.exe: FUN_004094a0 line 42: Loads .rim directly
                if (group.MainRimFile != null)
                {
                    paths.Add(group.MainRimFile);
                }
            }

            return paths;
        }
    }

    /// <summary>
    /// Represents a group of module files for a single module root.
    /// Matches exact behavior from swkotor.exe: FUN_004094a0 and swkotor2.exe: FUN_004096b0
    /// </summary>
    public class ModuleFileGroup
    {
        public string ModuleRoot { get; set; }
        public string ModFile { get; set; }
        public string MainRimFile { get; set; }  // .rim file (only loaded in simple mode)
        public string AreaRimFile { get; set; }  // _a.rim file (replaces .rim in complex mode)
        public string AreaExtendedRimFile { get; set; }  // _adx.rim file (replaces .rim if _a.rim not found)
        public string DataRimFile { get; set; }  // _s.rim file (adds to base)
        public string DlgErfFile { get; set; }  // _dlg.erf file (K2 only, adds to base)
        public bool UsesModOverride { get; set; }
        public bool UseComplexMode { get; set; }  // True if _a.rim or _adx.rim exists (complex mode)

        /// <summary>
        /// Gets all file paths in this group in registration order (matching Ghidra decompilation).
        /// </summary>
        public List<string> GetAllFiles()
        {
            var files = new List<string>();

            if (UsesModOverride)
            {
                // .mod overrides all
                if (ModFile != null) files.Add(ModFile);
            }
            else if (UseComplexMode)
            {
                // Complex mode order: _a.rim -> _adx.rim -> _s.rim -> _dlg.erf
                if (AreaRimFile != null) files.Add(AreaRimFile);
                if (AreaExtendedRimFile != null) files.Add(AreaExtendedRimFile);
                if (DataRimFile != null) files.Add(DataRimFile);
                if (DlgErfFile != null) files.Add(DlgErfFile);
            }
            else
            {
                // Simple mode: just .rim
                if (MainRimFile != null) files.Add(MainRimFile);
            }

            return files;
        }

        /// <summary>
        /// Checks if this group has any files.
        /// </summary>
        public bool HasFiles()
        {
            return ModFile != null || MainRimFile != null || AreaRimFile != null ||
                   AreaExtendedRimFile != null || DataRimFile != null || DlgErfFile != null;
        }
    }
}

