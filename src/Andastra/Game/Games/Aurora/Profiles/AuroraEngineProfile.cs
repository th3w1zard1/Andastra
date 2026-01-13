using System.Collections.Generic;
using Andastra.Game.Games.Aurora.EngineApi;
using Andastra.Game.Games.Common;
using Andastra.Game.Scripting.Interfaces;

namespace Andastra.Game.Games.Aurora.Profiles
{
    /// <summary>
    /// Abstract base class for Aurora Engine game profiles (Neverwinter Nights, Neverwinter Nights 2).
    /// </summary>
    /// <remarks>
    /// Aurora Engine Profile Base:
    /// - Based on Aurora Engine game profile system (Neverwinter Nights, Neverwinter Nights 2)
    /// - Aurora Engine uses CExoResMan for resource management (different from Odyssey's CExoKeyTable)
    /// - Resource precedence: OVERRIDE > MODULE > HAK (in load order) > BASE_GAME > HARDCODED
    /// - Original implementation: Aurora Engine executables (nwmain.exe, nwn2main.exe) initialize resource paths
    /// - Cross-engine comparison:
    ///   - Odyssey: chitin.key/KEY files with BIF archives
    ///   - Aurora: HAK files (ERF format) with module files and override directory
    ///   - Eclipse: PCC/UPK packages with RIM files
    ///   - Infinity: CHITIN.KEY with BIF files
    ///
    /// Aurora Engine Resource System (Reverse Engineered):
    /// - CExoResMan::CExoResMan (nwmain.exe: constructor @ 0x14018d6f0)
    ///   - Initializes resource manager with memory management and key table storage
    ///   - Sets up NWSync subsystem for resource synchronization
    /// - CExoResMan::Initialize (nwmain.exe: initialization @ addresses need Ghidra verification)
    ///   - Sets up MODULES, OVERRIDE, HAK directory aliases via AddDir calls
    ///   - Registers HAK files via AddKeyTable (type 3: encapsulated resource files)
    /// - Resource lookup: CExoResMan::Demand @ 0x14018ef90 routes to service functions
    ///   - ServiceFromDirectory: Override/modules directories (type 3)
    ///   - ServiceFromEncapsulated: HAK files (ERF format, type 2)
    ///   - ServiceFromResFile: Base game KEY/BIF resources (type 1)
    ///
    /// Module Loading System:
    /// - Module.ifo: Contains module metadata, HAK file references, entry area, etc.
    /// - HAK files: ERF archives containing module resources (models, textures, scripts)
    /// - Module directory: Contains module-specific files (ARE, GIT, IFO, etc.)
    /// - Override directory: Highest priority for modding (loose files override everything)
    ///
    /// Game Session Management:
    /// - CServerExoApp::LoadModule @ 0x140565c50: Loads module by name
    /// - CServerExoApp::UnloadModule @ 0x14056df00: Cleans up current module
    /// - Module state: Managed by CServerExoApp internal structures
    /// - Entity management: CNWSCreature, CNWSPlaceable, CNWSDoor, etc.
    ///
    /// This base class provides common Aurora Engine functionality while allowing
    /// game-specific implementations for Neverwinter Nights and Neverwinter Nights 2.
    /// </remarks>
    public abstract class AuroraEngineProfile : BaseEngineProfile
    {
        public override EngineFamily EngineFamily
        {
            get { return EngineFamily.Aurora; }
        }

        public override IEngineApi CreateEngineApi()
        {
            // Aurora Engine API provides NWScript functions for both NWN and NWN2
            // Function set is largely the same, with NWN2 having additional functions
            return new AuroraEngineApi();
        }

        protected override IResourceConfig CreateResourceConfig()
        {
            // Aurora Engine resource configuration
            // All Aurora games use the same basic structure: modules, override, hak directories
            // Game-specific differences handled in subclasses
            return new AuroraResourceConfig();
        }

        protected override ITableConfig CreateTableConfig()
        {
            // Aurora Engine table configuration
            // Uses 2DA files for game data (appearance, baseitems, classes, etc.)
            // Game-specific table schemas handled in subclasses
            return new AuroraTableConfig();
        }

        /// <summary>
        /// Aurora Engine resource configuration.
        /// </summary>
        /// <remarks>
        /// Aurora Resource Configuration:
        /// - ChitinKeyFile: Aurora Engine doesn't use chitin.key (uses KEY files differently)
        /// - TexturePackFiles: Aurora uses loose TGA/DDS files and ERF archives, not texture packs
        /// - DialogTlkFile: dialog.tlk for dialogue strings
        /// - ModulesDirectory: "modules" directory containing module files
        /// - OverrideDirectory: "override" directory for modding (highest priority)
        /// - SavesDirectory: "saves" directory for save games
        ///
        /// Directory Structure (Aurora Engine):
        /// - modules/: Module directories (module.ifo, area files, etc.)
        /// - override/: Loose files that override everything (highest priority)
        /// - hak/: HAK files (ERF archives) loaded per module
        /// - data/: Base game data files (KEY/BIF, ERF archives, loose files)
        /// - saves/: Save game files
        /// - music/: Music files
        /// - ambient/: Ambient sound files
        /// - movies/: Movie files
        /// - portraits/: Character portrait images
        /// - tlk/: Dialogue TLK files
        /// </remarks>
        private class AuroraResourceConfig : IResourceConfig
        {
            public string ChitinKeyFile
            {
                get
                {
                    // Aurora Engine uses KEY files but not chitin.key specifically
                    // Individual KEY files: chitin.key, patch.key, xp1.key, etc.
                    // This is handled by the resource provider, not a single chitin.key file
                    return null;
                }
            }

            public IReadOnlyList<string> TexturePackFiles
            {
                get
                {
                    // Aurora Engine doesn't use texture pack ERF files like Odyssey
                    // Textures are either loose TGA/DDS files or in ERF/HAK archives
                    return new List<string>();
                }
            }

            public string DialogTlkFile
            {
                get
                {
                    // Aurora Engine uses dialog.tlk for dialogue strings
                    // Based on nwmain.exe: Dialog TLK file loading
                    return "dialog.tlk";
                }
            }

            public string ModulesDirectory
            {
                get
                {
                    // Aurora Engine modules directory
                    // Based on nwmain.exe: "MODULES=" @ 0x140d80d20, "modules" @ 0x140d80f38
                    return "modules";
                }
            }

            public string OverrideDirectory
            {
                get
                {
                    // Aurora Engine override directory (highest priority for modding)
                    // Based on nwmain.exe: "OVERRIDE=" @ 0x140d80d50, "override" @ 0x140d80f40
                    return "override";
                }
            }

            public string SavesDirectory
            {
                get
                {
                    // Aurora Engine saves directory
                    return "saves";
                }
            }
        }

        /// <summary>
        /// Aurora Engine 2DA table configuration.
        /// </summary>
        /// <remarks>
        /// Aurora Table Configuration:
        /// - RequiredTables: Core 2DA files needed by Aurora Engine
        /// - AppearanceColumns: appearance.2da column mapping
        /// - BaseItemsColumns: baseitems.2da column mapping
        ///
        /// 2DA Files Used by Aurora Engine:
        /// - appearance.2da: Creature appearance data (models, textures, animations)
        /// - baseitems.2da: Item template data (models, properties, stats)
        /// - classes.2da: Character class definitions
        /// - feat.2da: Feat/spell definitions
        /// - skills.2da: Skill definitions
        /// - spells.2da: Spell definitions
        /// - surfacemat.2da: Surface material properties (walkmesh physics)
        /// - placeables.2da: Placeable object templates
        /// - genericdoors.2da: Door template data
        /// - heads.2da: Character head models and customization
        /// - portraits.2da: Character portrait images
        /// - racialtypes.2da: Race definitions
        /// - genders.2da: Gender definitions
        /// - soundset.2da: Audio soundset definitions
        /// - bodybag.2da: Death/decapitation model mappings
        /// - animations.2da: Animation sequence definitions
        /// - tilesets.2da: Area tileset definitions
        /// - loadscreens.2da: Loading screen images
        /// - keymap.2da: Input key mappings
        /// - reputation.2da: Faction reputation modifiers
        /// - ambientmusic.2da: Background music tracks
        /// - ambientsound.2da: Ambient sound effects
        /// </remarks>
        private class AuroraTableConfig : ITableConfig
        {
            private static readonly List<string> _requiredTables = new List<string>
            {
                "appearance",
                "baseitems",
                "classes",
                "feat",
                "skills",
                "spells",
                "surfacemat",
                "placeables",
                "genericdoors",
                "heads",
                "portraits",
                "racialtypes",
                "genders",
                "soundset",
                "bodybag",
                "animations",
                "tilesets",
                "loadscreens",
                "keymap",
                "reputation",
                "ambientmusic",
                "ambientsound"
            };

            private static readonly Dictionary<string, string> _appearanceColumns = new Dictionary<string, string>
            {
                { "label", "label" },
                { "modeltype", "modeltype" },
                { "race", "race" },
                { "gender", "gender" },
                { "age", "age" },
                { "height", "height" },
                { "weight", "weight" },
                { "modela", "modela" },
                { "modelb", "modelb" },
                { "modelc", "modelc" },
                { "texa", "texa" },
                { "texb", "texb" },
                { "texc", "texc" },
                { "walkdist", "walkdist" },
                { "rundist", "rundist" },
                { "headtrack", "headtrack" },
                { "headarc_h", "headarc_h" },
                { "headarc_v", "headarc_v" },
                { "voicetype", "voicetype" },
                { "wing_tail", "wing_tail" },
                { "wing_max", "wing_max" },
                { "animscale", "animscale" },
                { "bloodcolr", "bloodcolr" },
                { "wing_tai2", "wing_tai2" },
                { "wing_max2", "wing_max2" },
                { "animscale2", "animscale2" },
                { "bloodcol2", "bloodcol2" },
                { "feetdist", "feetdist" },
                { "soundapptype", "soundapptype" },
                { "head_name", "head_name" },
                { "body_name", "body_name" },
                { "wing_name", "wing_name" },
                { "wing_name2", "wing_name2" },
                { "targetable", "targetable" },
                { "envmap", "envmap" },
                { "headaxis", "headaxis" },
                { "creper_space", "creper_space" },
                { "helmet_scale_m", "helmet_scale_m" },
                { "helmet_scale_f", "helmet_scale_f" }
            };

            private static readonly Dictionary<string, string> _baseItemsColumns = new Dictionary<string, string>
            {
                { "label", "label" },
                { "name", "name" },
                { "equipableslots", "equipableslots" },
                { "canrotateicon", "canrotateicon" },
                { "modeltype", "modeltype" },
                { "itemclass", "itemclass" },
                { "genderspecific", "genderspecific" },
                { "part1", "part1" },
                { "part2", "part2" },
                { "part3", "part3" },
                { "defaulticon", "defaulticon" },
                { "defaultmodel", "defaultmodel" },
                { "basitemstatref", "basitemstatref" },
                { "descidentified", "descidentified" },
                { "description", "description" },
                { "invslotwidth", "invslotwidth" },
                { "invslotheight", "invslotheight" },
                { "wearable", "wearable" },
                { "effectcolumn1", "effectcolumn1" },
                { "cost", "cost" },
                { "chargescancarry", "chargescancarry" },
                { "plot", "plot" },
                { "minlevel", "minlevel" },
                { "storepanel", "storepanel" },
                { "reqfeat0", "reqfeat0" },
                { "reqfeat1", "reqfeat1" },
                { "reqfeat2", "reqfeat2" },
                { "reqfeat3", "reqfeat3" },
                { "reqfeat4", "reqfeat4" },
                { "reqfeat5", "reqfeat5" },
                { "acenhancementbonus", "acenhancementbonus" },
                { "weaponwield", "weaponwield" },
                { "dieappear", "dieappear" },
                { "weapontype", "weapontype" },
                { "weapcolor", "weapcolor" },
                { "ammunitiontype", "ammunitiontype" },
                { "quiver", "quiver" },
                { "stacking", "stacking" },
                { "itemtype", "itemtype" },
                { "ilrstacksize", "ilrstacksize" },
                { "propsku", "propsku" },
                { "modelvariation", "modelvariation" },
                { "bodyvariation", "bodyvariation" },
                { "texturevariation", "texturevariation" },
                { "normalvariation", "normalvariation" },
                { "paletteid", "paletteid" },
                { "comment", "comment" },
                { "paletteable", "paletteable" },
                { "upgradable", "upgradable" },
                { "identified", "identified" }
            };

            public IReadOnlyList<string> RequiredTables
            {
                get { return _requiredTables; }
            }

            public IReadOnlyDictionary<string, string> AppearanceColumns
            {
                get { return _appearanceColumns; }
            }

            public IReadOnlyDictionary<string, string> BaseItemsColumns
            {
                get { return _baseItemsColumns; }
            }
        }
    }
}
