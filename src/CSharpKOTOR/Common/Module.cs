// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py
// Original: """Module system for KotOR game modules."""
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.Capsule;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;

namespace CSharpKOTOR.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:78-184
    // Original: class KModuleType(Enum):
    /// <summary>
    /// Module file type enumeration.
    /// KotOR modules are split across multiple archive files.
    /// </summary>
    public enum KModuleType
    {
        /// <summary>
        /// Main module archive containing core module files (.rim)
        /// Contains: IFO (module info), ARE (area data), GIT (dynamic area info)
        /// </summary>
        MAIN = 0,  // ".rim"

        /// <summary>
        /// Data module archive containing module resources (_s.rim)
        /// Contains: UTC, UTD, UTE, UTI, UTM, UTP, UTS, UTT, UTW, FAC, LYT, NCS, PTH
        /// Note: In KotOR 2, DLG files are NOT in _s.rim (see K2_DLG)
        /// </summary>
        DATA = 1,  // "_s.rim"

        /// <summary>
        /// KotOR 2 dialog archive containing dialog files (_dlg.erf)
        /// Contains: DLG (dialog) files
        /// Note: KotOR 1 stores DLG files in _s.rim, KotOR 2 uses separate _dlg.erf
        /// </summary>
        K2_DLG = 2,  // "_dlg.erf"

        /// <summary>
        /// Community override module archive (single-file format) (.mod)
        /// Contains: All module resources in a single ERF archive
        /// Priority: Takes precedence over .rim/_s.rim/_dlg.erf files
        /// </summary>
        MOD = 3  // ".mod"
    }

    public static class KModuleTypeExtensions
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:136-183
        // Original: def contains(self, restype: ResourceType, *, game: Game | None = None) -> bool:
        public static bool Contains(this KModuleType moduleType, ResourceType restype, Game? game = null)
        {
            if (restype.TargetType() != restype)
            {
                return false;
            }

            if (restype == ResourceType.DLG)
            {
                if (game == null)
                {
                    return moduleType == KModuleType.DATA || moduleType == KModuleType.K2_DLG;
                }

                if (game.Value.IsK1())
                {
                    return moduleType == KModuleType.DATA;
                }

                if (game.Value.IsK2())
                {
                    return moduleType == KModuleType.K2_DLG;
                }
            }

            if (moduleType == KModuleType.MOD)
            {
                return restype != ResourceType.TwoDA;
            }

            if (moduleType == KModuleType.MAIN)
            {
                return restype == ResourceType.ARE || restype == ResourceType.IFO || restype == ResourceType.GIT;
            }

            if (moduleType == KModuleType.DATA)
            {
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:168-182
                // Original: return restype in {ResourceType.FAC, ResourceType.LYT, ...}
                return restype == ResourceType.FAC ||
                       restype == ResourceType.LYT ||
                       restype == ResourceType.NCS ||
                       restype == ResourceType.PTH ||
                       restype == ResourceType.UTC ||
                       restype == ResourceType.UTD ||
                       restype == ResourceType.UTE ||
                       restype == ResourceType.UTI ||
                       restype == ResourceType.UTM ||
                       restype == ResourceType.UTP ||
                       restype == ResourceType.UTS ||
                       restype == ResourceType.UTT ||
                       restype == ResourceType.UTW;
            }

            throw new InvalidOperationException($"Invalid ModuleType enum: {moduleType}");
        }

        public static string GetExtension(this KModuleType moduleType)
        {
            switch (moduleType)
            {
                case KModuleType.MAIN:
                    return ".rim";
                case KModuleType.DATA:
                    return "_s.rim";
                case KModuleType.K2_DLG:
                    return "_dlg.erf";
                case KModuleType.MOD:
                    return ".mod";
                default:
                    throw new ArgumentOutOfRangeException(nameof(moduleType), moduleType, null);
            }
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:186-213
    // Original: @dataclass(frozen=True) class ModulePieceInfo:
    /// <summary>
    /// Information about a module piece (archive file).
    /// </summary>
    public struct ModulePieceInfo
    {
        public string Root { get; }
        public KModuleType ModType { get; }

        public ModulePieceInfo(string root, KModuleType modType)
        {
            Root = root;
            ModType = modType;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:192-199
        // Original: @classmethod def from_filename(cls, filename: str | ResourceIdentifier) -> Self:
        public static ModulePieceInfo FromFilename(string filename)
        {
            string root = Module.NameToRoot(filename);
            string ext = Path.GetExtension(filename);
            KModuleType modType = ext switch
            {
                ".rim" when filename.Contains("_s.rim") => KModuleType.DATA,
                ".rim" => KModuleType.MAIN,
                "_dlg.erf" => KModuleType.K2_DLG,
                ".mod" => KModuleType.MOD,
                _ => throw new ArgumentException($"Unknown module type for filename: {filename}")
            };
            return new ModulePieceInfo(root, modType);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:201-202
        // Original: def filename(self) -> str:
        public string Filename()
        {
            return Root + ModType.GetExtension();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:207-209
        // Original: def resname(self) -> str:
        public string Resname()
        {
            string filename = Filename();
            int dotIndex = filename.IndexOf('.');
            return dotIndex >= 0 ? filename.Substring(0, dotIndex) : filename;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:211-212
        // Original: def restype(self) -> ResourceType:
        public ResourceType Restype()
        {
            return ResourceType.FromExtension(ModType.GetExtension());
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:516-548
    // Original: @staticmethod @lru_cache(maxsize=5000) def name_to_root(name: str) -> str:
    public static class Module
    {
        private static readonly Dictionary<string, string> _nameToRootCache = new Dictionary<string, string>();

        /// <summary>
        /// Extracts the root module name from a string path or filename.
        /// This method strips any path components, file extensions, and common module suffixes
        /// (_s, _dlg) to get the base module name. The result is cached for performance.
        /// </summary>
        public static string NameToRoot(string name)
        {
            if (_nameToRootCache.TryGetValue(name, out string cached))
            {
                return cached;
            }

            string[] splitPath = name.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string parsedName = splitPath.Length > 0 ? splitPath[splitPath.Length - 1] : name;
            int lastDot = parsedName.LastIndexOf('.');
            string nameWithoutExt = lastDot >= 0 ? parsedName.Substring(0, lastDot) : parsedName;
            string root = nameWithoutExt.Trim();
            string casefoldRoot = root.ToLowerInvariant();

            if (casefoldRoot.EndsWith("_s"))
            {
                root = root.Substring(0, root.Length - 2);
            }

            if (casefoldRoot.EndsWith("_dlg"))
            {
                root = root.Substring(0, root.Length - 4);
            }

            _nameToRootCache[name] = root;
            return root;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:550-573
        // Original: @staticmethod def filepath_to_root(filepath: os.PathLike | str) -> str:
        public static string FilepathToRoot(string filepath)
        {
            return NameToRoot(filepath);
        }
    }
}

