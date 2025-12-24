using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Plot;

namespace Andastra.Runtime.Core.Save
{
    /// <summary>
    /// Complete save game data.
    /// </summary>
    /// <remarks>
    /// Save Game Data Structure:
    /// - Based on swkotor2.exe save game format
    /// - Located via string references: "savenfo" @ 0x007be1f0, "SAVEGAME" @ 0x007be28c, "SAVES:" @ 0x007be284
    /// - Save function: FUN_004eb750 @ 0x004eb750 creates save game ERF archive
    /// - Maps to KOTOR save structure:
    ///   - NFO.res (save metadata): GFF with "NFO " signature, contains AREANAME, TIMEPLAYED, SAVEGAMENAME, etc.
    ///   - GLOBALVARS.res (global variables): GFF with "GLOB" signature, saved by FUN_005ac670 @ 0x005ac670
    ///   - PARTYTABLE.res (party state): GFF with "PT  " signature, saved by FUN_0057bd70 @ 0x0057bd70
    ///   - [module]_s.rim (per-module states): ERF archive containing area state GFF files for visited areas
    ///   - Various GFF resources for entity states (creature positions, door states, etc.)
    /// - Save file location: "SAVES:\{saveName}\savegame.sav" (ERF archive)
    /// - Save metadata location: "SAVES:\{saveName}\savenfo.res" (GFF file)
    /// - Original implementation: Save files are ERF archives (ERF with "MOD V1.0" signature @ 0x007be0d4)
    /// </remarks>
    public class SaveGameData
    {
        /// <summary>
        /// Save name (displayed in UI).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Type of save (manual, auto, quick).
        /// </summary>
        public SaveType SaveType { get; set; }

        /// <summary>
        /// When the save was created.
        /// </summary>
        public DateTime SaveTime { get; set; }

        /// <summary>
        /// Current module ResRef.
        /// </summary>
        public string CurrentModule { get; set; }

        /// <summary>
        /// Entry position in the current module.
        /// </summary>
        public Vector3 EntryPosition { get; set; }

        /// <summary>
        /// Entry facing in the current module.
        /// </summary>
        public float EntryFacing { get; set; }

        /// <summary>
        /// In-game time.
        /// </summary>
        public GameTime GameTime { get; set; }

        /// <summary>
        /// Global variable state.
        /// </summary>
        public GlobalVariableState GlobalVariables { get; set; }

        /// <summary>
        /// Party state.
        /// </summary>
        public PartyState PartyState { get; set; }

        /// <summary>
        /// Per-area state (keyed by area ResRef).
        /// </summary>
        public Dictionary<string, AreaState> AreaStates { get; set; }

        /// <summary>
        /// Module-to-area mapping (keyed by module ResRef, value is list of area ResRefs belonging to that module).
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Mod_Area_list field in module IFO file contains the list of areas belonging to each module.
        /// This mapping is stored in save data to enable checking if an area belongs to a module even when the module is not loaded.
        /// Original implementation: Module IFO file contains Mod_Area_list (GFF List) with Area_Name fields for each area.
        /// Located via string reference: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
        /// </remarks>
        public Dictionary<string, List<string>> ModuleAreaMappings { get; set; }

        /// <summary>
        /// Journal entries.
        /// </summary>
        public List<JournalEntry> JournalEntries { get; set; }

        /// <summary>
        /// Plot states (keyed by plot index).
        /// </summary>
        /// <remarks>
        /// Plot State Storage:
        /// - Based on swkotor2.exe: Plot states are saved as part of game state
        /// - Original implementation: Plot states track which plots have been triggered/completed
        /// - Plot state includes: plot index, label, triggered status, completed status, trigger count, last triggered time
        /// </remarks>
        public Dictionary<int, Plot.PlotState> PlotStates { get; set; }

        /// <summary>
        /// Screenshot data (PNG bytes).
        /// </summary>
        public byte[] Screenshot { get; set; }

        /// <summary>
        /// Total play time.
        /// </summary>
        public TimeSpan PlayTime { get; set; }

        /// <summary>
        /// Game version that created this save.
        /// </summary>
        public string GameVersion { get; set; }

        /// <summary>
        /// Current area name (for display).
        /// </summary>
        public string CurrentAreaName { get; set; }

        /// <summary>
        /// Whether cheats were used.
        /// </summary>
        public bool CheatUsed { get; set; }

        /// <summary>
        /// Save slot number.
        /// </summary>
        public int SaveNumber { get; set; }

        /// <summary>
        /// Gameplay hint flag.
        /// </summary>
        public bool GameplayHint { get; set; }

        /// <summary>
        /// Story hint flags (10 flags).
        /// </summary>
        public List<bool> StoryHints { get; set; }

        /// <summary>
        /// Live content flags (bitmask).
        /// </summary>
        public List<bool> LiveContent { get; set; }

        /// <summary>
        /// Live content entry strings (up to 9 entries: LIVE1-9).
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750
        /// Original implementation stores LIVE1-9 as string fields in NFO GFF
        /// These are live content entry strings that correspond to the LIVECONTENT bitmask
        /// </remarks>
        public List<string> LiveContentStrings { get; set; }

        /// <summary>
        /// Player character name.
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// Faction reputation state.
        /// </summary>
        /// <remarks>
        /// Faction Reputation Storage:
        /// - Based on swkotor2.exe: REPUTE.fac file format
        /// - Located via string reference: "REPUTE" @ (needs verification)
        /// - FAC file format: GFF with "FAC " signature, contains FactionList and RepList
        /// - FactionList: List of Faction structs (FactionName: string, FactionGlobal: uint)
        /// - RepList: List of Reputation structs (FactionID1: uint, FactionID2: uint, FactionRep: uint)
        /// - Original implementation: Faction relationships stored in GFF structures with FactionID, FactionRep fields
        /// - Reference: vendor/xoreos/src/engines/nwn2/faction.cpp (loadFac method)
        /// - Reference: vendor/PyKotor/wiki/Bioware-Aurora-Faction.md (Faction Format documentation)
        /// - Reputation values: 0-100 range (0-10=hostile, 11-89=neutral, 90-100=friendly)
        /// </remarks>
        public FactionReputationState FactionReputation { get; set; }

        public SaveGameData()
        {
            AreaStates = new Dictionary<string, AreaState>();
            JournalEntries = new List<JournalEntry>();
            GameTime = new GameTime();
            GlobalVariables = new GlobalVariableState();
            PartyState = new PartyState();
            StoryHints = new List<bool>();
            LiveContent = new List<bool>();
            LiveContentStrings = new List<string>();
            PlotStates = new Dictionary<int, PlotState>();
            ModuleAreaMappings = new Dictionary<string, List<string>>();
            FactionReputation = new FactionReputationState();
        }
    }

    /// <summary>
    /// Basic save info for save list display.
    /// </summary>
    public class SaveGameInfo
    {
        /// <summary>
        /// Save name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Save type.
        /// </summary>
        public SaveType SaveType { get; set; }

        /// <summary>
        /// When saved.
        /// </summary>
        public DateTime SaveTime { get; set; }

        /// <summary>
        /// Module name for display.
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// Total play time.
        /// </summary>
        public TimeSpan PlayTime { get; set; }

        /// <summary>
        /// PC name.
        /// </summary>
        public string PlayerName { get; set; }

        /// <summary>
        /// PC level.
        /// </summary>
        public int PlayerLevel { get; set; }

        /// <summary>
        /// Slot index.
        /// </summary>
        public int SlotIndex { get; set; }

        /// <summary>
        /// Path to save folder.
        /// </summary>
        public string SavePath { get; set; }
    }

    /// <summary>
    /// In-game time.
    /// </summary>
    public class GameTime
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }

        /// <summary>
        /// Gets total minutes since day start.
        /// </summary>
        public int MinutesPastMidnight
        {
            get { return Hour * 60 + Minute; }
        }

        /// <summary>
        /// Gets total game hours elapsed.
        /// </summary>
        public int TotalHours
        {
            get { return Year * 12 * 28 * 24 + Month * 28 * 24 + Day * 24 + Hour; }
        }
    }

    /// <summary>
    /// Global variable state (GLOBALVARS.res).
    /// </summary>
    public class GlobalVariableState
    {
        /// <summary>
        /// Boolean globals (Name -> Value).
        /// </summary>
        public Dictionary<string, bool> Booleans { get; set; }

        /// <summary>
        /// Numeric globals (Name -> Value).
        /// </summary>
        public Dictionary<string, int> Numbers { get; set; }

        /// <summary>
        /// String globals (Name -> Value).
        /// </summary>
        public Dictionary<string, string> Strings { get; set; }

        /// <summary>
        /// Location globals (Name -> Value).
        /// </summary>
        public Dictionary<string, SavedLocation> Locations { get; set; }

        public GlobalVariableState()
        {
            Booleans = new Dictionary<string, bool>();
            Numbers = new Dictionary<string, int>();
            Strings = new Dictionary<string, string>();
            Locations = new Dictionary<string, SavedLocation>();
        }
    }

    /// <summary>
    /// A saved location reference.
    /// </summary>
    public class SavedLocation
    {
        public string AreaResRef { get; set; }
        public Vector3 Position { get; set; }
        public float Facing { get; set; }
    }

    /// <summary>
    /// Party state (PARTYTABLE.res).
    /// </summary>
    public class PartyState
    {
        /// <summary>
        /// Player character data.
        /// </summary>
        public CreatureState PlayerCharacter { get; set; }

        /// <summary>
        /// Available party members (template ResRef -> state).
        /// </summary>
        public Dictionary<string, PartyMemberState> AvailableMembers { get; set; }

        /// <summary>
        /// Currently selected party (indices into AvailableMembers).
        /// </summary>
        public List<string> SelectedParty { get; set; }

        /// <summary>
        /// Gold/credits.
        /// </summary>
        public int Gold { get; set; }

        /// <summary>
        /// Influence values (K2 only, 12 entries).
        /// </summary>
        public List<int> Influence { get; set; }

        /// <summary>
        /// Current XP.
        /// </summary>
        public int ExperiencePoints { get; set; }

        /// <summary>
        /// Item component count.
        /// </summary>
        public int ItemComponent { get; set; }

        /// <summary>
        /// Item chemical count.
        /// </summary>
        public int ItemChemical { get; set; }

        /// <summary>
        /// Swoop race time 1.
        /// </summary>
        public int Swoop1 { get; set; }

        /// <summary>
        /// Swoop race time 2.
        /// </summary>
        public int Swoop2 { get; set; }

        /// <summary>
        /// Swoop race time 3.
        /// </summary>
        public int Swoop3 { get; set; }

        /// <summary>
        /// Total play time.
        /// </summary>
        public TimeSpan PlayTime { get; set; }

        /// <summary>
        /// Currently controlled NPC ID (-1 if none).
        /// </summary>
        public int ControlledNPC { get; set; }

        /// <summary>
        /// Solo mode flag.
        /// </summary>
        public bool SoloMode { get; set; }

        /// <summary>
        /// Cheat used flag.
        /// </summary>
        public bool CheatUsed { get; set; }

        /// <summary>
        /// Leader ResRef.
        /// </summary>
        public string LeaderResRef { get; set; }

        /// <summary>
        /// Puppet IDs.
        /// </summary>
        public List<uint> Puppets { get; set; }

        /// <summary>
        /// Available puppets (3 flags).
        /// </summary>
        public List<bool> AvailablePuppets { get; set; }

        /// <summary>
        /// Selectable puppets (3 flags).
        /// </summary>
        public List<bool> SelectablePuppets { get; set; }

        /// <summary>
        /// AI state.
        /// </summary>
        public int AIState { get; set; }

        /// <summary>
        /// Follow state.
        /// </summary>
        public int FollowState { get; set; }

        /// <summary>
        /// Galaxy map planet mask.
        /// </summary>
        public int GalaxyMapPlanetMask { get; set; }

        /// <summary>
        /// Galaxy map selected point.
        /// </summary>
        public int GalaxyMapSelectedPoint { get; set; }

        /// <summary>
        /// Pazaak cards (23 entries).
        /// </summary>
        public List<int> PazaakCards { get; set; }

        /// <summary>
        /// Pazaak side list (10 entries).
        /// </summary>
        public List<int> PazaakSideList { get; set; }

        /// <summary>
        /// Tutorial windows shown (33 flags).
        /// </summary>
        public List<bool> TutorialWindowsShown { get; set; }

        /// <summary>
        /// Last GUI panel.
        /// </summary>
        public int LastGUIPanel { get; set; }

        /// <summary>
        /// Feedback messages.
        /// </summary>
        public List<FeedbackMessage> FeedbackMessages { get; set; }

        /// <summary>
        /// Dialogue messages.
        /// </summary>
        public List<DialogueMessage> DialogueMessages { get; set; }

        /// <summary>
        /// Combat messages.
        /// </summary>
        public List<CombatMessage> CombatMessages { get; set; }

        /// <summary>
        /// Cost multipliers.
        /// </summary>
        public List<float> CostMultipliers { get; set; }

        /// <summary>
        /// Disable map flag.
        /// </summary>
        public bool DisableMap { get; set; }

        /// <summary>
        /// Disable regen flag.
        /// </summary>
        public bool DisableRegen { get; set; }

        public PartyState()
        {
            AvailableMembers = new Dictionary<string, PartyMemberState>();
            SelectedParty = new List<string>();
            Influence = new List<int>();
            Puppets = new List<uint>();
            AvailablePuppets = new List<bool>();
            SelectablePuppets = new List<bool>();
            PazaakCards = new List<int>();
            PazaakSideList = new List<int>();
            TutorialWindowsShown = new List<bool>();
            FeedbackMessages = new List<FeedbackMessage>();
            DialogueMessages = new List<DialogueMessage>();
            CombatMessages = new List<CombatMessage>();
            CostMultipliers = new List<float>();
            ControlledNPC = -1;
        }
    }

    /// <summary>
    /// State for a party member.
    /// </summary>
    public class PartyMemberState
    {
        /// <summary>
        /// Template ResRef.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Current creature state.
        /// </summary>
        public CreatureState State { get; set; }

        /// <summary>
        /// Whether available for selection.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Whether can be selected (some NPCs are locked out at times).
        /// </summary>
        public bool IsSelectable { get; set; }
    }

    /// <summary>
    /// Saved creature state.
    /// </summary>
    public class CreatureState : EntityState
    {
        /// <summary>
        /// Current level.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Experience points.
        /// </summary>
        public int XP { get; set; }

        /// <summary>
        /// Current Force points (if any).
        /// </summary>
        public int CurrentFP { get; set; }

        /// <summary>
        /// Maximum Force points.
        /// </summary>
        public int MaxFP { get; set; }

        /// <summary>
        /// Equipped items.
        /// </summary>
        public EquipmentState Equipment { get; set; }

        /// <summary>
        /// Inventory items.
        /// </summary>
        public List<ItemState> Inventory { get; set; }

        /// <summary>
        /// Known powers/abilities.
        /// </summary>
        public List<string> KnownPowers { get; set; }

        /// <summary>
        /// Known feats.
        /// </summary>
        public List<string> KnownFeats { get; set; }

        /// <summary>
        /// Class levels.
        /// </summary>
        public List<ClassLevel> ClassLevels { get; set; }

        /// <summary>
        /// Skill ranks.
        /// </summary>
        public Dictionary<string, int> Skills { get; set; }

        /// <summary>
        /// Base attributes.
        /// </summary>
        public AttributeSet Attributes { get; set; }

        /// <summary>
        /// Alignment value (0-100, 50 = neutral).
        /// </summary>
        public int Alignment { get; set; }

        public CreatureState()
        {
            Equipment = new EquipmentState();
            Inventory = new List<ItemState>();
            KnownPowers = new List<string>();
            KnownFeats = new List<string>();
            ClassLevels = new List<ClassLevel>();
            Skills = new Dictionary<string, int>();
            Attributes = new AttributeSet();
        }
    }

    /// <summary>
    /// Equipment slot state.
    /// </summary>
    public class EquipmentState
    {
        public ItemState Head { get; set; }
        public ItemState Armor { get; set; }
        public ItemState Gloves { get; set; }
        public ItemState RightHand { get; set; }
        public ItemState LeftHand { get; set; }
        public ItemState Belt { get; set; }
        public ItemState Implant { get; set; }
        public ItemState RightArm { get; set; }
        public ItemState LeftArm { get; set; }
    }

    /// <summary>
    /// Saved item state.
    /// </summary>
    public class ItemState
    {
        /// <summary>
        /// Item template ResRef.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Stack count.
        /// </summary>
        public int StackSize { get; set; }

        /// <summary>
        /// Current charges (for items with uses).
        /// </summary>
        public int Charges { get; set; }

        /// <summary>
        /// Whether identified.
        /// </summary>
        public bool Identified { get; set; }

        /// <summary>
        /// Upgrades/modifications.
        /// </summary>
        public List<ItemUpgrade> Upgrades { get; set; }

        public ItemState()
        {
            StackSize = 1;
            Identified = true;
            Upgrades = new List<ItemUpgrade>();
        }
    }

    /// <summary>
    /// Item upgrade slot.
    /// </summary>
    public class ItemUpgrade
    {
        /// <summary>
        /// Upgrade slot type.
        /// </summary>
        public int UpgradeSlot { get; set; }

        /// <summary>
        /// Upgrade item ResRef.
        /// </summary>
        public string UpgradeResRef { get; set; }
    }

    /// <summary>
    /// Class level entry.
    /// </summary>
    public class ClassLevel
    {
        /// <summary>
        /// Class ID.
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>
        /// Levels in this class.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Powers gained at this class level.
        /// </summary>
        public List<string> PowersGained { get; set; }

        public ClassLevel()
        {
            PowersGained = new List<string>();
        }
    }

    /// <summary>
    /// D&amp;D-style attribute set.
    /// </summary>
    public class AttributeSet
    {
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        /// <summary>
        /// Gets attribute modifier.
        /// </summary>
        public int GetModifier(int attributeValue)
        {
            return (attributeValue - 10) / 2;
        }
    }

    /// <summary>
    /// Journal entry state.
    /// </summary>
    public class JournalEntry
    {
        /// <summary>
        /// Quest tag.
        /// </summary>
        public string QuestTag { get; set; }

        /// <summary>
        /// Current state.
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// When entry was added.
        /// </summary>
        public DateTime DateAdded { get; set; }
    }

    /// <summary>
    /// Feedback message.
    /// </summary>
    public class FeedbackMessage
    {
        public string Message { get; set; }
        public int Type { get; set; }
        public byte Color { get; set; }
    }

    /// <summary>
    /// Dialogue message.
    /// </summary>
    public class DialogueMessage
    {
        public string Speaker { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Combat message.
    /// </summary>
    public class CombatMessage
    {
        public string Message { get; set; }
        public int Type { get; set; }
        public byte Color { get; set; }
    }

    /// <summary>
    /// Faction reputation state (REPUTE.fac).
    /// </summary>
    /// <remarks>
    /// Faction Reputation Storage:
    /// - Based on swkotor2.exe: REPUTE.fac file format
    /// - FAC file format: GFF with "FAC " signature, contains FactionList and RepList
    /// - FactionList: List of Faction structs (FactionName: string, FactionGlobal: uint)
    /// - RepList: List of Reputation structs (FactionID1: uint, FactionID2: uint, FactionRep: uint)
    /// - Original implementation: Faction relationships stored in GFF structures with FactionID, FactionRep fields
    /// - Reference: vendor/xoreos/src/engines/nwn2/faction.cpp (loadFac method)
    /// - Reference: vendor/PyKotor/wiki/Bioware-Aurora-Faction.md (Faction Format documentation)
    /// - Reputation values: 0-100 range (0-10=hostile, 11-89=neutral, 90-100=friendly)
    /// </remarks>
    public class FactionReputationState
    {
        /// <summary>
        /// List of faction entries (faction ID -> faction name).
        /// </summary>
        /// <remarks>
        /// Faction names may be empty/unknown for some faction IDs.
        /// Names are primarily used for display/debugging purposes.
        /// </remarks>
        public Dictionary<int, string> FactionNames { get; set; }

        /// <summary>
        /// Faction global flags (faction ID -> global flag).
        /// </summary>
        /// <remarks>
        /// Global effect flag: 1 if all members of this faction immediately change reputation when reputation changes.
        /// </remarks>
        public Dictionary<int, bool> FactionGlobal { get; set; }

        /// <summary>
        /// Reputation entries (source faction ID -> target faction ID -> reputation value).
        /// </summary>
        /// <remarks>
        /// Reputation values: 0-100 range
        /// - 0-10: Hostile (will attack on sight)
        /// - 11-89: Neutral (will not attack, but not friendly)
        /// - 90-100: Friendly (allied, will assist in combat)
        /// </remarks>
        public Dictionary<int, Dictionary<int, int>> Reputations { get; set; }

        public FactionReputationState()
        {
            FactionNames = new Dictionary<int, string>();
            FactionGlobal = new Dictionary<int, bool>();
            Reputations = new Dictionary<int, Dictionary<int, int>>();
        }
    }
}
