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
    /// Priority Rules:
    /// 1. If {moduleRoot}.mod exists, use ONLY the .mod file (overrides all rim-like files)
    /// 2. If no .mod exists, combine all rim-like files:
    ///    - {moduleRoot}.rim (main archive, required)
    ///    - {moduleRoot}_s.rim (data archive, optional)
    ///    - {moduleRoot}_dlg.erf (dialog archive, K2 only, optional)
    /// 
    /// Reverse engineering notes (clean-room behavioral):
    /// - The engine enumerates the MODULES directory using a wildcard file glob (effectively "*.*")
    ///   and then filters by naming rules in code. This is why full ".mod"/".rim"/".erf" literals
    ///   are not consistently present as standalone strings in the binaries.
    ///   - swkotor.exe: `FUN_005e8cf0` (directory enumerator; FindFirstFileA over "*.*" when extension string is empty)
    ///   - swkotor2.exe: `FUN_006345d0` (same behavior)
    /// - `_s.rim` is explicitly treated as a special suffix in both games:
    ///   - swkotor.exe: `FUN_0067bc40` / `FUN_006cfa70` reference `"_s.rim"` and strip suffix for root normalization.
    ///   - swkotor2.exe: `FUN_006d1a50` / `FUN_0073dcb0` do the same.
    /// - `.hak` is not used by KotOR/KotOR2 module loading (no `.hak` string presence in either exe; HAK is Aurora/NWN).
    /// - `_dlg.erf` is treated as an exact, hardcoded suffix in TSL (no evidence of a wildcard `_*.erf` rule in exes).
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
        /// </summary>
        /// <param name="modulesPath">Path to the modules directory</param>
        /// <param name="moduleRoot">Module root name (e.g., "001EBO")</param>
        /// <param name="game">Game type (K1 or K2)</param>
        /// <returns>ModuleFileGroup containing discovered files, or null if no files found</returns>
        public static ModuleFileGroup DiscoverModuleFiles(string modulesPath, string moduleRoot, Game game)
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

            // Check for .mod file first (highest priority - overrides all rim-like files)
            string modFileName = moduleRoot + ".mod";
            string modPath = TryGetFilePathByName(fileNameToPath, modFileName);
            if (modPath != null)
            {
                // .mod file exists - use only this file, ignore all rim-like files
                return new ModuleFileGroup
                {
                    ModuleRoot = moduleRoot,
                    ModFile = modPath,
                    MainRimFile = null,
                    DataRimFile = null,
                    DlgErfFile = null,
                    UsesModOverride = true
                };
            }

            // No .mod file - discover rim-like files
            string mainRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + ".rim");
            string dataRimPath = TryGetFilePathByName(fileNameToPath, moduleRoot + "_s.rim");
            string dlgErfPath = null;

            // K2 only: check for _dlg.erf file
            if (game.IsK2())
            {
                dlgErfPath = TryGetFilePathByName(fileNameToPath, moduleRoot + "_dlg.erf");
            }

            // Without a .mod file, the main .rim is required for a loadable module.
            if (mainRimPath == null)
            {
                return null;
            }

            return new ModuleFileGroup
            {
                ModuleRoot = moduleRoot,
                ModFile = null,
                MainRimFile = mainRimPath,
                DataRimFile = dataRimPath,
                DlgErfFile = dlgErfPath,
                UsesModOverride = false
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

            // Module containers:
            // - <root>.mod
            // - <root>.rim and <root>_s.rim
            // - (TSL) <root>_dlg.erf
            if (lowerName.EndsWith(".mod"))
                return true;
            if (lowerName.EndsWith(".rim"))
                return true;
            if (lowerName.EndsWith("_dlg.erf"))
                return true;

            return false;
        }

        /// <summary>
        /// Gets all module file paths for a module root, in priority order.
        /// </summary>
        /// <param name="modulesPath">Path to the modules directory</param>
        /// <param name="moduleRoot">Module root name</param>
        /// <param name="game">Game type</param>
        /// <returns>List of file paths in priority order (highest priority first)</returns>
        public static List<string> GetModuleFilePaths(string modulesPath, string moduleRoot, Game game)
        {
            ModuleFileGroup group = DiscoverModuleFiles(modulesPath, moduleRoot, game);
            if (group == null)
            {
                return new List<string>();
            }

            var paths = new List<string>();

            if (group.UsesModOverride)
            {
                // .mod file overrides all
                if (group.ModFile != null)
                {
                    paths.Add(group.ModFile);
                }
            }
            else
            {
                // Rim-like files in order: main, data, dlg
                if (group.MainRimFile != null)
                {
                    paths.Add(group.MainRimFile);
                }
                if (group.DataRimFile != null)
                {
                    paths.Add(group.DataRimFile);
                }
                if (group.DlgErfFile != null)
                {
                    paths.Add(group.DlgErfFile);
                }
            }

            return paths;
        }
    }

    /// <summary>
    /// Represents a group of module files for a single module root.
    /// </summary>
    public class ModuleFileGroup
    {
        public string ModuleRoot { get; set; }
        public string ModFile { get; set; }
        public string MainRimFile { get; set; }
        public string DataRimFile { get; set; }
        public string DlgErfFile { get; set; }
        public bool UsesModOverride { get; set; }

        /// <summary>
        /// Gets all file paths in this group.
        /// </summary>
        public List<string> GetAllFiles()
        {
            var files = new List<string>();
            if (ModFile != null) files.Add(ModFile);
            if (MainRimFile != null) files.Add(MainRimFile);
            if (DataRimFile != null) files.Add(DataRimFile);
            if (DlgErfFile != null) files.Add(DlgErfFile);
            return files;
        }

        /// <summary>
        /// Checks if this group has any files.
        /// </summary>
        public bool HasFiles()
        {
            return ModFile != null || MainRimFile != null || DataRimFile != null || DlgErfFile != null;
        }
    }
}

