using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;

namespace Odyssey.Core.Save
{
    /// <summary>
    /// Complete save game data.
    /// </summary>
    /// <remarks>
    /// Maps to KOTOR save structure:
    /// - NFO.res (save metadata)
    /// - GLOBALVARS.res (global variables)
    /// - PARTYTABLE.res (party state)
    /// - [module]_s.rim (per-module states)
    /// - Various GFF resources for entity states
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
        /// Journal entries.
        /// </summary>
        public List<JournalEntry> JournalEntries { get; set; }

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

        public SaveGameData()
        {
            AreaStates = new Dictionary<string, AreaState>();
            JournalEntries = new List<JournalEntry>();
            GameTime = new GameTime();
            GlobalVariables = new GlobalVariableState();
            PartyState = new PartyState();
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
        /// Influence values (K2 only).
        /// </summary>
        public Dictionary<string, int> Influence { get; set; }

        /// <summary>
        /// Current XP.
        /// </summary>
        public int ExperiencePoints { get; set; }

        public PartyState()
        {
            AvailableMembers = new Dictionary<string, PartyMemberState>();
            SelectedParty = new List<string>();
            Influence = new Dictionary<string, int>();
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
}
