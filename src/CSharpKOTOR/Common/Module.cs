using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Formats.Capsule;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
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
            KModuleType modType = extension switch
            {
                ".rim" => KModuleType.MAIN,
                "_s.rim" => KModuleType.DATA,
                "_dlg.erf" => KModuleType.K2_DLG,
                ".mod" => KModuleType.MOD,
                _ => throw new ArgumentException($"Unknown module extension: {extension}")
            };

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

            return pieceInfo.ModType switch
            {
                KModuleType.DATA => new ModuleDataPiece(path, createIfNotExist),
                KModuleType.MAIN => new ModuleLinkPiece(path, createIfNotExist),
                KModuleType.K2_DLG => new ModuleDLGPiece(path, createIfNotExist),
                KModuleType.MOD => new ModuleFullOverridePiece(path, createIfNotExist),
                _ => throw new ArgumentException($"Unknown module type: {pieceInfo.ModType}")
            };
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
                string moduleIfoPath = Path.Combine(Path.GetDirectoryName(Path.GetResolvedPath()), "module.ifo");
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
}
