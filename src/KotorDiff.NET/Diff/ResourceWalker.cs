// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:884-980
// Original: class ResourceWalker: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Installation;

namespace KotorDiff.NET.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:884-980
    // Original: class ResourceWalker: ...
    public class ResourceWalker
    {
        private readonly object _root;
        private readonly object _otherRoot; // Used to determine if composite loading should be enabled

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:887-890
        // Original: def __init__(self, root: Path, *, other_root: Path | None = None): ...
        public ResourceWalker(object root, object otherRoot = null)
        {
            _root = root;
            _otherRoot = otherRoot;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:892-900
        // Original: def __iter__(self) -> Iterator[ComparableResource]: ...
        public IEnumerable<ComparableResource> Walk()
        {
            if (_root is FileInfo fileInfo && IsCapsuleFile(fileInfo.Name))
            {
                return FromCapsule(fileInfo);
            }
            else if (_root is FileInfo singleFile)
            {
                return new[] { FromFile(singleFile, "") };
            }
            else if (_root is DirectoryInfo dirInfo && LooksLikeInstall(dirInfo))
            {
                return FromInstall(dirInfo);
            }
            else if (_root is DirectoryInfo dir)
            {
                return FromDirectory(dir);
            }

            return new List<ComparableResource>();
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:906-908
        // Original: @staticmethod def _looks_like_install(path: Path) -> bool: ...
        private static bool LooksLikeInstall(DirectoryInfo path)
        {
            return path.Exists && File.Exists(Path.Combine(path.FullName, "chitin.key"));
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:931-934
        // Original: def _from_file(self, file_path: Path, *, base_prefix: str) -> ComparableResource: ...
        private ComparableResource FromFile(FileInfo filePath, string basePrefix)
        {
            string ext = Path.GetExtension(filePath.Name).ToLowerInvariant().TrimStart('.');
            string identifier = !string.IsNullOrEmpty(basePrefix) 
                ? $"{basePrefix}{filePath.Name}" 
                : filePath.Name;
            byte[] data = File.ReadAllBytes(filePath.FullName);
            return new ComparableResource(identifier, ext, data);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:936-940
        // Original: def _from_directory(self, dir_path: Path) -> Iterable[ComparableResource]: ...
        private IEnumerable<ComparableResource> FromDirectory(DirectoryInfo dirPath)
        {
            foreach (var file in dirPath.GetFiles("*", SearchOption.AllDirectories).OrderBy(f => f.FullName))
            {
                string rel = Path.GetRelativePath(dirPath.FullName, file.FullName);
                string basePrefix = rel.Substring(0, rel.Length - file.Name.Length);
                yield return FromFile(file, basePrefix);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:942-962
        // Original: def _from_capsule(self, file_path: Path) -> Iterable[ComparableResource]: ...
        private IEnumerable<ComparableResource> FromCapsule(FileInfo filePath)
        {
            // TODO: Implement capsule loading
            // This should use CSharpKOTOR.Extract.Capsule to load ERF/RIM/MOD files
            // For now, return empty
            yield break;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:964-980
        // Original: def _from_install(self, install_root: Path) -> Iterable[ComparableResource]: ...
        private IEnumerable<ComparableResource> FromInstall(DirectoryInfo installRoot)
        {
            var results = new List<ComparableResource>();
            try
            {
                var installation = new Installation(installRoot.FullName);
                
                // Override files
                foreach (var resource in installation.OverrideResources())
                {
                    string identifier = Path.GetRelativePath(installRoot.FullName, resource.FilePath).Replace('\\', '/');
                    results.Add(new ComparableResource(identifier, resource.ResType.Extension.ToLowerInvariant(), resource.GetData()));
                }

                // Module capsules - get from modules directory
                string modulesPath = installation.ModulePath();
                if (Directory.Exists(modulesPath))
                {
                    foreach (var modFile in Directory.GetFiles(modulesPath, "*.rim").Concat(Directory.GetFiles(modulesPath, "*.mod")))
                    {
                        var modPath = new FileInfo(modFile);
                        if (modPath.Exists)
                        {
                            results.AddRange(FromCapsule(modPath));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty on error
            }

            return results;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:78-80
        // Original: def is_capsule_file(filename: str) -> bool: ...
        private static bool IsCapsuleFile(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return false;
            }

            string ext = Path.GetExtension(filename).ToLowerInvariant().TrimStart('.');
            return ext == "erf" || ext == "mod" || ext == "rim" || ext == "sav";
        }
    }
}

