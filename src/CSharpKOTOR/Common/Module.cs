using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Formats.Capsule;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Logger;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Tools;
using JetBrains.Annotations;

namespace CSharpKOTOR.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:70-75
    // Original: SEARCH_ORDER: list[SearchLocation] = [SearchLocation.OVERRIDE, SearchLocation.CUSTOM_MODULES, SearchLocation.CHITIN]
    public static class ModuleSearchOrder
    {
        public static readonly SearchLocation[] Order = { SearchLocation.OVERRIDE, SearchLocation.CUSTOM_MODULES, SearchLocation.CHITIN };
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
        /// </summary>
        MAIN,  // .rim

        /// <summary>
        /// Data module archive containing module resources.
        /// Contains: UTC, UTD, UTE, UTI, UTM, UTP, UTS, UTT, UTW, FAC, LYT, NCS, PTH
        /// File naming: &lt;modulename&gt;_s.rim
        /// Note: In KotOR 2, DLG files are NOT in _s.rim (see K2_DLG)
        /// </summary>
        DATA,  // _s.rim

        /// <summary>
        /// KotOR 2 dialog archive containing dialog files.
        /// Contains: DLG (dialog) files
        /// File naming: &lt;modulename&gt;_dlg.erf
        /// Note: KotOR 1 stores DLG files in _s.rim, KotOR 2 uses separate _dlg.erf
        /// </summary>
        K2_DLG,  // _dlg.erf

        /// <summary>
        /// Community override module archive (single-file format).
        /// Contains: All module resources in a single ERF archive
        /// File naming: &lt;modulename&gt;.mod
        /// Priority: Takes precedence over .rim/_s.rim/_dlg.erf files
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
                case KModuleType.DATA:
                    return "_s.rim";
                case KModuleType.K2_DLG:
                    return "_dlg.erf";
                case KModuleType.MOD:
                    return ".mod";
                default:
                    throw new ArgumentException($"Invalid KModuleType: {type}");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:136-183
        // Original: def contains(self, restype: ResourceType, *, game: Game | None = None) -> bool:
        public static bool Contains(this KModuleType type, ResourceType restype, Game? game = null)
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
                    new Logger.RobustLogger().Warning($"{Filename()} has IFO with incorrect field 'Mod_Area_list' type '{actualFtype}', expected 'List'");
                }
                else
                {
                    GFFList areaList = gffIfo.Root.GetList("Mod_Area_list");
                    if (areaList == null)
                    {
                        new Logger.RobustLogger().Error($"{Filename()}: Module.IFO has a Mod_Area_list field, but it is not a valid list.");
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
                new Logger.RobustLogger().Error($"{Filename()}: Module.IFO does not have an existing Mod_Area_list.");
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
                        new Logger.RobustLogger().Error($"{Filename()}: ARE has a Name field, but it is not a valid LocalizedString.");
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
        private readonly Installation.Installation _installation;
        private readonly string _root;
        private ResRef _cachedModId;
        private readonly Dictionary<string, ModulePieceResource> _capsules = new Dictionary<string, ModulePieceResource>();
        private HashSet<ResourceIdentifier> _gitSearch;

        public Dictionary<ResourceIdentifier, ModuleResource> Resources => _resources;
        public bool DotMod => _dotMod;
        public Installation.Installation Installation => _installation;
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
            string modulesPath = CSharpKOTOR.Installation.Installation.GetModulesPath(_installation.Path);
            if (_dotMod)
            {
                string modFilepath = Path.Combine(modulesPath, _root + KModuleType.MOD.GetExtension());
                if (File.Exists(modFilepath))
                {
                    _capsules[KModuleType.MOD.ToString()] = new ModuleFullOverridePiece(modFilepath);
                }
                else
                {
                    _dotMod = false;
                    string mainFilepath = Path.Combine(modulesPath, _root + KModuleType.MAIN.GetExtension());
                    string dataFilepath = Path.Combine(modulesPath, _root + KModuleType.DATA.GetExtension());
                    _capsules[KModuleType.MAIN.ToString()] = new ModuleLinkPiece(mainFilepath);
                    _capsules[KModuleType.DATA.ToString()] = new ModuleDataPiece(dataFilepath);
                    if (_installation.Game.IsK2())
                    {
                        string dlgFilepath = Path.Combine(modulesPath, _root + KModuleType.K2_DLG.GetExtension());
                        _capsules[KModuleType.K2_DLG.ToString()] = new ModuleDLGPiece(dlgFilepath);
                    }
                }
            }
            else
            {
                string mainFilepath = Path.Combine(modulesPath, _root + KModuleType.MAIN.GetExtension());
                string dataFilepath = Path.Combine(modulesPath, _root + KModuleType.DATA.GetExtension());
                _capsules[KModuleType.MAIN.ToString()] = new ModuleLinkPiece(mainFilepath);
                _capsules[KModuleType.DATA.ToString()] = new ModuleDataPiece(dataFilepath);
                if (_installation.Game.IsK2())
                {
                    string dlgFilepath = Path.Combine(modulesPath, _root + KModuleType.K2_DLG.GetExtension());
                    _capsules[KModuleType.K2_DLG.ToString()] = new ModuleDLGPiece(dlgFilepath);
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
                _capsules.TryGetValue(KModuleType.MAIN.ToString(), out relevantCapsule);
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
                new RobustLogger().Warning(string.Format("No locations found for '{0}.{1}' which are intended to add to module '{2}'", resname, restype, _root));
            }

            ResourceIdentifier ident = new ResourceIdentifier(resname, restype);
            if (!_resources.TryGetValue(ident, out ModuleResource moduleResource))
            {
                // Create a new ModuleResource - for now, we'll use a generic approach
                // In a full implementation, this would need to create ModuleResource<T> with the correct T type
                // For now, we'll create a basic ModuleResource<object> as a placeholder
                // TODO: Implement proper type-specific ModuleResource creation based on restype
                var genericResource = new ModuleResource<object>(resname, restype, _installation, _root);
                moduleResource = genericResource;
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1470-1488
        // Original: def models(self) -> list[ModuleResource[MDL]]:
        /// <summary>
        /// Returns a list of MDL model resources.
        /// </summary>
        /// <remarks>
        /// NOTE: This is a placeholder implementation. The full implementation requires filtering by ResourceType.MDL
        /// and proper type casting to ModuleResource&lt;MDL&gt;.
        /// </remarks>
        public List<object> Models()
        {
            // TODO: Filter resources by ResourceType.MDL and return as List&lt;ModuleResource&lt;MDL&gt;&gt;
            // This requires proper type handling for the generic ModuleResource&lt;T&gt;
            return new List<object>();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:584-799
        // Original: def reload_resources(self):
        private void ReloadResources()
        {
            string displayName = (_dotMod ? $"{_root}.mod" : $"{_root}.rim");
            new RobustLogger().Info(string.Format("Loading module resources needed for '{0}'", displayName));

            // Get main capsule for searching
            ModulePieceResource mainCapsule = LookupMainCapsule();
            List<ModulePieceResource> capsulesToSearch = new List<ModulePieceResource> { mainCapsule };

            // Define search order for resources (OVERRIDE, CUSTOM_MODULES, CHITIN)
            SearchLocation[] order = ModuleSearchOrder.Order;

            // Get module ID for link resource queries
            ResRef linkResname = ModuleId();
            if (linkResname == null)
            {
                new RobustLogger().Warning(string.Format("Module ID is null for module '{0}', cannot load resources", _root));
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
                    new RobustLogger().Info(string.Format("Adding location '{0}' for resource '{1}' from erf/rim '{2}'",
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
                if (query.ResType != ResourceType.VIS)
                {
                    // TODO: Implement iter_resource_identifiers() on loaded resource
                    // This requires the actual GIT/LYT classes to have this method
                    // For now, we'll skip this part
                }
            }

            if (originalPath != null)
            {
                resourceWrapper.Activate(originalPath);
            }
        }

        private void ProcessCoreResources(string displayName)
        {
            foreach (FileResource resource in _installation.CoreResources())
            {
                if (_resources.ContainsKey(resource.Identifier) || _gitSearch.Contains(resource.Identifier))
                {
                    new RobustLogger().Info(string.Format("Found chitin/core location '{0}' for resource '{1}' for module '{2}'",
                        resource.FilePath, resource.Identifier, displayName));
                    AddLocations(resource.ResName, resource.ResType, new[] { resource.FilePath });
                }
            }
        }

        private void ProcessOverrideResources(string displayName)
        {
            foreach (string directory in _installation.OverrideList())
            {
                foreach (FileResource resource in _installation.OverrideResources(directory))
                {
                    if (!_resources.ContainsKey(resource.Identifier) && !_gitSearch.Contains(resource.Identifier))
                    {
                        continue;
                    }
                    new RobustLogger().Info(string.Format("Found override location '{0}' for module '{1}'", resource.FilePath, displayName));
                    AddLocations(resource.ResName, resource.ResType, new[] { resource.FilePath });
                }
            }
        }

        private void ProcessModelTextures(string displayName)
        {
            HashSet<string> lookupTextureQueries = new HashSet<string>();
            HashSet<string> lookupLightmapQueries = new HashSet<string>();

            // TODO: Implement models() method and iterate through models
            // For now, this is a placeholder
            /*
            foreach (var model in Models())
            {
                new RobustLogger().Info(string.Format("Finding textures/lightmaps for model '{0}'...", model.GetIdentifier()));
                try
                {
                    byte[] modelData = (byte[])model.Resource();
                    if (modelData == null)
                    {
                        new RobustLogger().Warning(string.Format("Missing model '{0}', needed by module '{1}'", model.GetIdentifier(), displayName));
                        continue;
                    }

                    lookupTextureQueries.UnionWith(ModelTools.IterateTextures(modelData));
                    lookupLightmapQueries.UnionWith(ModelTools.IterateLightmaps(modelData));
                }
                catch (Exception ex)
                {
                    new RobustLogger().Warning(string.Format("Suppressed exception while getting model data '{0}': {1}", model.GetIdentifier(), ex.Message));
                }
            }
            */

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

                new RobustLogger().Debug(string.Format("Adding {0} texture location(s) for '{1}' to '{2}': {3}",
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

        public abstract void AddLocations(IEnumerable<string> filepaths);
        public abstract List<string> Locations();
        public abstract string Activate(string filepath = null);
        public abstract bool IsActive();
        public abstract object Resource();
        public abstract string Filename();
        public abstract ResourceIdentifier GetIdentifier();
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
        private readonly Installation.Installation _installation;
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
        public string GetResName()
        {
            return ResName;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1796-1803
        // Original: def restype(self) -> ResourceType:
        public ResourceType GetResType()
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
                new Logger.RobustLogger().Warning(
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
                    new Logger.RobustLogger().Warning($"No resource found for '{Identifier}'");
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

        // Placeholder methods - will be fully implemented as dependencies are ported
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module.py:1840-1874
        // Original: def data(self) -> bytes | None:
        public byte[] Data()
        {
            // TODO: Implement full data() method once helper functions are ported
            string activePath = Active();
            if (activePath == null)
            {
                return null;
            }

            // Check if capsule file
            if (FileHelpers.IsCapsuleFile(activePath))
            {
                var capsule = new Capsule(activePath);
                return capsule.GetResource(ResName, ResType);
            }

            // Check if BIF file
            if (FileHelpers.IsBifFile(activePath))
            {
                var resource = _installation.Resource(ResName, ResType, new[] { SearchLocation.CHITIN });
                return resource?.Data;
            }

            // Regular file
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
            // TODO: Implement full resource() method once all format readers are ported
            // This requires read_are, read_dlg, read_git, etc. functions
            if (!_resourceLoadAttempted)
            {
                _resourceLoadAttempted = true; // Mark as attempted to prevent repeated loading
                byte[] data = Data();
                if (data == null)
                {
                    // Data() returned null, set to default to cache the "not found" result
                    _resourceObj = default(T);
                    return default(T);
                }

                // Placeholder - will be replaced with actual format readers
                // conversions: dict[ResourceType, Callable[[SOURCE_TYPES], Any]] = { ... }
                // In Python: self._resource_obj = conversions.get(self._restype, lambda _: None)(data)
                // When format readers are implemented, set _resourceObj to the converted resource object here
                // For now, set to default(T) to cache the "conversion not implemented" state
                // This prevents repeated calls to Data() while format readers are being ported
                _resourceObj = default(T);
            }

            return _resourceObj;
        }
    }
}
