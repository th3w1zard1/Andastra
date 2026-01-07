using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Andastra.Parsing.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:70-75
    // Original: SEARCH_ORDER: list[SearchLocation] = [SearchLocation.OVERRIDE, SearchLocation.CUSTOM_MODULES, SearchLocation.CHITIN]
    public static class ModuleSearchOrder
    {
        public static readonly SearchLocation[] Order = {
            SearchLocation.OVERRIDE,
            SearchLocation.CUSTOM_MODULES,
            SearchLocation.CHITIN,
        };
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:78-184
    // Original: class KModuleType(Enum):
    /// <summary>
    /// Module file type enumeration.
    /// KotOR modules are split across multiple archive files. The module system
    /// uses different file extensions to organize resources by type and priority.
    /// </summary>
    public enum KModuleType
    {
        /// <summary>
        /// Main module archive containing core module files.
        /// Contains: IFO (module info), ARE (area data), GIT (dynamic area info)
        /// File naming: &lt;modulename&gt;.rim
        /// Note: Only loaded in simple mode (flag at offset 0x54 == 0)
        /// swkotor.exe: FUN_004094a0 line 32-42
        /// </summary>
        MAIN,  // .rim

        /// <summary>
        /// Area-specific module archive (complex mode).
        /// Contains: ARE (area data) and related area resources
        /// File naming: &lt;modulename&gt;_a.rim
        /// Behavior: REPLACES .rim in complex mode
        /// swkotor.exe: FUN_004094a0 line 61, 159
        /// </summary>
        AREA,  // _a.rim

        /// <summary>
        /// Extended area module archive (complex mode).
        /// Contains: ARE (area data) and extended area resources
        /// File naming: &lt;modulename&gt;_adx.rim
        /// Behavior: REPLACES .rim if _a.rim not found
        /// swkotor.exe: FUN_004094a0 line 74, 85
        /// </summary>
        AREA_EXTENDED,  // _adx.rim

        /// <summary>
        /// Data module archive containing module resources.
        /// Contains: UTC, UTD, UTE, UTI, UTM, UTP, UTS, UTT, UTW, FAC, LYT, NCS, PTH
        /// File naming: &lt;modulename&gt;_s.rim
        /// Behavior: ADDS to base (only if .mod not found)
        /// Note: In KotOR 2, DLG files are NOT in _s.rim (see K2_DLG)
        /// swkotor.exe: FUN_004094a0 line 107, 118
        /// </summary>
        DATA,  // _s.rim

        /// <summary>
        /// KotOR 2 dialog archive containing dialog files.
        /// Contains: DLG (dialog) files
        /// File naming: &lt;modulename&gt;_dlg.erf
        /// Behavior: ADDS to base (only if .mod not found, K2 only)
        /// Note: KotOR 1 stores DLG files in _s.rim, KotOR 2 uses separate _dlg.erf
        /// swkotor2.exe: FUN_004096b0 line 128, 147
        /// </summary>
        K2_DLG,  // _dlg.erf

        /// <summary>
        /// Community override module archive (single-file format).
        /// Contains: All module resources in a single ERF archive
        /// File naming: &lt;modulename&gt;.mod
        /// Behavior: REPLACES all other files (skips _s.rim/_dlg.erf)
        /// Priority: Takes precedence over .rim/_s.rim/_dlg.erf files
        /// swkotor.exe: FUN_004094a0 line 95, 136
        /// </summary>
        MOD  // .mod
    }

    public static class KModuleTypeExtensions
    {
        public static string GetExtension(this KModuleType type)
        {
            switch (type)
            {
                case KModuleType.MAIN:
                    return ".rim";
                case KModuleType.AREA:
                    return "_a.rim";
                case KModuleType.AREA_EXTENDED:
                    return "_adx.rim";
                case KModuleType.DATA:
                    return "_s.rim";
                case KModuleType.K2_DLG:
                    return "_dlg.erf";  // TSL only
                case KModuleType.MOD:
                    return ".mod";
                default:
                    throw new ArgumentException($"Invalid KModuleType: {type}");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:136-183
        // Original: def contains(self, restype: ResourceType, *, game: Game | None = None) -> bool:
        //
        // REVERSE ENGINEERING NOTES (swkotor.exe / swkotor2.exe):
        // This method implements the CONVENTIONAL resource type distribution for module files.
        // IMPORTANT: This is a CONVENTION, not a hard engine requirement.
        //
        // Engine Behavior (reverse engineered):
        // - The engine's resource manager (CExoResMan equivalent) does NOT filter resource types when loading from modules
        // - Module loading code (swkotor.exe: FUN_004094a0, swkotor2.exe: FUN_004096b0) opens RIM files without type filtering
        // - Container formats (RIM/ERF/MOD) accept ANY resource type ID - no validation at container level
        // - The engine will attempt to load ANY resource type stored in a module container, as long as:
        //   1. The resource type ID is valid
        //   2. The resource data can be parsed by the appropriate loader
        // Our implementation will enforce that:
        // - ResRef is a flat 16-byte ASCII string (no path separators)
        // - Container format has no subfolder/hierarchy support
        // - Module loading code only handles filename-based discovery
        public static bool Contains(this KModuleType type, ResourceType restype, BioWareGame? game = null)
        {
            if (restype.TargetType() != restype)
            {
                return false;
            }

            if (restype == ResourceType.DLG)
            {
                if (game == null)
                {
                    return type == KModuleType.DATA || type == KModuleType.K2_DLG;
                }
                if (game.Value.IsK1())
                {
                    return type == KModuleType.DATA;
                }
                if (game.Value.IsK2())
                {
                    return type == KModuleType.K2_DLG;
                }
            }

            if (type == KModuleType.MOD)
            {
                return restype != ResourceType.TwoDA;
            }

            if (type == KModuleType.MAIN)
            {
                return restype == ResourceType.ARE || restype == ResourceType.IFO || restype == ResourceType.GIT;
            }

            // _a.rim and _adx.rim contain ARE resources (swkotor.exe: FUN_004094a0 line 61, 74 - checks for ARE type 0xbba)
            if (type == KModuleType.AREA || type == KModuleType.AREA_EXTENDED)
            {
                return restype == ResourceType.ARE || restype == ResourceType.IFO || restype == ResourceType.GIT;
            }

            if (type == KModuleType.DATA)
            {
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

            throw new InvalidOperationException($"Invalid KModuleType enum: {type}");
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:186-213
    // Original: @dataclass(frozen=True) class ModulePieceInfo:
    /// <summary>
    /// Information about a module piece (archive file).
    /// </summary>
    public sealed class ModulePieceInfo
    {
        public string Root { get; }
        public KModuleType ModType { get; }

        public ModulePieceInfo(string root, KModuleType modType)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            ModType = modType;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:191-199
        // Original: @classmethod def from_filename(cls, filename: str | ResourceIdentifier) -> Self:
        public static ModulePieceInfo FromFilename(string filename)
        {
            if (filename == null)
            {
                throw new ArgumentNullException(nameof(filename));
            }

            string root = Module.NameToRoot(filename);
            string extension = filename.Substring(root.Length);
            KModuleType modType;
            switch (extension)
            {
                case ".rim":
                    modType = KModuleType.MAIN;
                    break;
                case "_a.rim":
                    modType = KModuleType.AREA;
                    break;
                case "_adx.rim":
                    modType = KModuleType.AREA_EXTENDED;
                    break;
                case "_s.rim":
                    modType = KModuleType.DATA;
                    break;
                case "_dlg.erf":
                    modType = KModuleType.K2_DLG;
                    break;
                case ".mod":
                    modType = KModuleType.MOD;
                    break;
                default:
                    throw new ArgumentException($"Unknown module extension: {extension}");
            }

            return new ModulePieceInfo(root, modType);
        }

        public static ModulePieceInfo FromFilename(ResourceIdentifier identifier)
        {
            return FromFilename(identifier.ToString());
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:201-202
        // Original: def filename(self) -> str:
        public string Filename()
        {
            return Root + ModType.GetExtension();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:204-205
        // Original: def res_ident(self) -> ResourceIdentifier:
        public ResourceIdentifier ResIdent()
        {
            return ResourceIdentifier.FromPath(Filename());
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:207-209
        // Original: def resname(self) -> str:
        public string ResName()
        {
            string filename = Filename();
            int dotIndex = filename.IndexOf('.');
            return dotIndex >= 0 ? filename.Substring(0, dotIndex) : filename;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:211-212
        // Original: def restype(self) -> ResourceType:
        public ResourceType ResType()
        {
            return ResourceType.FromExtension(ModType.GetExtension());
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:215-258
    // Original: class ModulePieceResource(Capsule):
    /// <summary>
    /// Base class for module piece resources (archive files that make up a module).
    /// </summary>
    public abstract class ModulePieceResource : Capsule
    {
        public ModulePieceInfo PieceInfo { get; }
        public List<FileResource> MissingResources { get; } = new List<FileResource>();

        protected ModulePieceResource(string path, bool createIfNotExist = false)
            : base(path, createIfNotExist)
        {
            CaseAwarePath pathObj = new CaseAwarePath(path);
            PieceInfo = ModulePieceInfo.FromFilename(pathObj.Name);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:216-234
        // Original: def __new__(cls, path: os.PathLike | str, *args, **kwargs):
        // Factory method to create the appropriate ModulePieceResource subclass based on file extension
        public static ModulePieceResource Create(string path, bool createIfNotExist = false)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            CaseAwarePath pathObj = new CaseAwarePath(path);
            ModulePieceInfo pieceInfo = ModulePieceInfo.FromFilename(pathObj.Name);

            switch (pieceInfo.ModType)
            {
                case KModuleType.DATA:
                    return new ModuleDataPiece(path, createIfNotExist);
                case KModuleType.MAIN:
                    return new ModuleLinkPiece(path, createIfNotExist);
                case KModuleType.AREA:
                    return new ModuleLinkPiece(path, createIfNotExist);  // _a.rim uses same structure as .rim
                case KModuleType.AREA_EXTENDED:
                    return new ModuleLinkPiece(path, createIfNotExist);  // _adx.rim uses same structure as .rim
                case KModuleType.K2_DLG:
                    return new ModuleDLGPiece(path, createIfNotExist);
                case KModuleType.MOD:
                    return new ModuleFullOverridePiece(path, createIfNotExist);
                default:
                    throw new ArgumentException($"Unknown module type: {pieceInfo.ModType}");
            }
        }

        public string Filename()
        {
            return PieceInfo.Filename();
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:260-312
    // Original: class ModuleLinkPiece(ModulePieceResource):
    /// <summary>
    /// Represents the main module archive (.rim) containing IFO, ARE, and GIT files.
    /// </summary>
    public class ModuleLinkPiece : ModulePieceResource
    {
        public ModuleLinkPiece(string path, bool createIfNotExist = false)
            : base(path, createIfNotExist)
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:261-267
        // Original: def ifo(self) -> GFF:
        public GFF Ifo()
        {
            byte[] lookup = GetResource("module", ResourceType.IFO);
            if (lookup == null)
            {
                string moduleIfoPath = Path.Combine(System.IO.Path.GetDirectoryName(this.Path.ToString()), "module.ifo");
                throw new FileNotFoundException($"Module IFO not found", moduleIfoPath);
            }

            var reader = new Formats.GFF.GFFBinaryReader(lookup);
            return reader.Load();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:269-295
        // Original: def module_id(self) -> ResRef | None:
        public ResRef ModuleId()
        {
            // Get link resources (non-IFO resources that MAIN contains)
            var linkResources = new List<CapsuleResource>();
            foreach (CapsuleResource resource in this)
            {
                if (resource.ResType != ResourceType.IFO && KModuleType.MAIN.Contains(resource.ResType, null))
                {
                    linkResources.Add(resource);
                }
            }

            if (linkResources.Count > 0)
            {
                string checkResname = linkResources[0].Identifier.LowerResName;
                if (linkResources.All(res => res.Identifier.LowerResName == checkResname))
                {
                    Console.WriteLine($"Module ID, Check 1: All link resources have the same resref of '{checkResname}'");
                    return new ResRef(checkResname);
                }
            }

            GFF gffIfo = Ifo();
            if (gffIfo.Root.Exists("Mod_Area_list"))
            {
                GFFFieldType? actualFtype = gffIfo.Root.GetFieldType("Mod_Area_list");
                if (actualFtype != GFFFieldType.List)
                {
                    new Andastra.Parsing.Logger.RobustLogger().Warning($"{Filename()} has IFO with incorrect field 'Mod_Area_list' type '{actualFtype}', expected 'List'");
                }
                else
                {
                    GFFList areaList = gffIfo.Root.GetList("Mod_Area_list");
                    if (areaList == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Error($"{Filename()}: Module.IFO has a Mod_Area_list field, but it is not a valid list.");
                        return null;
                    }

                    foreach (GFFStruct gffStruct in areaList)
                    {
                        if (gffStruct.Exists("Area_Name"))
                        {
                            ResRef areaLocalizedName = gffStruct.GetResRef("Area_Name");
                            if (areaLocalizedName != null && areaLocalizedName.ToString().Trim().Length > 0)
                            {
                                Console.WriteLine($"Module ID, Check 2: Found in Mod_Area_list: '{areaLocalizedName}'");
                                return areaLocalizedName;
                            }
                        }
                    }

                    Console.WriteLine($"{Filename()}: Module.IFO does not contain a valid Mod_Area_list. Could not get the module id!");
                }
            }
            else
            {
                new Andastra.Parsing.Logger.RobustLogger().Error($"{Filename()}: Module.IFO does not have an existing Mod_Area_list.");
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:297-312
        // Original: def area_name(self) -> LocalizedString | ResRef:
        public LocalizedString AreaName()
        {
            CapsuleResource areaFileRes = null;
            foreach (CapsuleResource resource in this)
            {
                if (resource.ResType == ResourceType.ARE)
                {
                    areaFileRes = resource;
                    break;
                }
            }

            if (areaFileRes != null)
            {
                byte[] areData = areaFileRes.Data;
                var reader = new Formats.GFF.GFFBinaryReader(areData);
                GFF gffAre = reader.Load();

                if (gffAre.Root.Exists("Name"))
                {
                    GFFFieldType? actualFtype = gffAre.Root.GetFieldType("Name");
                    if (actualFtype != GFFFieldType.LocalizedString)
                    {
                        throw new ArgumentException($"{Filename()} has IFO with incorrect field 'Name' type '{actualFtype}', expected 'LocalizedString'");
                    }

                    LocalizedString result = gffAre.Root.GetLocString("Name");
                    if (result == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Error($"{Filename()}: ARE has a Name field, but it is not a valid LocalizedString.");
                        return LocalizedString.FromInvalid();
                    }

                    Console.WriteLine($"Check 1 result: '{result}'");
                    return result;
                }
            }

            throw new ArgumentException($"Failed to find an ARE for module '{PieceInfo.Filename()}'");
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:315
    // Original: class ModuleDataPiece(ModulePieceResource): ...
    /// <summary>
    /// Represents the data module archive (_s.rim) containing module resources.
    /// </summary>
    public class ModuleDataPiece : ModulePieceResource
    {
        public ModuleDataPiece(string path, bool createIfNotExist = false)
            : base(path, createIfNotExist)
        {
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:318
    // Original: class ModuleDLGPiece(ModulePieceResource): ...
    /// <summary>
    /// Represents the KotOR 2 dialog archive (_dlg.erf) containing dialog files.
    /// </summary>
    public class ModuleDLGPiece : ModulePieceResource
    {
        public ModuleDLGPiece(string path, bool createIfNotExist = false)
            : base(path, createIfNotExist)
        {
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:321
    // Original: class ModuleFullOverridePiece(ModuleDLGPiece, ModuleDataPiece, ModuleLinkPiece): ...
    /// <summary>
    /// Represents the community override module archive (.mod) that replaces all other module files.
    /// This class combines functionality from ModuleDLGPiece, ModuleDataPiece, and ModuleLinkPiece.
    /// </summary>
    public class ModuleFullOverridePiece : ModulePieceResource
    {
        private ModuleLinkPiece _linkPiece;

        public ModuleFullOverridePiece(string path, bool createIfNotExist = false)
            : base(path, createIfNotExist)
        {
            // Create a ModuleLinkPiece view of this same file to access link piece methods
            _linkPiece = new ModuleLinkPiece(path, createIfNotExist);
        }

        // This class combines functionality from ModuleDLGPiece, ModuleDataPiece, and ModuleLinkPiece
        // In C#, we implement the methods directly rather than using multiple inheritance
        public GFF Ifo()
        {
            return _linkPiece.Ifo();
        }

        public ResRef ModuleId()
        {
            return _linkPiece.ModuleId();
        }

        public LocalizedString AreaName()
        {
            return _linkPiece.AreaName();
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:331-2131
    // Original: class Module:
    /// <summary>
    /// Represents a KotOR game module with its resources and archives.
    /// A Module aggregates resources from multiple archive files (.rim, _s.rim, _dlg.erf)
    /// or a single override archive (.mod). It manages resource loading, activation,
    /// and provides access to module-specific resources like areas, creatures, items, etc.
    /// </summary>
    public class Module
    {
        private readonly Dictionary<ResourceIdentifier, ModuleResource> _resources = new Dictionary<ResourceIdentifier, ModuleResource>();
        private bool _dotMod;
        // private readonly Installation.Installation _installation;
        private readonly string _root;
        private ResRef _cachedModId;
        private readonly Dictionary<string, ModulePieceResource> _capsules = new Dictionary<string, ModulePieceResource>();
        private HashSet<ResourceIdentifier> _gitSearch;

        public Dictionary<ResourceIdentifier, ModuleResource> Resources => _resources;
        public bool DotMod => _dotMod;
        // public Installation.Installation Installation => _installation;
        public string Root => _root;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:379-416
        // Original: def __init__(self, filename_or_root: str, installation: Installation, *, use_dot_mod: bool = True):
        public Module(string filenameOrRoot, Installation.Installation installation, bool useDotMod = true)
        {
            if (filenameOrRoot == null)
            {
                throw new ArgumentNullException(nameof(filenameOrRoot));
            }
            if (installation == null)
            {
                throw new ArgumentNullException(nameof(installation));
            }

            _installation = installation;
            _dotMod = useDotMod;
            _root = NameToRoot(filenameOrRoot.ToLowerInvariant());
            _cachedModId = null;

            // Build all capsules relevant to this root in the provided installation
            // Use ModuleFileDiscovery to match exact swkotor.exe/swkotor2.exe behavior
            // string modulesPath = Andastra.Parsing.Installation.Installation.GetModulesPath(_installation.Path);
            Andastra.Parsing.Installation.ModuleFileGroup fileGroup =
                Andastra.Parsing.Installation.ModuleFileDiscovery.DiscoverModuleFiles(modulesPath, _root, _installation.Game);

            if (fileGroup == null || !fileGroup.HasFiles())
            {
                // No module files found - create empty capsules (will fail on access)
                // This matches original behavior where missing files cause errors on access
                return;
            }

            if (fileGroup.UsesModOverride)
            {
                // .mod file overrides all rim-like files
                // swkotor.exe: FUN_004094a0 line 136: Loads .mod, skips _s.rim
                _dotMod = true;
                if (fileGroup.ModFile != null && File.Exists(fileGroup.ModFile))
                {
                    _capsules[KModuleType.MOD.ToString()] = new ModuleFullOverridePiece(fileGroup.ModFile);
                }
            }
            else if (fileGroup.UseComplexMode)
            {
                // Complex mode: Load _a.rim or _adx.rim (replaces .rim), then _s.rim and _dlg.erf
                // swkotor.exe: FUN_004094a0 line 49-216
                _dotMod = false;

                // Step 1: Load _a.rim if exists (REPLACES .rim)
                // swkotor.exe: FUN_004094a0 line 159
                if (fileGroup.AreaRimFile != null && File.Exists(fileGroup.AreaRimFile))
                {
                    _capsules[KModuleType.AREA.ToString()] = new ModuleLinkPiece(fileGroup.AreaRimFile);
                }
                // Step 2: Load _adx.rim if _a.rim not found (REPLACES .rim)
                // swkotor.exe: FUN_004094a0 line 85
                else if (fileGroup.AreaExtendedRimFile != null && File.Exists(fileGroup.AreaExtendedRimFile))
                {
                    _capsules[KModuleType.AREA_EXTENDED.ToString()] = new ModuleLinkPiece(fileGroup.AreaExtendedRimFile);
                }

                // Step 3: Load _s.rim if exists (ADDS to base)
                // swkotor.exe: FUN_004094a0 line 118 (only if .mod not found)
                if (fileGroup.DataRimFile != null && File.Exists(fileGroup.DataRimFile))
                {
                    _capsules[KModuleType.DATA.ToString()] = new ModuleDataPiece(fileGroup.DataRimFile);
                }

                // Step 4: Load _dlg.erf if exists (K2 only, ADDS to base)
                // swkotor2.exe: FUN_004096b0 line 147 (only if .mod not found)
                if (fileGroup.DlgErfFile != null && File.Exists(fileGroup.DlgErfFile))
                {
                    _capsules[KModuleType.K2_DLG.ToString()] = new ModuleDLGPiece(fileGroup.DlgErfFile);
                }
            }
            else
            {
                // Simple mode: Just load .rim file directly
                // swkotor.exe: FUN_004094a0 line 32-42
                _dotMod = false;
                if (fileGroup.MainRimFile != null && File.Exists(fileGroup.MainRimFile))
                {
                    _capsules[KModuleType.MAIN.ToString()] = new ModuleLinkPiece(fileGroup.MainRimFile);
                }
            }

            ReloadResources();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:516-548
        // Original: @staticmethod @lru_cache(maxsize=5000) def name_to_root(name: str) -> str:
        private static readonly Dictionary<string, string> _nameToRootCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string NameToRoot(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

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
            if (casefoldRoot.EndsWith("_a"))
            {
                root = root.Substring(0, root.Length - 2);
            }
            if (casefoldRoot.EndsWith("_adx"))
            {
                root = root.Substring(0, root.Length - 4);
            }

            // Cache result (limit cache size to 5000 as in Python)
            if (_nameToRootCache.Count < 5000)
            {
                _nameToRootCache[name] = root;
            }

            return root;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:550-573
        // Original: @staticmethod def filepath_to_root(filepath: os.PathLike | str) -> str:
        public static string FilepathToRoot(string filepath)
        {
            if (filepath == null)
            {
                throw new ArgumentNullException(nameof(filepath));
            }
            return NameToRoot(filepath);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:464-465
        // Original: def root(self) -> str:
        public string GetRoot()
        {
            return _root.Trim();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:467-478
        // Original: def lookup_main_capsule(self) -> ModuleFullOverridePiece | ModuleLinkPiece:
        public ModulePieceResource LookupMainCapsule()
        {
            ModulePieceResource relevantCapsule = null;
            if (_dotMod)
            {
                // .mod overrides all
                if (_capsules.TryGetValue(KModuleType.MOD.ToString(), out ModulePieceResource modCapsule))
                {
                    relevantCapsule = modCapsule;
                }
                else if (_capsules.TryGetValue(KModuleType.MAIN.ToString(), out ModulePieceResource mainCapsule))
                {
                    relevantCapsule = mainCapsule;
                }
            }
            else
            {
                // Complex mode: Check for _a.rim or _adx.rim first (replaces .rim)
                // Simple mode: Just .rim
                if (_capsules.TryGetValue(KModuleType.AREA.ToString(), out ModulePieceResource areaCapsule))
                {
                    relevantCapsule = areaCapsule;
                }
                else if (_capsules.TryGetValue(KModuleType.AREA_EXTENDED.ToString(), out ModulePieceResource areaExtCapsule))
                {
                    relevantCapsule = areaExtCapsule;
                }
                else if (_capsules.TryGetValue(KModuleType.MAIN.ToString(), out ModulePieceResource mainCapsule))
                {
                    relevantCapsule = mainCapsule;
                }
            }

            if (relevantCapsule == null)
            {
                throw new InvalidOperationException("No main capsule found for module");
            }

            return relevantCapsule;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:480-491
        // Original: def lookup_data_capsule(self) -> ModuleFullOverridePiece | ModuleDataPiece:
        public ModulePieceResource LookupDataCapsule()
        {
            ModulePieceResource relevantCapsule = null;
            if (_dotMod)
            {
                if (_capsules.TryGetValue(KModuleType.MOD.ToString(), out ModulePieceResource modCapsule))
                {
                    relevantCapsule = modCapsule;
                }
                else if (_capsules.TryGetValue(KModuleType.DATA.ToString(), out ModulePieceResource dataCapsule))
                {
                    relevantCapsule = dataCapsule;
                }
            }
            else
            {
                _capsules.TryGetValue(KModuleType.DATA.ToString(), out relevantCapsule);
            }

            if (relevantCapsule == null)
            {
                throw new InvalidOperationException("No data capsule found for module");
            }

            return relevantCapsule;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:493-504
        // Original: def lookup_dlg_capsule(self) -> ModuleFullOverridePiece | ModuleDLGPiece:
        public ModulePieceResource LookupDlgCapsule()
        {
            ModulePieceResource relevantCapsule = null;
            if (_dotMod)
            {
                if (_capsules.TryGetValue(KModuleType.MOD.ToString(), out ModulePieceResource modCapsule))
                {
                    relevantCapsule = modCapsule;
                }
                else if (_capsules.TryGetValue(KModuleType.K2_DLG.ToString(), out ModulePieceResource dlgCapsule))
                {
                    relevantCapsule = dlgCapsule;
                }
            }
            else
            {
                _capsules.TryGetValue(KModuleType.K2_DLG.ToString(), out relevantCapsule);
            }

            if (relevantCapsule == null)
            {
                throw new InvalidOperationException("No DLG capsule found for module");
            }

            return relevantCapsule;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:506-514
        // Original: def module_id(self) -> ResRef | None:
        public ResRef ModuleId()
        {
            if (_cachedModId != null)
            {
                return _cachedModId;
            }

            ModulePieceResource dataCapsule = LookupMainCapsule();
            ResRef foundId = null;
            if (dataCapsule is ModuleLinkPiece linkPiece)
            {
                foundId = linkPiece.ModuleId();
            }
            else if (dataCapsule is ModuleFullOverridePiece overridePiece)
            {
                foundId = overridePiece.ModuleId();
            }

            Console.WriteLine($"Found module id '{foundId}' for module '{dataCapsule.Filename()}'");
            _cachedModId = foundId;
            return foundId;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:575-582
        // Original: def capsules(self) -> list[ModulePieceResource]:
        /// <summary>
        /// Returns a copy of the capsules used by the module.
        /// </summary>
        /// <returns>A list of linked capsules.</returns>
        public List<ModulePieceResource> Capsules()
        {
            var capsules = new List<ModulePieceResource>();
            foreach (var capsule in _capsules.Values)
            {
                if (capsule != null)
                {
                    capsules.Add(capsule);
                }
            }
            return capsules;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:892-909
        // Original: def resource(self, resname: str, restype: ResourceType) -> ModuleResource | None:
        /// <summary>
        /// Returns the resource with the given name and type from the module.
        /// </summary>
        [CanBeNull]
        public ModuleResource Resource(string resname, ResourceType restype)
        {
            var ident = new ResourceIdentifier(resname, restype);
            _resources.TryGetValue(ident, out ModuleResource resource);
            return resource;
        }

        /// <summary>
        /// Creates a type-specific ModuleResource instance based on ResourceType.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:884
        /// Original: module_resource = ModuleResource(resname, restype, self._installation, self._root)
        /// Uses reflection to create ModuleResource<T> with the appropriate type parameter.
        /// </summary>
        private ModuleResource CreateModuleResource(string resname, ResourceType restype)
        {
            // Map ResourceType to C# type for strongly-typed ModuleResource<T>
            // For GFF-based resources, we use the specific GFF type (ARE, GIT, UTC, etc.)
            // For non-GFF resources, we use object as a fallback
            Type resourceType = GetResourceTypeForRestype(restype);

            // Create ModuleResource<T> using reflection
            Type genericModuleResourceType = typeof(ModuleResource<>);
            Type specificModuleResourceType = genericModuleResourceType.MakeGenericType(resourceType);

            // Create instance with constructor: ModuleResource(string, ResourceType, Installation, string)
            object instance = Activator.CreateInstance(
                specificModuleResourceType,
                resname,
                restype,
                _installation,
                _root
            );

            return (ModuleResource)instance;
        }

        /// <summary>
        /// Maps ResourceType to the corresponding C# type for ModuleResource<T>.
        /// </summary>
        private Type GetResourceTypeForRestype(ResourceType restype)
        {
            // Map known resource types to their C# types
            // GFF-based resources use their specific types
            if (restype == ResourceType.ARE)
            {
                return typeof(ARE);
            }
            if (restype == ResourceType.GIT)
            {
                return typeof(Resource.Generics.GIT);
            }
            if (restype == ResourceType.IFO)
            {
                return typeof(IFO);
            }
            if (restype == ResourceType.UTC)
            {
                return typeof(Andastra.Parsing.Resource.Generics.UTC.UTC);
            }
            if (restype == ResourceType.UTD)
            {
                return typeof(UTD);
            }
            if (restype == ResourceType.UTP)
            {
                return typeof(UTP);
            }
            if (restype == ResourceType.UTS)
            {
                return typeof(UTS);
            }
            if (restype == ResourceType.UTI)
            {
                return typeof(Andastra.Parsing.Resource.Generics.UTI.UTI);
            }
            if (restype == ResourceType.UTE)
            {
                return typeof(UTE);
            }
            if (restype == ResourceType.UTT)
            {
                return typeof(UTT);
            }
            if (restype == ResourceType.UTW)
            {
                return typeof(UTW);
            }
            if (restype == ResourceType.UTM)
            {
                return typeof(Andastra.Parsing.Resource.Generics.UTM.UTM);
            }
            if (restype == ResourceType.LYT)
            {
                return typeof(Andastra.Parsing.Resource.Formats.LYT.LYT);
            }
            if (restype == ResourceType.VIS)
            {
                return typeof(Formats.VIS.VIS);
            }
            if (restype == ResourceType.PTH)
            {
                return typeof(PTH);
            }
            if (restype == ResourceType.MDL)
            {
                return typeof(MDL);
            }
            if (restype == ResourceType.TPC)
            {
                return typeof(Formats.TPC.TPC);
            }
            if (restype == ResourceType.NCS)
            {
                return typeof(Formats.NCS.NCS);
            }
            if (restype == ResourceType.DLG)
            {
                return typeof(DLG);
            }

            // Fallback to object for unknown or unsupported resource types
            return typeof(object);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:852-887
        // Original: def add_locations(self, resname: str, restype: ResourceType, locations: Iterable[Path]) -> ModuleResource:
        /// <summary>
        /// Creates or extends a ModuleResource keyed by the resname/restype with additional locations.
        /// This is how Module.resources dict gets filled.
        /// </summary>
        public ModuleResource AddLocations(string resname, ResourceType restype, IEnumerable<string> locations)
        {
            if (locations == null)
            {
                locations = new List<string>();
            }
            var locationsList = locations.ToList();
            if (locationsList.Count == 0 && !(resname == "dirt" && restype == ResourceType.TPC))
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("No locations found for '{0}.{1}' which are intended to add to module '{2}'", resname, restype, _root));
            }

            ResourceIdentifier ident = new ResourceIdentifier(resname, restype);
            if (!_resources.TryGetValue(ident, out ModuleResource moduleResource))
            {
                // Create type-specific ModuleResource<T> based on ResourceType
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:884
                // Original: module_resource = ModuleResource(resname, restype, self._installation, self._root)
                moduleResource = CreateModuleResource(resname, restype);
                _resources[ident] = moduleResource;
            }

            moduleResource.AddLocations(locationsList);
            return moduleResource;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:911-920
        // Original: def layout(self) -> ModuleResource[LYT] | None:
        /// <summary>
        /// Returns the LYT layout resource with a matching ID if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Layout()
        {
            return Resource(ModuleId()?.ToString(), ResourceType.LYT);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:975-990
        // Original: def git(self) -> ModuleResource[GIT] | None:
        /// <summary>
        /// Returns the git resource with matching id if found.
        /// </summary>
        [CanBeNull]
        public ModuleResource Git()
        {
            return Resource(ModuleId()?.ToString(), ResourceType.GIT);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1058-1075
        // Original: def creature(self, resname: str) -> ModuleResource[UTC] | None:
        /// <summary>
        /// Returns a UTC resource by name if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Creature(string resname)
        {
            return Resource(resname, ResourceType.UTC);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1105-1122
        // Original: def placeable(self, resname: str) -> ModuleResource[UTP] | None:
        /// <summary>
        /// Check if a placeable UTP resource with the given resname exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Placeable(string resname)
        {
            return Resource(resname, ResourceType.UTP);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1152-1169
        // Original: def door(self, resname: str) -> ModuleResource[UTD] | None:
        /// <summary>
        /// Returns a UTD resource matching the provided resname from this module.
        /// </summary>
        [CanBeNull]
        public ModuleResource Door(string resname)
        {
            return Resource(resname, ResourceType.UTD);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1033-1056
        // Original: def info(self) -> ModuleResource[IFO] | None:
        /// <summary>
        /// Returns the ModuleResource with type IFO if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Info()
        {
            return Resource("module", ResourceType.IFO);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:951-973
        // Original: def are(self) -> ModuleResource[ARE] | None:
        /// <summary>
        /// Returns the ARE resource with the given ID if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Are()
        {
            ResRef modId = ModuleId();
            return modId != null ? Resource(modId.ToString(), ResourceType.ARE) : null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1007-1028
        // Original: def pth(self) -> ModuleResource[PTH] | None:
        /// <summary>
        /// Finds the PTH resource with matching ID.
        /// </summary>
        [CanBeNull]
        public ModuleResource Pth()
        {
            ResRef modId = ModuleId();
            return modId != null ? Resource(modId.ToString(), ResourceType.PTH) : null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1560-1577
        // Original: def sound(self, resname: str) -> ModuleResource[UTS] | None:
        /// <summary>
        /// Returns a UTS resource by name if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Sound(string resname)
        {
            return Resource(resname, ResourceType.UTS);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:933-949
        // Original: def vis(self) -> ModuleResource[VIS] | None:
        /// <summary>
        /// Finds the VIS resource with matching ID.
        /// </summary>
        [CanBeNull]
        public ModuleResource Vis()
        {
            ResRef modId = ModuleId();
            return modId != null ? Resource(modId.ToString(), ResourceType.VIS) : null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1225-1245
        // Original: def items(self) -> list[ModuleResource[UTI]]:
        /// <summary>
        /// Returns a list of UTI resources for this module.
        /// </summary>
        /// <remarks>
        /// NOTE: Python implementation has a bug - it checks for ResourceType.UTD instead of ResourceType.UTI.
        /// Matching Python exactly as per 1:1 porting requirements.
        /// </remarks>
        public List<ModuleResource> Items()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.UTD).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1247-1271
        // Original: def encounter(self, resname: str) -> ModuleResource[UTE] | None:
        /// <summary>
        /// Find UTE resource by the specified resname.
        /// </summary>
        [CanBeNull]
        public ModuleResource Encounter(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            return _resources.Values.FirstOrDefault(resource =>
                resource.GetResType() == ResourceType.UTE &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1273-1293
        // Original: def encounters(self) -> list[ModuleResource[UTE]]:
        /// <summary>
        /// Returns a list of UTE resources for this module.
        /// </summary>
        public List<ModuleResource> Encounters()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.UTE).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1295-1317
        // Original: def store(self, resname: str) -> ModuleResource[UTM] | None:
        /// <summary>
        /// Looks up a material (UTM) resource by the specified resname from this module and returns the resource data.
        /// </summary>
        [CanBeNull]
        public ModuleResource Store(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            return _resources.Values.FirstOrDefault(resource =>
                resource.GetResType() == ResourceType.UTM &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1319-1323
        // Original: def stores(self) -> list[ModuleResource[UTM]]:
        /// <summary>
        /// Returns a list of material (UTM) resources for this module.
        /// </summary>
        public List<ModuleResource> Stores()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.UTM).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1325-1350
        // Original: def trigger(self, resname: str) -> ModuleResource[UTT] | None:
        /// <summary>
        /// Returns a trigger (UTT) resource by the specified resname if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Trigger(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            return _resources.Values.FirstOrDefault(resource =>
                resource.GetResType() == ResourceType.UTT &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1352-1372
        // Original: def triggers(self) -> list[ModuleResource[UTT]]:
        /// <summary>
        /// Returns a list of UTT resources for this module.
        /// </summary>
        public List<ModuleResource> Triggers()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.UTT).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1374-1399
        // Original: def waypoint(self, resname: str) -> ModuleResource[UTW] | None:
        /// <summary>
        /// Returns the UTW resource with the given name if it exists.
        /// </summary>
        [CanBeNull]
        public ModuleResource Waypoint(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            return _resources.Values.FirstOrDefault(resource =>
                resource.GetResType() == ResourceType.UTW &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1401-1417
        // Original: def waypoints(self) -> list[ModuleResource[UTW]]:
        /// <summary>
        /// Returns list of UTW resources from resources dict.
        /// </summary>
        public List<ModuleResource> Waypoints()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.UTW).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1419-1443
        // Original: def model(self, resname: str) -> ModuleResource[MDL] | None:
        /// <summary>
        /// Returns a ModuleResource object for the given resource name if it exists in this module.
        /// </summary>
        [CanBeNull]
        public ModuleResource Model(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            return _resources.Values.FirstOrDefault(resource =>
                resource.GetResType() == ResourceType.MDL &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1445-1468
        // Original: def model_ext(self, resname: str) -> ModuleResource | None:
        /// <summary>
        /// Finds a MDX module resource by name from this module.
        /// </summary>
        [CanBeNull]
        public ModuleResource ModelExt(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            return _resources.Values.FirstOrDefault(resource =>
                resource.GetResType() == ResourceType.MDX &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1470-1488
        // Original: def models(self) -> list[ModuleResource[MDL]]:
        /// <summary>
        /// Returns a list of MDL model resources.
        /// </summary>
        public List<ModuleResource> Models()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.MDL).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1490-1508
        // Original: def model_exts(self) -> list[ModuleResource]:
        /// <summary>
        /// Returns a list of MDX model resources.
        /// </summary>
        public List<ModuleResource> ModelExts()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.MDX).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1510-1536
        // Original: def texture(self, resname: str) -> ModuleResource[TPC] | None:
        /// <summary>
        /// Looks up a texture resource by resname from this module.
        /// </summary>
        [CanBeNull]
        public ModuleResource Texture(string resname)
        {
            string lowerResname = resname.ToLowerInvariant();
            HashSet<ResourceType> textureTypes = new HashSet<ResourceType> { ResourceType.TPC, ResourceType.TGA };
            return _resources.Values.FirstOrDefault(resource =>
                resource.IsActive() &&
                textureTypes.Contains(resource.GetResType()) &&
                resource.GetResName().ToLowerInvariant() == lowerResname);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1538-1558
        // Original: def textures(self) -> list[ModuleResource[MDL]]:
        /// <summary>
        /// Generates a list of texture resources from this module.
        /// </summary>
        public List<ModuleResource> Textures()
        {
            HashSet<ResourceType> textureTypes = new HashSet<ResourceType> { ResourceType.TPC, ResourceType.TGA };
            return _resources.Values.Where(resource =>
                resource.IsActive() &&
                textureTypes.Contains(resource.GetResType())).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1586-1606
        // Original: def sounds(self) -> list[ModuleResource[UTS]]:
        /// <summary>
        /// Returns a list of UTS resources.
        /// </summary>
        public List<ModuleResource> Sounds()
        {
            return _resources.Values.Where(resource => resource.GetResType() == ResourceType.UTS).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1608-1706
        // Original: def loadscreen(self) -> FileResource | None:
        /// <summary>
        /// Returns a FileResource object representing the loadscreen texture for this module.
        /// </summary>
        [CanBeNull]
        public FileResource Loadscreen()
        {
            // Get the ARE resource
            ModuleResource areResource = Are();
            if (areResource == null)
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("Module '{0}' has no ARE resource, cannot determine loadscreen", _root));
                return null;
            }

            // Read the ARE to get LoadScreenID
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1645-1649
            // Original: are_data = are_resource.resource()
            object areData = areResource.Resource();
            if (areData == null)
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("Failed to read ARE resource for module '{0}'", _root));
                return null;
            }

            // Cast to ARE type
            ARE are = areData as ARE;
            if (are == null)
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("ARE resource for module '{0}' is not of type ARE", _root));
                return null;
            }

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1651-1654
            // Original: loadscreen_id = are_data.loadscreen_id
            int loadscreenId = are.LoadScreenID;
            if (loadscreenId == 0)
            {
                new Andastra.Parsing.Logger.RobustLogger().Debug(string.Format("Module '{0}' has LoadScreenID=0, no loadscreen specified", _root));
                return null;
            }

            // Load loadscreens.2da from installation
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1656-1664
            // Original: loadscreens_result = self._installation.resource("loadscreens", ResourceType.TwoDA, [SearchLocation.OVERRIDE, SearchLocation.CHITIN])
            Installation.ResourceResult loadscreensResult = _installation.Resources.LookupResource(
                "loadscreens",
                ResourceType.TwoDA,
                new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN }
            );
            if (loadscreensResult == null || loadscreensResult.Data == null)
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning("loadscreens.2da not found in installation");
                return null;
            }

            // Parse 2DA file
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1666
            // Original: loadscreens_2da = read_2da(loadscreens_result.data)
            Formats.TwoDA.TwoDA loadscreens2da = new Formats.TwoDA.TwoDABinaryReader(loadscreensResult.Data).Load();

            // Get the bmpresref from loadscreens.2da using LoadScreenID as row index
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1668-1677
            // Original: loadscreen_row = loadscreens_2da.get_row(loadscreen_id)
            string bmpresref = null;
            try
            {
                Formats.TwoDA.TwoDARow loadscreenRow = loadscreens2da.GetRow(loadscreenId);
                bmpresref = loadscreenRow.GetString("bmpresref");
                if (string.IsNullOrWhiteSpace(bmpresref) || bmpresref == "****")
                {
                    new Andastra.Parsing.Logger.RobustLogger().Debug(string.Format("Module '{0}' loadscreen row {1} has no bmpresref", _root, loadscreenId));
                    return null;
                }
            }
            catch (Exception e)
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("Failed to get bmpresref from loadscreens.2da row {0}: {1}", loadscreenId, e.Message));
                return null;
            }

            // Search for the texture (TGA or TPC) using installation.locations()
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1679-1703
            // Original: texture_queries = [ResourceIdentifier(bmpresref, ResourceType.TPC), ResourceIdentifier(bmpresref, ResourceType.TGA)]
            var textureQueries = new List<ResourceIdentifier>
            {
                new ResourceIdentifier(bmpresref, ResourceType.TPC),
                new ResourceIdentifier(bmpresref, ResourceType.TGA)
            };
            SearchLocation[] searchOrder = { SearchLocation.OVERRIDE, SearchLocation.CUSTOM_MODULES, SearchLocation.CHITIN, SearchLocation.TEXTURES_TPA };
            Dictionary<ResourceIdentifier, List<LocationResult>> textureLocations = _installation.Locations(textureQueries, searchOrder);

            // Return the FileResource from the first location found
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1689-1703
            // Original: for query in texture_queries: if query in texture_locations and texture_locations[query]: location = texture_locations[query][0]; return location.as_file_resource()
            foreach (ResourceIdentifier query in textureQueries)
            {
                if (textureLocations.ContainsKey(query) && textureLocations[query].Count > 0)
                {
                    LocationResult location = textureLocations[query][0];
                    FileResource fileResource = location.FileResource;
                    if (fileResource != null)
                    {
                        return fileResource;
                    }
                    // If FileResource wasn't set, create one from the location
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1696-1702
                    // Original: return FileResource(resname=bmpresref, restype=query.restype, size=location.size, offset=location.offset, filepath=location.filepath)
                    return new FileResource(bmpresref, query.ResType, location.Size, location.Offset, location.FilePath);
                }
            }

            new Andastra.Parsing.Logger.RobustLogger().Debug(string.Format("Loadscreen texture '{0}' not found for module '{1}'", bmpresref, _root));
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:584-799
        // Original: def reload_resources(self):
        private void ReloadResources()
        {
            string displayName = (_dotMod ? $"{_root}.mod" : $"{_root}.rim");
            new Andastra.Parsing.Logger.RobustLogger().Info(string.Format("Loading module resources needed for '{0}'", displayName));

            // Get main capsule for searching
            ModulePieceResource mainCapsule = LookupMainCapsule();
            List<ModulePieceResource> capsulesToSearch = new List<ModulePieceResource> { mainCapsule };

            // Define search order for resources (OVERRIDE, CUSTOM_MODULES, CHITIN)
            SearchLocation[] order = ModuleSearchOrder.Order;

            // Get module ID for link resource queries
            ResRef linkResname = ModuleId();
            if (linkResname == null)
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("Module ID is null for module '{0}', cannot load resources", _root));
                return;
            }

            ResourceIdentifier lytQuery = new ResourceIdentifier(linkResname, ResourceType.LYT);
            ResourceIdentifier gitQuery = new ResourceIdentifier(linkResname, ResourceType.GIT);
            ResourceIdentifier visQuery = new ResourceIdentifier(linkResname, ResourceType.VIS);

            // Start in our module resources - needs to happen first so we can determine what resources are part of our module
            foreach (ModulePieceResource capsule in _capsules.Values)
            {
                if (capsule == null)
                    continue;

                foreach (CapsuleResource capsuleResource in capsule.GetResources())
                {
                    FileResource resource = new FileResource(capsuleResource.ResName, capsuleResource.ResType,
                        capsuleResource.Size, capsuleResource.Offset, capsule.Path.ToString());
                    new Andastra.Parsing.Logger.RobustLogger().Info(string.Format("Adding location '{0}' for resource '{1}' from erf/rim '{2}'",
                        capsule.Filename(), resource.Identifier, capsule.PieceInfo.ResIdent()));
                    AddLocations(resource.ResName, resource.ResType, new[] { capsule.Filename() });
                }
            }

            // Any resource referenced by the GIT/LYT/VIS not present in the module files
            // To be looked up elsewhere in the installation
            List<LazyCapsule> lazyCapsules = capsulesToSearch.Select(c => new LazyCapsule(c.Path.ToString())).ToList();
            Dictionary<ResourceIdentifier, List<LocationResult>> mainSearchResults =
                _installation.Locations(new List<ResourceIdentifier> { lytQuery, gitQuery, visQuery }, order, lazyCapsules);

            // Track all resources referenced by GIT/LYT/VIS
            HashSet<ResourceIdentifier> gitSearch = new HashSet<ResourceIdentifier>();
            HashSet<ResourceIdentifier> lytSearch = new HashSet<ResourceIdentifier>();
            HashSet<ResourceIdentifier> visSearch = new HashSet<ResourceIdentifier>();

            // Store references for use in other methods
            _gitSearch = gitSearch;

            // Process each query (GIT, LYT, VIS) in sequence
            ProcessGitLytVisQuery(gitQuery, mainSearchResults, gitSearch, mainCapsule);
            ProcessGitLytVisQuery(lytQuery, mainSearchResults, lytSearch, mainCapsule);
            ProcessGitLytVisQuery(visQuery, mainSearchResults, visSearch, mainCapsule);

            // From GIT/LYT references, find them in the installation
            HashSet<ResourceIdentifier> allReferences = new HashSet<ResourceIdentifier>();
            allReferences.UnionWith(gitSearch);
            allReferences.UnionWith(lytSearch);
            allReferences.UnionWith(visSearch);

            List<LazyCapsule> lazyCapsules2 = capsulesToSearch.Select(c => new LazyCapsule(c.Path.ToString())).ToList();
            Dictionary<ResourceIdentifier, List<LocationResult>> searchResults =
                _installation.Locations(allReferences.ToList(), order, lazyCapsules2);

            // Add locations for all found resources
            foreach (KeyValuePair<ResourceIdentifier, List<LocationResult>> kv in searchResults)
            {
                List<string> searchResultFilepaths = kv.Value.Select(loc => loc.FilePath).ToList();
                AddLocations(kv.Key.ResName, kv.Key.ResType, searchResultFilepaths);
            }

            // Process core resources and override directories
            ProcessCoreResources(displayName);
            ProcessOverrideResources(displayName);

            // Process texture resources from models
            ProcessModelTextures(displayName);

            // Finally iterate through all resources we may have missed
            ActivateRemainingResources();
        }

        private void ProcessGitLytVisQuery(ResourceIdentifier query, Dictionary<ResourceIdentifier, List<LocationResult>> mainSearchResults,
            HashSet<ResourceIdentifier> searchSet, ModulePieceResource mainCapsule)
        {
            if (!mainSearchResults.TryGetValue(query, out List<LocationResult> locations))
            {
                if (query.ResType == ResourceType.VIS)
                {
                    // VIS is optional
                    return;
                }
                throw new FileNotFoundException($"Required resource '{query}' not found for module", mainCapsule.Filename());
            }

            // Add locations and get the resource wrapper
            ModuleResource resourceWrapper = AddLocations(query.ResName, query.ResType,
                locations.Select(loc => loc.FilePath).ToList());

            if (resourceWrapper == null)
            {
                return;
            }

            // Store original path to restore later
            string originalPath = resourceWrapper.Locations().FirstOrDefault();

            // Check each location for referenced resources
            foreach (string location in resourceWrapper.Locations())
            {
                resourceWrapper.Activate(location);
                object loadedResource = resourceWrapper.Resource();

                // Only GIT/LYT have resource identifiers to collect
                // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:688-690
                // Original: if not isinstance(loaded_resource, VIS): search_set.update(loaded_resource.iter_resource_identifiers())
                if (query.ResType != ResourceType.VIS)
                {
                    if (loadedResource is Resource.Generics.GIT git)
                    {
                        foreach (ResourceIdentifier ident in git.IterResourceIdentifiers())
                        {
                            searchSet.Add(ident);
                        }
                    }
                    else if (loadedResource is Andastra.Parsing.Resource.Formats.LYT.LYT lyt)
                    {
                        foreach (ResourceIdentifier ident in lyt.IterResourceIdentifiers())
                        {
                            searchSet.Add(ident);
                        }
                    }
                }
            }

            if (originalPath != null)
            {
                resourceWrapper.Activate(originalPath);
            }
        }


        private void ProcessModelTextures(string displayName)
        {
            HashSet<string> lookupTextureQueries = new HashSet<string>();
            HashSet<string> lookupLightmapQueries = new HashSet<string>();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:760-763
            // Original: for model in self.models(): ... lookup_texture_queries.update(iterate_textures(model_data))
            foreach (ModuleResource model in Models())
            {
                new Andastra.Parsing.Logger.RobustLogger().Info(string.Format("Finding textures/lightmaps for model '{0}'...", model.GetIdentifier()));
                try
                {
                    byte[] modelData = model.Resource() as byte[];
                    if (modelData == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("Missing model '{0}', needed by module '{1}'", model.GetIdentifier(), displayName));
                        continue;
                    }

                    lookupTextureQueries.UnionWith(Andastra.Parsing.Tools.ModelTools.IterateTextures(modelData));
                    lookupLightmapQueries.UnionWith(Andastra.Parsing.Tools.ModelTools.IterateLightmaps(modelData));
                }
                catch (Exception ex)
                {
                    new Andastra.Parsing.Logger.RobustLogger().Warning(string.Format("Suppressed exception while getting model data '{0}': {1}", model.GetIdentifier(), ex.Message));
                }
            }

            // Process texture queries
            HashSet<string> texlmQueries = new HashSet<string>();
            texlmQueries.UnionWith(lookupTextureQueries);
            texlmQueries.UnionWith(lookupLightmapQueries);

            List<ResourceIdentifier> textureQueries = new List<ResourceIdentifier>();
            foreach (string texture in texlmQueries)
            {
                textureQueries.Add(new ResourceIdentifier(texture, ResourceType.TPC));
                textureQueries.Add(new ResourceIdentifier(texture, ResourceType.TGA));
            }

            Dictionary<ResourceIdentifier, List<LocationResult>> textureSearch = _installation.Locations(
                textureQueries,
                new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN, SearchLocation.TEXTURES_TPA }
            );

            foreach (KeyValuePair<ResourceIdentifier, List<LocationResult>> kv in textureSearch)
            {
                if (kv.Value.Count == 0)
                    continue;

                List<string> locationPaths = kv.Value.Select(loc => loc.FilePath).ToList();
                string pathsStr = kv.Value.Count <= 3
                    ? string.Join(", ", locationPaths)
                    : string.Join(", ", locationPaths.Take(3)) + $", ... and {locationPaths.Count - 3} more";

                new Andastra.Parsing.Logger.RobustLogger().Debug(string.Format("Adding {0} texture location(s) for '{1}' to '{2}': {3}",
                    kv.Value.Count, kv.Key, displayName, pathsStr));

                AddLocations(kv.Key.ResName, kv.Key.ResType, kv.Value.Select(loc => loc.FilePath));
            }
        }

        private void ActivateRemainingResources()
        {
            foreach (KeyValuePair<ResourceIdentifier, ModuleResource> kv in _resources)
            {
                if (kv.Value.IsActive())
                    continue;

                // Skip TPC resources if the TGA equivalent resource is already found and activated
                if (kv.Key.ResType == ResourceType.TPC)
                {
                    ResourceIdentifier tgaIdent = new ResourceIdentifier(kv.Key.ResName, ResourceType.TGA);
                    if (_resources.TryGetValue(tgaIdent, out ModuleResource tgaResource) && tgaResource.IsActive())
                        continue;
                }

                // Skip TGA resources if the TPC equivalent resource is already found and activated
                if (kv.Key.ResType == ResourceType.TGA)
                {
                    ResourceIdentifier tpcIdent = new ResourceIdentifier(kv.Key.ResName, ResourceType.TPC);
                    if (_resources.TryGetValue(tpcIdent, out ModuleResource tpcResource) && tpcResource.IsActive())
                        continue;
                }

                kv.Value.Activate();
            }
        }
    }

    /// <summary>
    /// Base class for module resources with multiple possible locations.
    /// </summary>
    public abstract class ModuleResource
    {
        public string ResName { get; protected set; }
        public ResourceType ResType { get; protected set; }
        public ResourceIdentifier Identifier { get; protected set; }
        public string ModuleRoot { get; protected set; }

        protected ModuleResource(string resname, ResourceType restype, string moduleRoot)
        {
            ResName = resname;
            ResType = restype;
            Identifier = new ResourceIdentifier(resname, restype);
            ModuleRoot = moduleRoot;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1787-1794
        // Original: def resname(self) -> str:
        public virtual string GetResName()
        {
            return ResName;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1796-1803
        // Original: def restype(self) -> ResourceType:
        public virtual ResourceType GetResType()
        {
            return ResType;
        }

        public abstract void AddLocations(IEnumerable<string> filepaths);
        public abstract List<string> Locations();
        public abstract string Activate(string filepath = null);
        public abstract bool IsActive();
        public abstract object Resource();
        public abstract string Filename();
        public abstract ResourceIdentifier GetIdentifier();
        public abstract byte[] Data();
        public abstract void Save();
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1709-2131
    // Original: class ModuleResource(Generic[T]):
    /// <summary>
    /// Represents a single resource within a module with multiple possible locations.
    /// ModuleResource manages a resource that may exist in multiple locations (override,
    /// module archives, chitin). It tracks all locations and allows activation of a
    /// specific location, with lazy loading of the actual resource object.
    /// </summary>
    public sealed class ModuleResource<T> : ModuleResource
    {
        // private readonly Installation.Installation _installation;
        private string _active;
        private T _resourceObj;
        private bool _resourceLoadAttempted; // Track if we've attempted to load (for caching when conversion not implemented)
        private readonly List<string> _locations = new List<string>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1756-1770
        // Original: def __init__(self, resname: str, restype: ResourceType, installation: Installation, module_root: str | None = None):
        public ModuleResource(string resname, ResourceType restype, Installation.Installation installation, string moduleRoot = null)
            : base(resname, restype, moduleRoot)
        {
            if (resname == null)
            {
                throw new ArgumentNullException(nameof(resname));
            }
            if (restype == null)
            {
                throw new ArgumentNullException(nameof(restype));
            }
            if (installation == null)
            {
                throw new ArgumentNullException(nameof(installation));
            }

            _installation = installation;
            _active = null;
            _resourceObj = default(T);
            _resourceLoadAttempted = false;
        }

        public override string ToString()
        {
            return $"{GetType().Name}(resname={ResName} restype={ResType} installation={_installation})";
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is ResourceIdentifier identifier)
            {
                return Identifier == identifier;
            }

            if (obj is ModuleResource<T> other)
            {
                return Identifier == other.Identifier;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Identifier.GetHashCode();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1787-1794
        // Original: def resname(self) -> str:
        public override string GetResName()
        {
            return ResName;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1796-1803
        // Original: def restype(self) -> ResourceType:
        public override ResourceType GetResType()
        {
            return ResType;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1805-1806
        // Original: def filename(self) -> str:
        public override string Filename()
        {
            return base.Identifier.ToString();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1808-1809
        // Original: def identifier(self) -> ResourceIdentifier:
        public override ResourceIdentifier GetIdentifier()
        {
            return Identifier;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1968-1977
        // Original: def add_locations(self, filepaths: Iterable[Path]):
        public override void AddLocations(IEnumerable<string> filepaths)
        {
            if (filepaths == null)
            {
                return;
            }

            foreach (string filepath in filepaths)
            {
                if (!string.IsNullOrEmpty(filepath) && !_locations.Contains(filepath))
                {
                    _locations.Add(filepath);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1979-1980
        // Original: def locations(self) -> list[Path]:
        public override List<string> Locations()
        {
            return new List<string>(_locations);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1982-2014
        // Original: def activate(self, filepath: os.PathLike | str | None = None) -> Path | None:
        public override string Activate(string filepath = null)
        {
            _resourceObj = default(T);
            if (filepath == null)
            {
                _active = _locations.Count > 0 ? _locations[0] : null;
            }
            else
            {
                if (!_locations.Contains(filepath))
                {
                    _locations.Add(filepath);
                }
                _active = filepath;
            }

            if (_active == null)
            {
                string moduleInfo = !string.IsNullOrEmpty(ModuleRoot) ? $" in module '{ModuleRoot}'" : "";
                string installationPath = _installation.Path;
                string locationsInfo = _locations.Count > 0
                    ? $"Searched locations: {string.Join(", ", _locations)}."
                    : "No locations were added to this resource.";
                new Andastra.Parsing.Logger.RobustLogger().Warning(
                    $"Cannot activate module resource '{Identifier}'{moduleInfo}: No locations found. " +
                    $"Installation: {installationPath}. {locationsInfo}"
                );
            }

            return _active;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:2041-2042
        // Original: def isActive(self) -> bool:
        public override bool IsActive()
        {
            return !string.IsNullOrEmpty(_active);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:2025-2039
        // Original: def active(self) -> Path | None:
        public string Active()
        {
            if (_active == null)
            {
                if (_locations.Count == 0)
                {
                    new Andastra.Parsing.Logger.RobustLogger().Warning($"No resource found for '{Identifier}'");
                    return null;
                }
                Activate();
            }
            return _active;
        }


        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:2016-2018
        // Original: def unload(self):
        public void Unload()
        {
            _resourceObj = default(T);
            _resourceLoadAttempted = false;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:2020-2023
        // Original: def reload(self):
        public void Reload()
        {
            _resourceObj = default(T);
            _resourceLoadAttempted = false;
            Resource(); // Trigger reload
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1840-1874
        // Original: def data(self) -> bytes | None:
        public override byte[] Data()
        {
            string fileName = $"{ResName}.{ResType.Extension}";
            string activePath = Active();
            if (activePath == null)
            {
                return null;
            }

            // Check if capsule file
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1856-1860
            // Original: if is_capsule_file(active_path): data = Capsule(active_path).resource(self._resname, self._restype); if data is None: RobustLogger().error(...); return data
            if (FileHelpers.IsCapsuleFile(activePath))
            {
                var capsule = new Capsule(activePath);
                byte[] data = capsule.GetResource(ResName, ResType);
                if (data == null)
                {
                    new Andastra.Parsing.Logger.RobustLogger().Error($"Resource '{fileName}' not found in '{activePath}'");
                }
                return data;
            }

            // Check if BIF file
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1862-1872
            // Original: if is_bif_file(active_path): resource = self._installation.resource(...); if resource is None: RobustLogger().error(...); return resource.data
            if (FileHelpers.IsBifFile(activePath))
            {
                var resource = _installation.Resource(ResName, ResType, new[] { SearchLocation.CHITIN });
                if (resource == null)
                {
                    string msg = $"Resource '{fileName}' not found in BIF '{activePath}' somehow?";
                    new Andastra.Parsing.Logger.RobustLogger().Error(msg);
                    return null;
                }
                return resource.Data;
            }

            // Regular file
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1874
            // Original: return active_path.read_bytes()
            if (File.Exists(activePath))
            {
                return File.ReadAllBytes(activePath);
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1876-1937
        // Original: def resource(self) -> T | None:
        public override object Resource()
        {
            if (!_resourceLoadAttempted)
            {
                _resourceLoadAttempted = true;
                byte[] data = Data();
                if (data == null)
                {
                    _resourceObj = default(T);
                    return default(T);
                }

                // Load resource based on type
                object loaded = null;
                if (ResType == ResourceType.ARE)
                {
                    loaded = ResourceAutoHelpers.ReadAre(data);
                }
                else if (ResType == ResourceType.GIT)
                {
                    loaded = ResourceAutoHelpers.ReadGit(data);
                }
                else if (ResType == ResourceType.IFO)
                {
                    loaded = ResourceAutoHelpers.ReadIfo(data);
                }
                else if (ResType == ResourceType.UTC)
                {
                    loaded = ResourceAutoHelpers.ReadUtc(data);
                }
                else if (ResType == ResourceType.PTH)
                {
                    loaded = ResourceAutoHelpers.ReadPth(data);
                }
                else if (ResType == ResourceType.UTD)
                {
                    loaded = ResourceAutoHelpers.ReadUtd(data);
                }
                else if (ResType == ResourceType.UTP)
                {
                    loaded = ResourceAutoHelpers.ReadUtp(data);
                }
                else if (ResType == ResourceType.UTS)
                {
                    loaded = ResourceAutoHelpers.ReadUts(data);
                }
                else if (ResType == ResourceType.LYT)
                {
                    loaded = Andastra.Parsing.Resource.Formats.LYT.LYTAuto.ReadLyt(data);
                }
                else if (ResType == ResourceType.VIS)
                {
                    loaded = Formats.VIS.VISAuto.ReadVis(data);
                }
                else if (ResType == ResourceType.NCS)
                {
                    // NCS is just raw bytes
                    loaded = data;
                }
                else
                {
                    // For other types, try to load as GFF if it's a GFF-based resource
                    if (ResType.IsGff())
                    {
                        try
                        {
                            var reader = new Formats.GFF.GFFBinaryReader(data);
                            loaded = reader.Load();
                        }
                        catch
                        {
                            loaded = null;
                        }
                    }
                }

                if (loaded != null && loaded is T)
                {
                    _resourceObj = (T)loaded;
                }
                else
                {
                    _resourceObj = default(T);
                }
            }

            return _resourceObj;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:2183-2254
        // Original: def save(self):
        /// <summary>
        /// Saves the resource to the active file.
        /// </summary>
        public override void Save()
        {
            string activePath = Active();
            if (string.IsNullOrEmpty(activePath))
            {
                activePath = CreateAnewInOverride();
            }

            if (FileHelpers.IsBifFile(activePath))
            {
                throw new ArgumentException("Cannot save file to BIF.");
            }

            // Get resource data as bytes
            byte[] resourceData = ResourceAuto.SaveResource(Resource(), ResType);
            if (resourceData == null)
            {
                throw new InvalidOperationException($"No conversion available for resource type {ResType}");
            }

            if (FileHelpers.IsAnyErfTypeFile(activePath))
            {
                // Handle ERF files (ERF, MOD, SAV, HAK)
                var erf = ERFAuto.ReadErf(activePath);
                ResourceType fileFormat = FileHelpers.IsModFile(activePath) ? ResourceType.MOD :
                    FileHelpers.IsSavFile(activePath) ? ResourceType.SAV :
                    ResourceType.ERF;
                erf.SetData(ResName, ResType, resourceData);
                ERFAuto.WriteErf(erf, activePath, fileFormat);
            }
            else if (FileHelpers.IsRimFile(activePath))
            {
                // Handle RIM files
                var rim = RIMAuto.ReadRim(activePath);
                rim.SetData(ResName, ResType, resourceData);
                RIMAuto.WriteRim(rim, activePath, ResourceType.RIM);
            }
            else
            {
                // Regular file - write directly
                File.WriteAllBytes(activePath, resourceData);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:2256-2269
        // Original: def _create_anew_in_override(self) -> Path:
        /// <summary>
        /// Creates a new file in the override folder if no active path exists.
        /// </summary>
        private string CreateAnewInOverride()
        {
            byte[] resData = ResourceAuto.SaveResource(Resource(), ResType);
            if (resData == null)
            {
                string overridePath = Path.Combine(_installation.OverridePath(), Filename());
                throw new FileNotFoundException($"Cannot create resource '{GetIdentifier()}' in override folder", overridePath);
            }

            new Andastra.Parsing.Logger.RobustLogger().Warning($"Saving ModuleResource '{GetIdentifier()}' to the Override folder as it does not have any other paths available...");
            string result = Path.Combine(_installation.OverridePath(), Filename());
            File.WriteAllBytes(result, resData);
            Activate(result);
            return result;
        }
    }
}
