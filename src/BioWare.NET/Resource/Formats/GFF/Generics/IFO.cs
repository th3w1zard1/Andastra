using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics
{
    /// <summary>
    /// Stores module information.
    /// </summary>
    /// <remarks>
    /// WHAT IS AN IFO FILE?
    /// 
    /// An IFO file is a Module Info file that stores all the information about a game module.
    /// A module is a complete game area that the player can explore, like a planet, space station,
    /// or building. The IFO file tells the game engine everything it needs to know about the module,
    /// including where the player starts, which areas are in the module, what scripts to run, and
    /// what time of day it is.
    /// 
    /// WHAT DATA DOES IT STORE?
    /// 
    /// An IFO file contains:
    /// 
    /// 1. MODULE IDENTIFICATION:
    ///    - ModId: A unique 16-byte identifier that distinguishes this module from all others
    ///    - Name: The display name of the module (shown in-game, can be in different languages)
    ///    - ModName: An alternate name for the module
    ///    - ResRef: The resource reference (file name) of the module
    ///    - Tag: A tag identifier used by scripts to reference this module
    /// 
    /// 2. ENTRY POINT:
    ///    - EntryArea: Which area the player starts in when entering the module
    ///    - EntryX, EntryY, EntryZ: The exact position where the player appears
    ///    - EntryDirectionX, EntryDirectionY, EntryDirectionZ: Which direction the player faces
    ///    - EntryDirection: The angle (in degrees) the player faces (calculated from X/Y/Z components)
    /// 
    /// 3. AREA LIST:
    ///    - AreaList: A list of all areas (ARE files) that are part of this module
    ///    - Each area is a separate location the player can visit
    /// 
    /// 4. SCRIPT HOOKS:
    ///    - OnClientEnter: Script that runs when a player enters the module
    ///    - OnClientLeave: Script that runs when a player leaves the module
    ///    - OnHeartbeat: Script that runs periodically while the module is loaded
    ///    - OnUserDefined: Script that runs when triggered by other scripts
    ///    - OnActivateItem: Script that runs when the player activates an item
    ///    - OnAcquireItem: Script that runs when the player gets an item
    ///    - OnUnacquireItem: Script that runs when the player loses an item
    ///    - OnPlayerDeath: Script that runs when the player dies
    ///    - OnPlayerDying: Script that runs when the player is about to die
    ///    - OnPlayerRespawn: Script that runs when the player respawns
    ///    - OnPlayerRest: Script that runs when the player rests
    ///    - OnPlayerLevelUp: Script that runs when the player levels up
    ///    - OnPlayerCancelCutscene: Script that runs when the player cancels a cutscene
    ///    - OnLoad: Script that runs when the module is loaded
    ///    - OnStart: Script that runs when the module starts
    /// 
    /// 5. TIME SETTINGS:
    ///    - DawnHour: The hour of day when dawn begins (0-23)
    ///    - DuskHour: The hour of day when dusk begins (0-23)
    ///    - TimeScale: How fast time passes (1.0 = normal speed, 2.0 = twice as fast)
    ///    - StartMonth: The month the module starts in (1-12)
    ///    - StartDay: The day of month the module starts on (1-31)
    ///    - StartHour: The hour of day the module starts at (0-23)
    ///    - StartYear: The year the module starts in
    /// 
    /// 6. MODULE METADATA:
    ///    - Description: A description of the module (shown in toolset, not in-game)
    ///    - ModVersion: The version number of the module
    ///    - ExpansionPack: Which expansion pack is required (0 = base game, 1+ = expansion)
    ///    - VaultId: An ID used for multiplayer modules
    ///    - VoId: Voice-over ID (for modules with voice acting)
    ///    - Hak: Hak pack name (for modules that use hak packs)
    ///    - XpScale: Experience point scaling (1.0 = normal, 2.0 = double XP)
    ///    - StartMovie: A movie file to play when the module starts
    /// 
    /// HOW DOES THE GAME ENGINE USE IFO FILES?
    /// 
    /// STEP 1: Loading the Module
    /// - When the player enters a module, the engine loads the IFO file
    /// - It reads the EntryArea to know which area to load first
    /// - It reads the EntryX/Y/Z to know where to place the player
    /// - It reads the EntryDirection to know which way the player should face
    /// 
    /// STEP 2: Initializing the Module
    /// - The engine reads the AreaList to know which areas are available
    /// - It loads all the areas listed in AreaList
    /// - It sets up the time system using the time settings
    /// - It runs the OnLoad script (if present)
    /// 
    /// STEP 3: Running Scripts
    /// - When events happen (player enters, player dies, etc.), the engine runs the corresponding script
    /// - Scripts can modify the game world, spawn creatures, change dialogue, etc.
    /// 
    /// STEP 4: Managing Time
    /// - The engine uses the time settings to determine what time of day it is
    /// - It uses DawnHour and DuskHour to transition between day and night
    /// - It uses TimeScale to control how fast time passes
    /// 
    /// WHY ARE IFO FILES NEEDED?
    /// 
    /// Without IFO files, the game engine wouldn't know:
    /// - Where the player should start when entering a module
    /// - Which areas are part of the module
    /// - What scripts to run when events happen
    /// - What time of day it is
    /// - What the module's name and description are
    /// 
    /// The IFO file acts as a master configuration file that tells the engine everything about the module.
    /// 
    /// RELATIONSHIP TO OTHER FILES:
    /// 
    /// - ARE files: The area files listed in AreaList
    /// - GIT files: The game instance template files for each area
    /// - LYT files: The layout files for each area
    /// - NCS files: The compiled script files referenced by script hooks
    /// - MOD/RIM files: The module archive files that contain all these resources
    /// 
    /// Together, these files define a complete game module that the player can explore.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): IFO files are loaded when a module is initialized. The engine reads
    /// the entry point information to place the player, loads all areas in the AreaList, sets up
    /// the time system, and registers script hooks for event handling.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:36-124
    /// </remarks>
    [PublicAPI]
    public sealed class IFO
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:36
        // Original: BINARY_TYPE = ResourceType.IFO
        public static readonly ResourceType BinaryType = ResourceType.IFO;

        /// <summary>
        /// Module ID: 16-byte unique identifier generated by toolset.
        /// This ID uniquely identifies this module and distinguishes it from all other modules.
        /// </summary>
        public byte[] ModId { get; set; } = new byte[16];

        /// <summary>
        /// Localized module name displayed in-game.
        /// This name can be in different languages depending on the game's localization settings.
        /// </summary>
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();

        /// <summary>
        /// Alternate module name (internal name).
        /// This is a secondary name for the module, separate from the display name.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:51
        /// Original: self.mod_name: LocalizedString = LocalizedString.from_invalid()
        /// </remarks>
        public LocalizedString ModName { get; set; } = LocalizedString.FromInvalid();

        /// <summary>
        /// Area name (ResRef of the primary area).
        /// This is the resource reference of the main area in the module.
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:56
        /// Original: self.area_name: ResRef = ResRef.from_blank()
        /// </remarks>
        public ResRef AreaName { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Module resource reference (file name).
        /// This is the name of the module file (without extension).
        /// </summary>
        /// <remarks>
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:62
        /// Original: self.resref: ResRef = ResRef.from_blank()
        /// </remarks>
        public ResRef ResRef { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Module tag identifier.
        /// This tag is used by scripts to reference this module.
        /// </summary>
        public string Tag { get; set; } = string.Empty;

        /// <summary>
        /// Entry area ResRef (starting area).
        /// When the player enters this module, they start in this area.
        /// </summary>
        public ResRef EntryArea { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Entry X position (east-west coordinate).
        /// When the player enters this module, they appear at this X coordinate.
        /// </summary>
        public float EntryX { get; set; }

        /// <summary>
        /// Entry Y position (north-south coordinate).
        /// When the player enters this module, they appear at this Y coordinate.
        /// </summary>
        public float EntryY { get; set; }

        /// <summary>
        /// Entry Z position (up-down coordinate).
        /// When the player enters this module, they appear at this Z coordinate.
        /// </summary>
        public float EntryZ { get; set; }

        /// <summary>
        /// Entry direction X component (facing direction).
        /// This is the X component of the direction vector that determines which way the player faces.
        /// </summary>
        public float EntryDirectionX { get; set; }

        /// <summary>
        /// Entry direction Y component (facing direction).
        /// This is the Y component of the direction vector that determines which way the player faces.
        /// </summary>
        public float EntryDirectionY { get; set; }

        /// <summary>
        /// Entry direction Z component (facing direction).
        /// This is the Z component of the direction vector that determines which way the player faces.
        /// </summary>
        public float EntryDirectionZ { get; set; }

        // Module script hooks
        public ResRef OnClientEnter { get; set; } = ResRef.FromBlank();
        public ResRef OnClientLeave { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeat { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefined { get; set; } = ResRef.FromBlank();
        public ResRef OnActivateItem { get; set; } = ResRef.FromBlank();
        public ResRef OnAcquireItem { get; set; } = ResRef.FromBlank();
        public ResRef OnUnacquireItem { get; set; } = ResRef.FromBlank();
        public ResRef OnPlayerDeath { get; set; } = ResRef.FromBlank();
        public ResRef OnPlayerDying { get; set; } = ResRef.FromBlank();
        public ResRef OnPlayerRespawn { get; set; } = ResRef.FromBlank();
        public ResRef OnPlayerRest { get; set; } = ResRef.FromBlank();
        public ResRef OnPlayerLevelUp { get; set; } = ResRef.FromBlank();
        public ResRef OnPlayerCancelCutscene { get; set; } = ResRef.FromBlank();

        // Area list (areas in this module)
        public List<ResRef> AreaList { get; set; } = new List<ResRef>();

        // Expansion pack requirements
        public int ExpansionPack { get; set; }

        // Module description
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();

        // Module version
        public int ModVersion { get; set; }

        // Module Vault ID (for multiplayer)
        public int VaultId { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:86
        // Original: self.vo_id: str = ""
        public string VoId { get; set; } = string.Empty;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:113
        // Original: self.hak: str = ""
        public string Hak { get; set; } = string.Empty;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:115-122
        // Original: Time settings
        public int DawnHour { get; set; }
        public int DuskHour { get; set; }
        public int TimeScale { get; set; }
        public int StartMonth { get; set; }
        public int StartDay { get; set; }
        public int StartHour { get; set; }
        public int StartYear { get; set; }
        public int XpScale { get; set; }

        // Game time fields (current game time stored in IFO)
        // SerializeIfoGameTime @ (K1: TODO: Find this address, TSL: 0x00500290) (swkotor2.exe: SerializeIfoGameTime @ 0x00500290)
        // Lines 96-100: Writes current game time as Mod_StartMinute/Second/MiliSec and Mod_PauseDay/PauseTime
        // These fields are written from the current game time when the IFO is saved
        // Implementation: Use IFOHelpers.PopulateIfoGameTimeFromTimeManager() to populate these fields from TimeManager
        /// <summary>
        /// Current game time - minute component (0-59).
        /// Written to IFO as Mod_StartMinute field.
        /// </summary>
        /// <remarks>
        /// SerializeIfoGameTime @ (K1: TODO: Find this address, TSL: 0x00500290) line 96 (swkotor2.exe: SerializeIfoGameTime @ 0x00500290)
        /// Written via FUN_004137e0(param_1, param_2, local_5c[0], "Mod_StartMinute")
        /// Populated by IFOHelpers.PopulateIfoGameTimeFromTimeManager() which matches original engine behavior
        /// </remarks>
        public int StartMinute { get; set; }

        /// <summary>
        /// Current game time - second component (0-59).
        /// Written to IFO as Mod_StartSecond field.
        /// </summary>
        /// <remarks>
        /// SerializeIfoGameTime @ (K1: TODO: Find this address, TSL: 0x00500290) line 97 (swkotor2.exe: SerializeIfoGameTime @ 0x00500290)
        /// Written via FUN_004137e0(param_1, param_2, local_58[0], "Mod_StartSecond")
        /// Populated by IFOHelpers.PopulateIfoGameTimeFromTimeManager() which matches original engine behavior
        /// </remarks>
        public int StartSecond { get; set; }

        /// <summary>
        /// Current game time - millisecond component (0-999).
        /// Written to IFO as Mod_StartMiliSec field.
        /// </summary>
        /// <remarks>
        /// SerializeIfoGameTime @ (K1: TODO: Find this address, TSL: 0x00500290) line 98 (swkotor2.exe: SerializeIfoGameTime @ 0x00500290)
        /// Written via FUN_004137e0(param_1, param_2, local_54[0], "Mod_StartMiliSec")
        /// Populated by IFOHelpers.PopulateIfoGameTimeFromTimeManager() which matches original engine behavior
        /// </remarks>
        public int StartMiliSec { get; set; }

        /// <summary>
        /// Pause day (day when time was paused).
        /// Written to IFO as Mod_PauseDay field.
        /// </summary>
        /// <remarks>
        /// SerializeIfoGameTime @ (K1: TODO: Find this address, TSL: 0x00500290) line 99 (swkotor2.exe: SerializeIfoGameTime @ 0x00500290)
        /// Written via FUN_00413880(param_1, param_2, uVar1, "Mod_PauseDay")
        /// Value comes from offset +0x28 of time system object (from FUN_004dc6e0 result)
        /// Populated by IFOHelpers.PopulateIfoGameTimeFromTimeManager() which matches original engine behavior
        /// </remarks>
        public uint PauseDay { get; set; }

        /// <summary>
        /// Pause time (time when paused, in milliseconds).
        /// Written to IFO as Mod_PauseTime field.
        /// </summary>
        /// <remarks>
        /// SerializeIfoGameTime @ (K1: TODO: Find this address, TSL: 0x00500290) line 100 (swkotor2.exe: SerializeIfoGameTime @ 0x00500290)
        /// Written via FUN_00413880(param_1, param_2, uVar2, "Mod_PauseTime")
        /// Value comes from offset +0x2c of time system object (from FUN_004dc6e0 result)
        /// String reference: "Mod_PauseTime" @ (K1: TODO: Find this address, TSL: 0x007be89c)
        /// Populated by IFOHelpers.PopulateIfoGameTimeFromTimeManager() which matches original engine behavior
        /// </remarks>
        public uint PauseTime { get; set; }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:123-124
        // Original: Additional script hooks
        public ResRef StartMovie { get; set; } = ResRef.FromBlank();

        // Additional script hooks from Python
        public ResRef OnLoad { get; set; } = ResRef.FromBlank();
        public ResRef OnStart { get; set; } = ResRef.FromBlank();

        // Entry direction as angle (computed from X/Y components)
        public float EntryDirection { get; set; }

        public IFO()
        {
        }
    }
}
