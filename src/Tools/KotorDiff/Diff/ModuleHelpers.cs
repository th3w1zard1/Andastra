// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:88-111, 1988-2007, 2010-2012
// Original: Various module helper functions
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Installation;
using BioWare.NET.Resource.Formats.Capsule;

namespace KotorDiff.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:88-93
    // Original: def get_module_root(module_filepath: Path) -> str: ...
    public static class ModuleHelpers
    {
        /// <summary>
        /// Extract the module root name, following Installation.py logic.
        /// </summary>
        public static string GetModuleRoot(string moduleFilepath)
        {
            string filename = Path.GetFileName(moduleFilepath);
            string root = Path.GetFileNameWithoutExtension(filename).ToLowerInvariant();
            root = root.EndsWith("_s") ? root.Substring(0, root.Length - 2) : root;
            root = root.EndsWith("_dlg") ? root.Substring(0, root.Length - 4) : root;
            return root;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1988-2007
        // Original: def group_module_files(files: set[str]) -> dict[str, list[str]]: ...
        /// <summary>
        /// Group module files by their root name.
        /// </summary>
        public static Dictionary<string, List<string>> GroupModuleFiles(HashSet<string> files)
        {
            var moduleGroups = new Dictionary<string, List<string>>();

            foreach (string filePath in files)
            {
                string filename = Path.GetFileName(filePath);
                if (IsCapsuleFile(filename))
                {
                    string root = GetModuleRoot(filePath);
                    if (!moduleGroups.ContainsKey(root))
                    {
                        moduleGroups[root] = new List<string>();
                    }
                    moduleGroups[root].Add(filePath);
                }
            }

            return moduleGroups;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2010-2012
        // Original: def is_modules_directory(dir_path: Path) -> bool: ...
        /// <summary>
        /// Check if a directory is a modules directory.
        /// </summary>
        public static bool IsModulesDirectory(DirectoryInfo dirPath)
        {
            string name = dirPath.Name.ToLowerInvariant();
            return name == "modules" || name == "module" || name == "mods";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:78-80
        // Original: def is_capsule_file(filename: str) -> bool: ...
        /// <summary>
        /// Check if a filename is a capsule file (ERF, RIM, MOD, SAV).
        /// </summary>
        public static bool IsCapsuleFile(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return false;
            }

            string ext = Path.GetExtension(filename).ToLowerInvariant().TrimStart('.');
            return ext == "erf" || ext == "mod" || ext == "rim" || ext == "sav";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1906-1927
        // Original: def should_use_composite_for_file(...): ...
        /// <summary>
        /// Determine if composite module loading should be used for a specific file.
        /// Only use composite loading for .rim files when comparing against .mod files.
        /// </summary>
        public static bool ShouldUseCompositeForFile(string filePath, string otherFilePath)
        {
            var file = new FileInfo(filePath);
            var otherFile = new FileInfo(otherFilePath);

            // Check if this file is a .rim file (not in rims folder)
            if (!IsCapsuleFile(file.Name))
            {
                return false;
            }
            if (file.Directory != null && file.Directory.Name.ToLowerInvariant() == "rims")
            {
                return false;
            }
            if (Path.GetExtension(file.Name).ToLowerInvariant() != ".rim")
            {
                return false;
            }

            // Check if the other file is a .mod file (not in rims folder)
            if (!IsCapsuleFile(otherFile.Name))
            {
                return false;
            }
            if (otherFile.Directory != null && otherFile.Directory.Name.ToLowerInvariant() == "rims")
            {
                return false;
            }
            return Path.GetExtension(otherFile.Name).ToLowerInvariant() == ".mod";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2015-2106
        // Original: def apply_folder_resolution_order(...): ...
        /// <summary>
        /// Apply folder-level resolution order to module files.
        /// When both .mod and .rim/_s.rim/_dlg.erf files exist for the same module, .mod takes priority.
        /// </summary>
        public static HashSet<string> ApplyFolderResolutionOrder(HashSet<string> files, Action<string> logFunc)
        {
            var moduleGroups = new Dictionary<string, List<string>>();
            var nonModuleFiles = new List<string>();

            string FileExtMatch(string name)
            {
                string nameLower = name.ToLowerInvariant();
                if (nameLower.EndsWith(".mod"))
                {
                    return ".mod";
                }
                if (nameLower.EndsWith("_dlg.erf"))
                {
                    return "_dlg.erf";
                }
                if (nameLower.EndsWith("_s.rim"))
                {
                    return "_s.rim";
                }
                if (nameLower.EndsWith(".rim"))
                {
                    return ".rim";
                }
                return null;
            }

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string ext = FileExtMatch(fileName);
                if (ext != null)
                {
                    try
                    {
                        string root = GetModuleRoot(filePath);
                        if (!moduleGroups.ContainsKey(root))
                        {
                            moduleGroups[root] = new List<string>();
                        }
                        moduleGroups[root].Add(filePath);
                    }
                    catch (Exception e)
                    {
                        logFunc($"Warning: Could not determine module root for '{filePath}': {e.GetType().Name}: {e.Message}");
                        nonModuleFiles.Add(filePath);
                    }
                }
                else
                {
                    nonModuleFiles.Add(filePath);
                }
            }

            var resolvedFiles = new HashSet<string>(nonModuleFiles, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in moduleGroups)
            {
                string root = kvp.Key;
                List<string> groupFiles = kvp.Value;

                // Partition into .mod (highest priority) and rim group (exclusively .rim, _s.rim, _dlg.erf)
                var modFiles = groupFiles.Where(f => Path.GetFileName(f).ToLowerInvariant().EndsWith(".mod")).ToList();
                var rimlikeFiles = groupFiles.Where(f =>
                {
                    string fname = Path.GetFileName(f).ToLowerInvariant();
                    return (fname.EndsWith(".rim") && !fname.EndsWith("_s.rim")) ||
                           fname.EndsWith("_s.rim") ||
                           fname.EndsWith("_dlg.erf");
                }).ToList();

                if (modFiles.Count > 0 || rimlikeFiles.Count > 0)
                {
                    logFunc($"\nFolder resolution for module '{root}':");
                    logFunc("  Files found:");
                    foreach (string rimFile in rimlikeFiles)
                    {
                        logFunc($"    - {Path.GetFileName(rimFile)} (.rim/_s.rim/_dlg.erf)");
                    }
                    foreach (string modFile in modFiles)
                    {
                        logFunc($"    - {Path.GetFileName(modFile)} (.mod)");
                    }
                }

                if (modFiles.Count > 0)
                {
                    // .mod exists - use it, ignore rimlike group
                    if (modFiles.Count > 1)
                    {
                        logFunc($"  Warning: Multiple .mod files for module '{root}'");
                    }

                    if (rimlikeFiles.Count > 0)
                    {
                        logFunc($"  Resolution: .mod takes priority -> Using '{Path.GetFileName(modFiles[0])}'");
                        logFunc($"              (ignoring {rimlikeFiles.Count} .rim/_s.rim/_dlg.erf files)");
                    }
                    else
                    {
                        logFunc($"  Resolution: Using '{Path.GetFileName(modFiles[0])}' (.mod file)");
                    }

                    resolvedFiles.Add(modFiles[0]);
                }
                else
                {
                    // No .mod - use all .rim/_s.rim/_dlg.erf files
                    if (rimlikeFiles.Count > 0)
                    {
                        logFunc($"  Resolution: No .mod found -> Using {rimlikeFiles.Count} .rim/_s.rim/_dlg.erf file(s)");
                    }
                    foreach (string filePath in rimlikeFiles)
                    {
                        resolvedFiles.Add(filePath);
                    }
                }
            }

            return resolvedFiles;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2723-2735
        // Original: def determine_composite_loading(...): ...
        /// <summary>
        /// Determine if composite loading should be used and find related files.
        /// </summary>
        public static Tuple<bool, List<string>, string> DetermineCompositeLoading(string containerPath)
        {
            string moduleRoot = GetModuleRoot(containerPath);
            string containerDir = Path.GetDirectoryName(containerPath);
            var relatedFiles = new List<string>();

            string[] extensions = { ".mod", ".rim", "_s.rim", "_dlg.erf" };
            foreach (string ext in extensions)
            {
                string relatedFile = Path.Combine(containerDir, $"{moduleRoot}{ext}");
                if (File.Exists(relatedFile))
                {
                    relatedFiles.Add(relatedFile);
                }
            }

            bool useComposite = relatedFiles.Count > 1;
            return Tuple.Create(useComposite, relatedFiles, moduleRoot);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1930-1943
        // Original: def load_capsule(...): ...
        /// <summary>
        /// Load a capsule file, either as a simple Capsule or CompositeModuleCapsule.
        /// </summary>
        public static object LoadCapsule(string filePath, bool useComposite, Action<string> logFunc)
        {
            try
            {
                if (useComposite)
                {
                    return new CompositeModuleCapsule(filePath);
                }
                return new BioWare.NET.Resource.Formats.Capsule.Capsule(filePath);
            }
            catch (Exception e)
            {
                if (logFunc != null)
                {
                    logFunc($"Could not load '{filePath}'. Reason: {e.GetType().Name}: {e.Message}");
                }
                return null;
            }
        }
    }
}

