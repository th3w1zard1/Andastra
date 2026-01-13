// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:782-811
// Original: class CompositeModuleCapsule: ...
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BioWare.NET;
using BioWare.NET.Resource.Formats.Capsule;
using BioWare.NET.Installation;

namespace KotorDiff.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:782-811
    // Original: class CompositeModuleCapsule: ...
    /// <summary>
    /// A capsule that aggregates resources from multiple related module files.
    /// </summary>
    public class CompositeModuleCapsule : IEnumerable<CapsuleResource>
    {
        private readonly FileInfo _primaryPath;
        private readonly List<FileInfo> _relatedFiles;
        private readonly Dictionary<string, Capsule> _capsules;

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:785-800
        // Original: def __init__(self, primary_module_path: CaseAwarePath): ...
        public CompositeModuleCapsule(string primaryModulePath)
        {
            _primaryPath = new FileInfo(primaryModulePath);
            _relatedFiles = FindRelatedModuleFiles(_primaryPath);
            _capsules = new Dictionary<string, Capsule>();

            // Load all related capsules
            foreach (var filePath in _relatedFiles)
            {
                try
                {
                    _capsules[filePath.FullName] = new Capsule(filePath.FullName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cannot load {filePath.FullName} as capsule!");
                    Console.WriteLine("Full traceback:");
                    Console.WriteLine($"  {ex}");
                    // Continue loading other files
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:802-805
        // Original: def __iter__(self): ...
        public IEnumerator<CapsuleResource> GetEnumerator()
        {
            foreach (var capsule in _capsules.Values)
            {
                foreach (var res in capsule)
                {
                    yield return res;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:807-810
        // Original: @property def name(self) -> str: ...
        public string Name
        {
            get
            {
                return _primaryPath.Name;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:96-110
        // Original: def find_related_module_files(module_path: CaseAwarePath) -> list[CaseAwarePath]: ...
        private static List<FileInfo> FindRelatedModuleFiles(FileInfo modulePath)
        {
            string root = GetModuleRoot(modulePath);
            DirectoryInfo moduleDir = modulePath.Directory;

            // Possible extensions for related module files
            string[] extensions = { ".rim", ".mod", "_s.rim", "_dlg.erf" };
            var relatedFiles = new List<FileInfo>();

            foreach (string ext in extensions)
            {
                string candidatePath = Path.Combine(moduleDir.FullName, $"{root}{ext}");
                var candidate = new FileInfo(candidatePath);
                if (candidate.Exists)
                {
                    relatedFiles.Add(candidate);
                }
            }

            return relatedFiles;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:88-93
        // Original: def get_module_root(module_filepath: Path) -> str: ...
        private static string GetModuleRoot(FileInfo moduleFilepath)
        {
            string root = Path.GetFileNameWithoutExtension(moduleFilepath.Name).ToLowerInvariant();
            root = root.EndsWith("_s") ? root.Substring(0, root.Length - 2) : root;
            root = root.EndsWith("_dlg") ? root.Substring(0, root.Length - 4) : root;
            return root;
        }
    }
}

