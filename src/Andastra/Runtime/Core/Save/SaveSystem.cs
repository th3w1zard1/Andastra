using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Plot;

namespace Andastra.Runtime.Core.Save
{
    /// <summary>
    /// Save slot type.
    /// </summary>
    public enum SaveType
    {
        /// <summary>
        /// Manual save created by player.
        /// </summary>
        Manual,

        /// <summary>
        /// Automatic save created on area transition.
        /// </summary>
        Auto,

        /// <summary>
        /// Quick save slot.
        /// </summary>
        Quick
    }

    /// <summary>
    /// Manages save and load operations.
    /// </summary>
    /// <remarks>
    /// KOTOR Save System:
    /// - SAV files are ERF archives containing game state
    /// - Save contains: global vars, party state, inventory, module states
    /// - Each area visited has saved state (entity positions, door states, etc.)
    /// - Save overlay integrates into resource precedence chain
    ///
    /// Save Structure:
    /// - GLOBALVARS.res - Global variable state
    /// - PARTYTABLE.res - Party member list and selection
    /// - [module]_s.rim - Per-module state (positions, etc.)
    /// - NFO.res - Save metadata (name, time, screenshot)
    ///
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) save system implementation:
    /// - Main save function: SerializeSaveNfo @ 0x004eb750 (located via "savenfo" @ 0x007be1f0)
    /// - Save global variables: SaveGlobalVariables @ 0x005ac670 (located via "GLOBALVARS" @ 0x007c27bc)
    /// - Save party table: SavePartyTable @ 0x0057bd70 (located via "PARTYTABLE" @ 0x007c1910)
    /// - Load save function: FUN_00708990 @ 0x00708990 (located via "LoadSavegame" @ 0x007bdc90)
    /// - Auto-save function: FUN_004f0c50 @ 0x004f0c50
    /// - Load save metadata: FUN_00707290 @ 0x00707290
    /// </remarks>
    public class SaveSystem
    {
        private readonly IWorld _world;
        private readonly ISaveDataProvider _dataProvider;
        private object _globals; // IScriptGlobals - stored as object to avoid dependency
        private Party.PartySystem _partySystem; // PartySystem - stored as concrete type since both are in Andastra.Runtime.Core
        private PlotSystem _plotSystem; // PlotSystem - stored as concrete type since both are in Andastra.Runtime.Core
        private object _factionManager; // FactionManager - stored as object to avoid dependency on engine-specific implementation

        /// <summary>
        /// Currently loaded save data.
        /// </summary>
        public SaveGameData CurrentSave { get; private set; }

        /// <summary>
        /// Event fired when saving begins.
        /// </summary>
        public event Action<string> OnSaveBegin;

        /// <summary>
        /// Event fired when saving completes.
        /// </summary>
        public event Action<string, bool> OnSaveComplete;

        /// <summary>
        /// Event fired when loading begins.
        /// </summary>
        public event Action<string> OnLoadBegin;

        /// <summary>
        /// Event fired when loading completes.
        /// </summary>
        public event Action<string, bool> OnLoadComplete;

        public SaveSystem(IWorld world, ISaveDataProvider dataProvider)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _dataProvider = dataProvider ?? throw new ArgumentNullException("dataProvider");
        }

        /// <summary>
        /// Sets the script globals instance for saving/loading global variables.
        /// </summary>
        public void SetScriptGlobals(object globals)
        {
            _globals = globals;
        }

        /// <summary>
        /// Sets the party system instance for saving/loading party state.
        /// </summary>
        public void SetPartySystem(Party.PartySystem partySystem)
        {
            _partySystem = partySystem;
        }

        /// <summary>
        /// Sets the faction manager instance for saving/loading faction reputation state.
        /// </summary>
        public void SetFactionManager(object factionManager)
        {
            _factionManager = factionManager;
        }

        #region Save Operations

        /// <summary>
        /// Creates a save game.
        /// </summary>
        /// <param name="saveName">Name for the save.</param>
        /// <param name="saveType">Type of save.</param>
        /// <returns>True if save succeeded.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SerializeSaveNfo @ 0x004eb750
        /// Located via string reference: "savenfo" @ 0x007be1f0
        /// Original implementation:
        /// 1. Creates save directory "SAVES:\{saveName}"
        /// 2. Writes savenfo.res (GFF with "NFO " signature) containing:
        ///    - AREANAME: Current area name
        ///    - LASTMODULE: Last module ResRef
        ///    - TIMEPLAYED: Seconds played (uint32)
        ///    - CHEATUSED: Cheat used flag (bool)
        ///    - SAVEGAMENAME: Save game name string
        ///    - TIMESTAMP: System time (FILETIME structure)
        ///    - PCNAME: Player character name
        ///    - SAVENUMBER: Save slot number (uint32)
        ///    - GAMEPLAYHINT: Gameplay hint flag (bool)
        ///    - STORYHINT0-9: Story hint flags (bool array)
        ///    - LIVECONTENT: Live content flags (bitmask)
        ///    - LIVE1-9: Live content entries (string array)
        /// 3. Creates savegame.sav (ERF with "MOD V1.0" signature) containing:
        ///    - GLOBALVARS.res (global variable state)
        ///    - PARTYTABLE.res (party state)
        ///    - Module state files (entity positions, door/placeable states)
        /// 4. Progress updates at 5%, 10%, 15%, 20%, 25%, 30% completion milestones
        /// </remarks>
        public bool Save(string saveName, SaveType saveType = SaveType.Manual)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                return false;
            }

            if (OnSaveBegin != null)
            {
                OnSaveBegin(saveName);
            }

            try
            {
                SaveGameData saveData = CreateSaveData(saveName, saveType);
                bool success = _dataProvider.WriteSave(saveData);

                if (OnSaveComplete != null)
                {
                    OnSaveComplete(saveName, success);
                }

                return success;
            }
            catch (Exception)
            {
                if (OnSaveComplete != null)
                {
                    OnSaveComplete(saveName, false);
                }
                return false;
            }
        }

        /// <summary>
        /// Creates save data from current game state.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SerializeSaveNfo @ 0x004eb750
        /// Original implementation: Collects module info (current module, entry position/facing), game time (year/month/day/hour/minute),
        /// global variables (via SaveGlobalVariables @ 0x005ac670), party state (via SavePartyTable @ 0x0057bd70), and area states.
        /// Saves entity positions, HP, door/placeable states for current area.
        /// </remarks>
        private SaveGameData CreateSaveData(string saveName, SaveType saveType)
        {
            var saveData = new SaveGameData();
            saveData.Name = saveName;
            saveData.SaveType = saveType;
            saveData.SaveTime = DateTime.Now;

            // Save module info
            Andastra.Runtime.Core.Interfaces.IModule module = _world.CurrentModule;
            if (module != null)
            {
                saveData.CurrentModule = module.ResRef;
                saveData.EntryPosition = ((Core.Module.RuntimeModule)module).EntryPosition;
                saveData.EntryFacing = ((Core.Module.RuntimeModule)module).EntryFacing;
            }

            // Save time
            if (module != null)
            {
                saveData.GameTime = new GameTime
                {
                    Year = module.Year,
                    Month = module.Month,
                    Day = module.Day,
                    Hour = module.MinutesPastMidnight / 60,
                    Minute = module.MinutesPastMidnight % 60
                };
            }

            // Save global variables
            SaveGlobalVariables(saveData);

            // Save party state
            SavePartyState(saveData);

            // Save area states
            SaveAreaStates(saveData);

            // Save plot state
            SavePlotState(saveData);

            // Save faction reputation state
            SaveFactionReputation(saveData);

            return saveData;
        }

        // Save global variables to save data structure
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SaveGlobalVariables @ 0x005ac670
        // Located via string reference: "GLOBALVARS" @ 0x007c27bc
        // Original implementation: Constructs path "{savePath}\GLOBALVARS", writes GFF file containing all global int/bool/string variables
        // Uses reflection to access private dictionaries in ScriptGlobals (_globalInts, _globalBools, _globalStrings)
        private void SaveGlobalVariables(SaveGameData saveData)
        {
            saveData.GlobalVariables = new GlobalVariableState();

            if (_globals == null)
            {
                return;
            }

            // Use reflection to access private dictionaries in ScriptGlobals
            var globalsType = _globals.GetType();
            var globalIntsField = globalsType.GetField("_globalInts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var globalBoolsField = globalsType.GetField("_globalBools", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var globalStringsField = globalsType.GetField("_globalStrings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (globalIntsField != null)
            {
                var intsDict = globalIntsField.GetValue(_globals) as Dictionary<string, int>;
                if (intsDict != null)
                {
                    foreach (var kvp in intsDict)
                    {
                        saveData.GlobalVariables.Numbers[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (globalBoolsField != null)
            {
                var boolsDict = globalBoolsField.GetValue(_globals) as Dictionary<string, bool>;
                if (boolsDict != null)
                {
                    foreach (var kvp in boolsDict)
                    {
                        saveData.GlobalVariables.Booleans[kvp.Key] = kvp.Value;
                    }
                }
            }

            if (globalStringsField != null)
            {
                var stringsDict = globalStringsField.GetValue(_globals) as Dictionary<string, string>;
                if (stringsDict != null)
                {
                    foreach (var kvp in stringsDict)
                    {
                        saveData.GlobalVariables.Strings[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        // Save party member list and selection state
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SavePartyTable @ 0x0057bd70
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation: Writes GFF file with "PT  " signature (V2.0) containing party members, puppets, available NPCs,
        // influence values, gold, XP pool, solo mode flag, cheat used flag, and various game state flags
        // Constructs path "{savePath}\PARTYTABLE"
        private void SavePartyState(SaveGameData saveData)
        {
            saveData.PartyState = new PartyState();

            if (_partySystem == null)
            {
                return;
            }

            // Save party gold and XP
            saveData.PartyState.Gold = _partySystem.Gold;
            saveData.PartyState.ExperiencePoints = _partySystem.ExperiencePoints;
            saveData.PartyState.SoloMode = _partySystem.SoloMode;

            // Save player character state
            if (_partySystem.PlayerCharacter != null && _partySystem.PlayerCharacter.Entity != null)
            {
                saveData.PartyState.PlayerCharacter = CreateCreatureState(_partySystem.PlayerCharacter.Entity);
                saveData.PlayerName = _partySystem.PlayerCharacter.Entity.Tag;
            }

            // Save available party members
            foreach (Party.PartyMember member in _partySystem.AvailableMembers)
            {
                if (member.Entity != null)
                {
                    string templateResRef = member.TemplateResRef ?? member.Entity.Tag;
                    if (!string.IsNullOrEmpty(templateResRef))
                    {
                        var memberState = new PartyMemberState();
                        memberState.TemplateResRef = templateResRef;
                        memberState.State = CreateCreatureState(member.Entity);
                        memberState.IsAvailable = member.IsAvailable;
                        memberState.IsSelectable = member.IsSelectable;
                        saveData.PartyState.AvailableMembers[templateResRef] = memberState;
                    }
                }
            }

            // Save active party selection (by template ResRef)
            foreach (Party.PartyMember member in _partySystem.ActiveParty)
            {
                if (member.Entity != null)
                {
                    string templateResRef = member.TemplateResRef ?? member.Entity.Tag;
                    if (!string.IsNullOrEmpty(templateResRef) && !saveData.PartyState.SelectedParty.Contains(templateResRef))
                    {
                        saveData.PartyState.SelectedParty.Add(templateResRef);
                    }
                }
            }

            // Save leader (first in active party is leader)
            if (_partySystem.Leader != null && _partySystem.Leader.Entity != null)
            {
                saveData.PartyState.LeaderResRef = _partySystem.Leader.TemplateResRef ?? _partySystem.Leader.Entity.Tag;
            }
        }

        /// <summary>
        /// Saves plot state to save data.
        /// </summary>
        /// <remarks>
        /// Plot State Saving (swkotor2.exe):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Plot state is saved as part of game state
        /// - Original implementation: Plot states are tracked and saved to prevent duplicate processing
        /// - Plot state includes: plot index, label, triggered status, completed status, trigger count
        /// </remarks>
        private void SavePlotState(SaveGameData saveData)
        {
            saveData.PlotStates = new Dictionary<int, PlotState>();

            if (_plotSystem == null)
            {
                return;
            }

            // Save all plot states
            var allPlotStates = _plotSystem.GetAllPlotStates();
            foreach (var kvp in allPlotStates)
            {
                // Create a copy of the plot state for saving
                PlotState plotState = kvp.Value;
                saveData.PlotStates[kvp.Key] = new PlotState
                {
                    PlotIndex = plotState.PlotIndex,
                    Label = plotState.Label,
                    IsTriggered = plotState.IsTriggered,
                    IsCompleted = plotState.IsCompleted,
                    TriggerCount = plotState.TriggerCount,
                    LastTriggered = plotState.LastTriggered
                };
            }
        }

        /// <summary>
        /// Saves faction reputation state to save data.
        /// </summary>
        /// <remarks>
        /// Faction Reputation Saving (swkotor2.exe):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Faction reputation is saved as REPUTE.fac file in savegame.sav
        /// - Located via string reference: "REPUTE" @ (needs verification)
        /// - Original implementation: Faction relationships stored in GFF structures with FactionID, FactionRep fields
        /// - Faction reputation matrix: Dictionary&lt;sourceFaction, Dictionary&lt;targetFaction, reputation&gt;&gt;
        /// - Reputation values: 0-100 range (0-10=hostile, 11-89=neutral, 90-100=friendly)
        /// </remarks>
        private void SaveFactionReputation(SaveGameData saveData)
        {
            saveData.FactionReputation = new FactionReputationState();

            if (_factionManager == null)
            {
                return;
            }

            // Use reflection to access FactionManager's internal faction reputation dictionary
            // FactionManager has _factionReputation field: Dictionary&lt;int, Dictionary&lt;int, int&gt;&gt;
            var factionManagerType = _factionManager.GetType();
            var factionReputationField = factionManagerType.GetField("_factionReputation",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (factionReputationField == null)
            {
                return;
            }

            var factionReputation = factionReputationField.GetValue(_factionManager) as Dictionary<int, Dictionary<int, int>>;
            if (factionReputation == null)
            {
                return;
            }

            // Copy faction reputation matrix to save data
            foreach (var sourceKvp in factionReputation)
            {
                int sourceFaction = sourceKvp.Key;
                Dictionary<int, int> targetReps = sourceKvp.Value;

                if (targetReps == null)
                {
                    continue;
                }

                // Initialize source faction entry if not present
                if (!saveData.FactionReputation.Reputations.ContainsKey(sourceFaction))
                {
                    saveData.FactionReputation.Reputations[sourceFaction] = new Dictionary<int, int>();
                }

                // Copy reputation values
                foreach (var targetKvp in targetReps)
                {
                    int targetFaction = targetKvp.Key;
                    int reputation = targetKvp.Value;
                    saveData.FactionReputation.Reputations[sourceFaction][targetFaction] = reputation;
                }
            }

            // Note: Faction names and global flags are not stored in FactionManager
            // They are typically loaded from repute.2da or module data
            // We leave FactionNames and FactionGlobal empty, as they can be reconstructed from repute.2da on load
        }

        private void SaveAreaStates(SaveGameData saveData)
        {
            // Save state for each visited area
            saveData.AreaStates = new Dictionary<string, AreaState>();

            if (_world.CurrentArea != null)
            {
                AreaState areaState = CreateAreaState(_world.CurrentArea);
                saveData.AreaStates[_world.CurrentArea.ResRef] = areaState;
            }

            // Save module-to-area mapping for the current module
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mod_Area_list in module IFO file
            // Original implementation: Module IFO contains Mod_Area_list field (GFF List) with area ResRefs
            // Located via string references: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
            // This allows verification of area-to-module relationships without loading the module IFO
            Andastra.Runtime.Core.Interfaces.IModule module = _world.CurrentModule;
            if (module != null && !string.IsNullOrEmpty(module.ResRef))
            {
                // Initialize the mapping for this module if not already present
                if (!saveData.ModuleAreaMappings.ContainsKey(module.ResRef))
                {
                    saveData.ModuleAreaMappings[module.ResRef] = new List<string>();
                }

                // Get all areas from the module
                // For RuntimeModule, we can use the AreaList property directly
                // For other module types, we iterate through the Areas property
                var runtimeModule = module as Core.Module.RuntimeModule;
                if (runtimeModule != null && runtimeModule.AreaList != null)
                {
                    // Use the AreaList property which contains all area ResRefs from Mod_Area_list
                    foreach (string areaResRef in runtimeModule.AreaList)
                    {
                        if (!string.IsNullOrEmpty(areaResRef) &&
                            !saveData.ModuleAreaMappings[module.ResRef].Contains(areaResRef))
                        {
                            saveData.ModuleAreaMappings[module.ResRef].Add(areaResRef);
                        }
                    }
                }
                else
                {
                    // Fallback: iterate through Areas property to get area ResRefs
                    foreach (IArea area in module.Areas)
                    {
                        if (area != null && !string.IsNullOrEmpty(area.ResRef))
                        {
                            if (!saveData.ModuleAreaMappings[module.ResRef].Contains(area.ResRef))
                            {
                                saveData.ModuleAreaMappings[module.ResRef].Add(area.ResRef);
                            }
                        }
                    }
                }
            }
        }

        private AreaState CreateAreaState(IArea area)
        {
            var state = new AreaState();
            state.AreaResRef = area.ResRef;

            // Save entity positions and states
            foreach (IEntity creature in area.Creatures)
            {
                EntityState entityState = CreateEntityState(creature);
                state.CreatureStates.Add(entityState);
            }

            foreach (IEntity door in area.Doors)
            {
                EntityState entityState = CreateEntityState(door);
                state.DoorStates.Add(entityState);
            }

            foreach (IEntity placeable in area.Placeables)
            {
                EntityState entityState = CreateEntityState(placeable);
                state.PlaceableStates.Add(entityState);
            }

            return state;
        }

        private EntityState CreateEntityState(IEntity entity)
        {
            var state = new EntityState();
            state.Tag = entity.Tag;
            state.ObjectId = entity.ObjectId;
            state.ObjectType = entity.ObjectType;

            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                state.Position = transform.Position;
                state.Facing = transform.Facing;
            }

            Interfaces.Components.IStatsComponent stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                state.CurrentHP = stats.CurrentHP;
                state.MaxHP = stats.MaxHP;
            }

            // Save door state
            Interfaces.Components.IDoorComponent door = entity.GetComponent<Interfaces.Components.IDoorComponent>();
            if (door != null)
            {
                state.IsOpen = door.IsOpen;
                state.IsLocked = door.IsLocked;
            }

            // Save placeable state
            Interfaces.Components.IPlaceableComponent placeable = entity.GetComponent<Interfaces.Components.IPlaceableComponent>();
            if (placeable != null)
            {
                state.IsOpen = placeable.IsOpen;
                state.IsLocked = placeable.IsLocked;
            }

            return state;
        }

        /// <summary>
        /// Creates a creature state from an entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005226d0 @ 0x005226d0 (save creature data to GFF)
        /// Original implementation saves complete creature state including:
        /// - Position, orientation, HP, FP
        /// - Level, XP, ClassLevels, Skills, Attributes
        /// - Equipment (all slots), Inventory (all items)
        /// - KnownPowers (Force powers/spells), KnownFeats
        /// - Alignment and other creature-specific data
        /// Uses GFF format with fields for each data type
        /// </remarks>
        private CreatureState CreateCreatureState(IEntity entity)
        {
            var state = new CreatureState();
            state.Tag = entity.Tag;
            state.ObjectId = entity.ObjectId;
            state.ObjectType = entity.ObjectType;

            // Save transform (position and facing)
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                state.Position = transform.Position;
                state.Facing = transform.Facing;
            }

            // Save stats component data
            Interfaces.Components.IStatsComponent stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                state.CurrentHP = stats.CurrentHP;
                state.MaxHP = stats.MaxHP;
                state.CurrentFP = stats.CurrentFP;
                state.MaxFP = stats.MaxFP;
                state.Level = stats.Level;

                // Save attributes (STR, DEX, CON, INT, WIS, CHA)
                state.Attributes = new AttributeSet
                {
                    Strength = stats.GetAbility(Ability.Strength),
                    Dexterity = stats.GetAbility(Ability.Dexterity),
                    Constitution = stats.GetAbility(Ability.Constitution),
                    Intelligence = stats.GetAbility(Ability.Intelligence),
                    Wisdom = stats.GetAbility(Ability.Wisdom),
                    Charisma = stats.GetAbility(Ability.Charisma)
                };

                // Save all skills (iterate through skill IDs)
                // KOTOR has 8 skills: 0-7 (COMPUTER_USE, DEMOLITIONS, STEALTH, AWARENESS, PERSUADE, REPAIR, SECURITY, TREAT_INJURY)
                for (int skillId = 0; skillId < 8; skillId++)
                {
                    int skillRank = stats.GetSkillRank(skillId);
                    if (skillRank > 0)
                    {
                        // Use skill ID as string key (e.g., "0", "1", etc.)
                        state.Skills[skillId.ToString()] = skillRank;
                    }
                }

                // Save known spells/powers using reflection to access GetKnownSpells method if available
                // Based on Odyssey StatsComponent implementation which has GetKnownSpells()
                var statsType = stats.GetType();
                var getKnownSpellsMethod = statsType.GetMethod("GetKnownSpells", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (getKnownSpellsMethod != null)
                {
                    var knownSpells = getKnownSpellsMethod.Invoke(stats, null) as System.Collections.IEnumerable;
                    if (knownSpells != null)
                    {
                        foreach (object spellId in knownSpells)
                        {
                            if (spellId is int)
                            {
                                state.KnownPowers.Add(((int)spellId).ToString());
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: Check for spells using HasSpell method (iterate through spell IDs)
                    // This is less efficient but works if GetKnownSpells is not available
                    // Typical KOTOR spell ID range: 0-200 (approximate)
                    for (int spellId = 0; spellId < 500; spellId++)
                    {
                        if (stats.HasSpell(spellId))
                        {
                            state.KnownPowers.Add(spellId.ToString());
                        }
                    }
                }

                // Save XP using reflection to access Experience property if available
                // Based on Odyssey StatsComponent which has Experience property
                var experienceProperty = statsType.GetProperty("Experience", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (experienceProperty != null)
                {
                    object experienceValue = experienceProperty.GetValue(stats);
                    if (experienceValue is int)
                    {
                        state.XP = (int)experienceValue;
                    }
                }
            }

            // Save class levels using reflection to access CreatureComponent if available
            // Based on Odyssey CreatureComponent which has ClassList property
            var creatureComponentType = entity.GetType().Assembly.GetType("Andastra.Runtime.Engines.Odyssey.Components.CreatureComponent");
            if (creatureComponentType != null)
            {
                // Try to get CreatureComponent from entity using reflection
                var getComponentMethod = entity.GetType().GetMethod("GetComponent", new System.Type[] { typeof(System.Type) });
                if (getComponentMethod != null)
                {
                    object creatureComp = getComponentMethod.Invoke(entity, new object[] { creatureComponentType });
                    if (creatureComp != null)
                    {
                        // Get ClassList property
                        var classListProperty = creatureComponentType.GetProperty("ClassList");
                        if (classListProperty != null)
                        {
                            var classList = classListProperty.GetValue(creatureComp) as System.Collections.IEnumerable;
                            if (classList != null)
                            {
                                foreach (object cls in classList)
                                {
                                    var classType = cls.GetType();
                                    var classIdProperty = classType.GetProperty("ClassId");
                                    var levelProperty = classType.GetProperty("Level");
                                    if (classIdProperty != null && levelProperty != null)
                                    {
                                        object classId = classIdProperty.GetValue(cls);
                                        object level = levelProperty.GetValue(cls);
                                        if (classId is int && level is int)
                                        {
                                            state.ClassLevels.Add(new ClassLevel
                                            {
                                                ClassId = (int)classId,
                                                Level = (int)level
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        // Get FeatList property
                        var featListProperty = creatureComponentType.GetProperty("FeatList");
                        if (featListProperty != null)
                        {
                            var featList = featListProperty.GetValue(creatureComp) as System.Collections.IEnumerable;
                            if (featList != null)
                            {
                                foreach (object featId in featList)
                                {
                                    if (featId is int)
                                    {
                                        state.KnownFeats.Add(((int)featId).ToString());
                                    }
                                }
                            }
                        }

                        // Get KnownPowers property (additional to stats component)
                        var knownPowersProperty = creatureComponentType.GetProperty("KnownPowers");
                        if (knownPowersProperty != null)
                        {
                            var knownPowers = knownPowersProperty.GetValue(creatureComp) as System.Collections.IEnumerable;
                            if (knownPowers != null)
                            {
                                foreach (object powerId in knownPowers)
                                {
                                    if (powerId is int)
                                    {
                                        string powerIdStr = ((int)powerId).ToString();
                                        if (!state.KnownPowers.Contains(powerIdStr))
                                        {
                                            state.KnownPowers.Add(powerIdStr);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Save inventory and equipment
            Interfaces.Components.IInventoryComponent inventory = entity.GetComponent<Interfaces.Components.IInventoryComponent>();
            if (inventory != null)
            {
                // Save equipped items (KOTOR equipment slots)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Equipment slots saved in PARTYTABLE.res
                // Slot constants: INVENTORY_SLOT_HEAD=0, INVENTORY_SLOT_BODY=1, INVENTORY_SLOT_HANDS=3,
                // INVENTORY_SLOT_RIGHTWEAPON=4, INVENTORY_SLOT_LEFTWEAPON=5, INVENTORY_SLOT_LEFTARM=7,
                // INVENTORY_SLOT_RIGHTARM=8, INVENTORY_SLOT_IMPLANT=9, INVENTORY_SLOT_BELT=10
                IEntity headItem = inventory.GetItemInSlot(0); // INVENTORY_SLOT_HEAD
                if (headItem != null)
                {
                    state.Equipment.Head = CreateItemState(headItem);
                }

                IEntity armorItem = inventory.GetItemInSlot(1); // INVENTORY_SLOT_BODY
                if (armorItem != null)
                {
                    state.Equipment.Armor = CreateItemState(armorItem);
                }

                IEntity glovesItem = inventory.GetItemInSlot(3); // INVENTORY_SLOT_HANDS
                if (glovesItem != null)
                {
                    state.Equipment.Gloves = CreateItemState(glovesItem);
                }

                IEntity rightHandItem = inventory.GetItemInSlot(4); // INVENTORY_SLOT_RIGHTWEAPON
                if (rightHandItem != null)
                {
                    state.Equipment.RightHand = CreateItemState(rightHandItem);
                }

                IEntity leftHandItem = inventory.GetItemInSlot(5); // INVENTORY_SLOT_LEFTWEAPON
                if (leftHandItem != null)
                {
                    state.Equipment.LeftHand = CreateItemState(leftHandItem);
                }

                IEntity leftArmItem = inventory.GetItemInSlot(7); // INVENTORY_SLOT_LEFTARM
                if (leftArmItem != null)
                {
                    state.Equipment.LeftArm = CreateItemState(leftArmItem);
                }

                IEntity rightArmItem = inventory.GetItemInSlot(8); // INVENTORY_SLOT_RIGHTARM
                if (rightArmItem != null)
                {
                    state.Equipment.RightArm = CreateItemState(rightArmItem);
                }

                IEntity implantItem = inventory.GetItemInSlot(9); // INVENTORY_SLOT_IMPLANT
                if (implantItem != null)
                {
                    state.Equipment.Implant = CreateItemState(implantItem);
                }

                IEntity beltItem = inventory.GetItemInSlot(10); // INVENTORY_SLOT_BELT
                if (beltItem != null)
                {
                    state.Equipment.Belt = CreateItemState(beltItem);
                }

                // Save all inventory items (non-equipped items in inventory bag)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Inventory items saved in PARTYTABLE.res
                foreach (IEntity invItem in inventory.GetAllItems())
                {
                    // Skip items that are already saved as equipped items
                    if (invItem != headItem && invItem != armorItem && invItem != glovesItem &&
                        invItem != rightHandItem && invItem != leftHandItem && invItem != leftArmItem &&
                        invItem != rightArmItem && invItem != implantItem && invItem != beltItem)
                    {
                        ItemState itemState = CreateItemState(invItem);
                        state.Inventory.Add(itemState);
                    }
                }
            }

            // Save alignment using reflection to access Alignment property if available
            // Based on creature templates which may have Alignment property
            var alignmentProperty = entity.GetType().GetProperty("Alignment", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (alignmentProperty != null)
            {
                object alignmentValue = alignmentProperty.GetValue(entity);
                if (alignmentValue is int)
                {
                    state.Alignment = (int)alignmentValue;
                }
            }

            return state;
        }

        /// <summary>
        /// Creates an item state from an item entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item data saved in PARTYTABLE.res
        /// Saves item template ResRef, stack size, charges, identified flag, and upgrades
        /// </remarks>
        private ItemState CreateItemState(IEntity itemEntity)
        {
            var itemState = new ItemState();

            if (itemEntity == null)
            {
                return itemState;
            }

            // Get item component
            Interfaces.Components.IItemComponent itemComponent = itemEntity.GetComponent<Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                itemState.TemplateResRef = itemComponent.TemplateResRef ?? string.Empty;
                itemState.StackSize = itemComponent.StackSize;
                itemState.Charges = itemComponent.Charges;
                itemState.Identified = itemComponent.Identified;

                // Save item upgrades
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item upgrades saved in save files
                foreach (Interfaces.Components.ItemUpgrade upgrade in itemComponent.Upgrades)
                {
                    itemState.Upgrades.Add(new ItemUpgrade
                    {
                        UpgradeSlot = upgrade.UpgradeType,
                        UpgradeResRef = upgrade.Index.ToString() // Store index as ResRef string (may need adjustment based on actual upgrade format)
                    });
                }
            }

            return itemState;
        }

        #endregion

        #region Load Operations

        /// <summary>
        /// Loads a save game.
        /// </summary>
        /// <param name="saveName">Name of the save to load.</param>
        /// <returns>True if load succeeded.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00708990 @ 0x00708990
        /// Located via string reference: "LoadSavegame" @ 0x007bdc90 (also "savenfo" @ 0x007be1f0)
        /// Original implementation:
        /// 1. Reads savegame.sav ERF archive (signature "MOD V1.0")
        /// 2. Extracts and loads savenfo.res (GFF with "NFO " signature) for metadata
        /// 3. Extracts GLOBALVARS.res (see FUN_005ac740 @ 0x005ac740) and restores global variables
        /// 4. Extracts PARTYTABLE.res (see FUN_0057dcd0 @ 0x0057dcd0) and restores party state
        /// 5. Loads module state files (entity positions, door/placeable states)
        /// 6. Progress updates at 5%, 10%, 15%, 20%, 25%, 30%, 50% completion
        /// </remarks>
        public bool Load(string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                return false;
            }

            if (OnLoadBegin != null)
            {
                OnLoadBegin(saveName);
            }

            try
            {
                SaveGameData saveData = _dataProvider.ReadSave(saveName);
                if (saveData == null)
                {
                    if (OnLoadComplete != null)
                    {
                        OnLoadComplete(saveName, false);
                    }
                    return false;
                }

                bool success = ApplySaveData(saveData);
                CurrentSave = success ? saveData : null;

                if (OnLoadComplete != null)
                {
                    OnLoadComplete(saveName, success);
                }

                return success;
            }
            catch (Exception)
            {
                if (OnLoadComplete != null)
                {
                    OnLoadComplete(saveName, false);
                }
                return false;
            }
        }

        /// <summary>
        /// Applies loaded save data to the game state.
        /// </summary>
        private bool ApplySaveData(SaveGameData saveData)
        {
            // Restore global variables
            RestoreGlobalVariables(saveData);

            // Restore party state
            RestorePartyState(saveData);

            // Restore plot state
            RestorePlotState(saveData);

            // Restore faction reputation state
            RestoreFactionReputation(saveData);

            // Load module (area states are restored when areas are loaded)
            // This would trigger the module loader
            // The area states become a resource overlay

            return true;
        }

        // Restore global variables from save data
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005ac740 @ 0x005ac740
        // Located via string reference: "GLOBALVARS" @ 0x007c27bc
        // Original implementation: Reads GFF file from "SAVES:\{saveName}\GLOBALVARS", restores all global int/bool/string variables
        // Uses reflection to call SetGlobalInt, SetGlobalBool, SetGlobalString methods on ScriptGlobals
        private void RestoreGlobalVariables(SaveGameData saveData)
        {
            if (saveData.GlobalVariables == null || _globals == null)
            {
                return;
            }

            // Restore global variables to IScriptGlobals using reflection
            // This avoids a direct dependency on Odyssey.Scripting
            var globalsType = _globals.GetType();
            var setGlobalBoolMethod = globalsType.GetMethod("SetGlobalBool");
            var setGlobalIntMethod = globalsType.GetMethod("SetGlobalInt");
            var setGlobalStringMethod = globalsType.GetMethod("SetGlobalString");

            if (saveData.GlobalVariables.Booleans != null && setGlobalBoolMethod != null)
            {
                foreach (KeyValuePair<string, bool> kvp in saveData.GlobalVariables.Booleans)
                {
                    setGlobalBoolMethod.Invoke(_globals, new object[] { kvp.Key, kvp.Value });
                }
            }

            if (saveData.GlobalVariables.Numbers != null && setGlobalIntMethod != null)
            {
                foreach (KeyValuePair<string, int> kvp in saveData.GlobalVariables.Numbers)
                {
                    setGlobalIntMethod.Invoke(_globals, new object[] { kvp.Key, kvp.Value });
                }
            }

            if (saveData.GlobalVariables.Strings != null && setGlobalStringMethod != null)
            {
                foreach (KeyValuePair<string, string> kvp in saveData.GlobalVariables.Strings)
                {
                    setGlobalStringMethod.Invoke(_globals, new object[] { kvp.Key, kvp.Value });
                }
            }
        }

        // Restore party member list and selection state
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0057dcd0 @ 0x0057dcd0
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation: Reads GFF file with "PT  " signature, restores party members, puppets, available NPCs,
        // influence values, gold, XP pool, solo mode flag, and various game state flags
        private void RestorePartyState(SaveGameData saveData)
        {
            if (saveData.PartyState == null || _partySystem == null)
            {
                return;
            }

            // Restore party gold and XP
            _partySystem.Gold = saveData.PartyState.Gold;
            _partySystem.ExperiencePoints = saveData.PartyState.ExperiencePoints;
            _partySystem.SoloMode = saveData.PartyState.SoloMode;

            // Restore player character state
            if (saveData.PartyState.PlayerCharacter != null)
            {
                // Player character entity should already exist in world
                // We restore its state via entity components
                IEntity pcEntity = _world.GetEntityByTag(saveData.PartyState.PlayerCharacter.Tag);
                if (pcEntity != null)
                {
                    RestoreCreatureState(pcEntity, saveData.PartyState.PlayerCharacter);
                    _partySystem.SetPlayerCharacter(pcEntity);
                }
            }

            // Restore available party members
            foreach (var kvp in saveData.PartyState.AvailableMembers)
            {
                string templateResRef = kvp.Key;
                PartyMemberState memberState = kvp.Value;

                // Find entity by tag or create from template
                IEntity memberEntity = _world.GetEntityByTag(memberState.State.Tag);
                if (memberEntity != null)
                {
                    RestoreCreatureState(memberEntity, memberState.State);

                    // Add to available members (NPCSlot is int, use -1 to let PartySystem assign slot)
                    _partySystem.AddAvailableMember(memberEntity, -1);

                    // Set availability flags
                    _partySystem.SetAvailability(memberEntity, memberState.IsAvailable);
                    _partySystem.SetSelectability(memberEntity, memberState.IsSelectable);
                }
            }

            // Restore active party selection
            foreach (string templateResRef in saveData.PartyState.SelectedParty)
            {
                if (saveData.PartyState.AvailableMembers.TryGetValue(templateResRef, out PartyMemberState memberState))
                {
                    IEntity memberEntity = _world.GetEntityByTag(memberState.State.Tag);
                    if (memberEntity != null)
                    {
                        Party.PartyMember member = _partySystem.GetAvailableMemberByTag(memberEntity.Tag);
                        if (member != null)
                        {
                            _partySystem.AddToActiveParty(member);
                        }
                    }
                }
            }

            // Restore leader
            if (!string.IsNullOrEmpty(saveData.PartyState.LeaderResRef))
            {
                IEntity leaderEntity = _world.GetEntityByTag(saveData.PartyState.LeaderResRef);
                if (leaderEntity != null)
                {
                    Party.PartyMember leader = _partySystem.GetAvailableMemberByTag(leaderEntity.Tag);
                    if (leader == null && _partySystem.PlayerCharacter != null &&
                        _partySystem.PlayerCharacter.Entity.ObjectId == leaderEntity.ObjectId)
                    {
                        leader = _partySystem.PlayerCharacter;
                    }

                    if (leader != null)
                    {
                        // Find leader index in active party
                        int leaderIndex = -1;
                        for (int i = 0; i < _partySystem.ActiveParty.Count; i++)
                        {
                            if (_partySystem.ActiveParty[i] == leader)
                            {
                                leaderIndex = i;
                                break;
                            }
                        }
                        if (leaderIndex >= 0)
                        {
                            _partySystem.SetLeader(leaderIndex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restores creature state to an entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0057dcd0 @ 0x0057dcd0 (load party data from PARTYTABLE.res)
        /// Original implementation restores complete creature state including:
        /// - Position, orientation, HP, FP
        /// - Level, XP, ClassLevels, Skills, Attributes
        /// - Equipment (all slots), Inventory (all items)
        /// - KnownPowers (Force powers/spells), KnownFeats
        /// - Alignment and other creature-specific data
        /// </remarks>
        private void RestoreCreatureState(IEntity entity, CreatureState state)
        {
            if (entity == null || state == null)
            {
                return;
            }

            // Restore transform (position and facing)
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = state.Position;
                transform.Facing = state.Facing;
            }

            // Restore stats component data
            Interfaces.Components.IStatsComponent stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                stats.CurrentHP = state.CurrentHP;
                stats.MaxHP = state.MaxHP;
                stats.CurrentFP = state.CurrentFP;
                stats.MaxFP = state.MaxFP;

                // Restore level using reflection to set Level property if available
                var statsType = stats.GetType();
                var levelProperty = statsType.GetProperty("Level", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (levelProperty != null && levelProperty.CanWrite)
                {
                    levelProperty.SetValue(stats, state.Level);
                }

                // Restore attributes (STR, DEX, CON, INT, WIS, CHA)
                if (state.Attributes != null)
                {
                    stats.SetAbility(Ability.Strength, state.Attributes.Strength);
                    stats.SetAbility(Ability.Dexterity, state.Attributes.Dexterity);
                    stats.SetAbility(Ability.Constitution, state.Attributes.Constitution);
                    stats.SetAbility(Ability.Intelligence, state.Attributes.Intelligence);
                    stats.SetAbility(Ability.Wisdom, state.Attributes.Wisdom);
                    stats.SetAbility(Ability.Charisma, state.Attributes.Charisma);
                }

                // Restore skills using reflection to access SetSkillRank method if available
                // Based on Odyssey StatsComponent which has SetSkillRank method
                var setSkillRankMethod = statsType.GetMethod("SetSkillRank", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (setSkillRankMethod != null && state.Skills != null)
                {
                    foreach (var kvp in state.Skills)
                    {
                        if (int.TryParse(kvp.Key, out int skillId))
                        {
                            setSkillRankMethod.Invoke(stats, new object[] { skillId, kvp.Value });
                        }
                    }
                }

                // Restore XP using reflection to set Experience property if available
                var experienceProperty = statsType.GetProperty("Experience", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (experienceProperty != null && experienceProperty.CanWrite)
                {
                    experienceProperty.SetValue(stats, state.XP);
                }

                // Restore known spells/powers using reflection to access AddSpell method if available
                // Based on Odyssey StatsComponent which has AddSpell method
                var addSpellMethod = statsType.GetMethod("AddSpell", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (addSpellMethod != null && state.KnownPowers != null)
                {
                    foreach (string powerIdStr in state.KnownPowers)
                    {
                        if (int.TryParse(powerIdStr, out int powerId))
                        {
                            addSpellMethod.Invoke(stats, new object[] { powerId });
                        }
                    }
                }
            }

            // Restore class levels using reflection to access CreatureComponent if available
            // Based on Odyssey CreatureComponent which has ClassList property
            var creatureComponentType = entity.GetType().Assembly.GetType("Andastra.Runtime.Engines.Odyssey.Components.CreatureComponent");
            if (creatureComponentType != null)
            {
                // Try to get CreatureComponent from entity using reflection
                var getComponentMethod = entity.GetType().GetMethod("GetComponent", new System.Type[] { typeof(System.Type) });
                if (getComponentMethod != null)
                {
                    object creatureComp = getComponentMethod.Invoke(entity, new object[] { creatureComponentType });
                    if (creatureComp != null && state.ClassLevels != null)
                    {
                        // Get ClassList property and restore class levels
                        var classListProperty = creatureComponentType.GetProperty("ClassList");
                        if (classListProperty != null)
                        {
                            var classList = classListProperty.GetValue(creatureComp) as System.Collections.IList;
                            if (classList != null)
                            {
                                classList.Clear();
                                var classType = System.Type.GetType("Andastra.Runtime.Engines.Odyssey.Components.CreatureClass");
                                if (classType != null)
                                {
                                    foreach (ClassLevel classLevel in state.ClassLevels)
                                    {
                                        object cls = System.Activator.CreateInstance(classType);
                                        var classIdProperty = classType.GetProperty("ClassId");
                                        var levelProperty = classType.GetProperty("Level");
                                        if (classIdProperty != null && levelProperty != null)
                                        {
                                            classIdProperty.SetValue(cls, classLevel.ClassId);
                                            levelProperty.SetValue(cls, classLevel.Level);
                                            classList.Add(cls);
                                        }
                                    }
                                }
                            }
                        }

                        // Get FeatList property and restore feats
                        if (state.KnownFeats != null)
                        {
                            var featListProperty = creatureComponentType.GetProperty("FeatList");
                            if (featListProperty != null)
                            {
                                var featList = featListProperty.GetValue(creatureComp) as System.Collections.IList;
                                if (featList != null)
                                {
                                    featList.Clear();
                                    foreach (string featIdStr in state.KnownFeats)
                                    {
                                        if (int.TryParse(featIdStr, out int featId))
                                        {
                                            featList.Add(featId);
                                        }
                                    }
                                }
                            }
                        }

                        // Get KnownPowers property and restore powers (additional to stats component)
                        if (state.KnownPowers != null)
                        {
                            var knownPowersProperty = creatureComponentType.GetProperty("KnownPowers");
                            if (knownPowersProperty != null)
                            {
                                var knownPowers = knownPowersProperty.GetValue(creatureComp) as System.Collections.IList;
                                if (knownPowers != null)
                                {
                                    knownPowers.Clear();
                                    foreach (string powerIdStr in state.KnownPowers)
                                    {
                                        if (int.TryParse(powerIdStr, out int powerId))
                                        {
                                            knownPowers.Add(powerId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Restore inventory and equipment
            Interfaces.Components.IInventoryComponent inventory = entity.GetComponent<Interfaces.Components.IInventoryComponent>();
            if (inventory != null)
            {
                // Restore equipped items (KOTOR equipment slots)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Equipment slots restored from PARTYTABLE.res
                if (state.Equipment != null)
                {
                    if (state.Equipment.Head != null)
                    {
                        IEntity headItem = RestoreItemFromState(state.Equipment.Head);
                        if (headItem != null)
                        {
                            inventory.SetItemInSlot(0, headItem); // INVENTORY_SLOT_HEAD
                        }
                    }

                    if (state.Equipment.Armor != null)
                    {
                        IEntity armorItem = RestoreItemFromState(state.Equipment.Armor);
                        if (armorItem != null)
                        {
                            inventory.SetItemInSlot(1, armorItem); // INVENTORY_SLOT_BODY
                        }
                    }

                    if (state.Equipment.Gloves != null)
                    {
                        IEntity glovesItem = RestoreItemFromState(state.Equipment.Gloves);
                        if (glovesItem != null)
                        {
                            inventory.SetItemInSlot(3, glovesItem); // INVENTORY_SLOT_HANDS
                        }
                    }

                    if (state.Equipment.RightHand != null)
                    {
                        IEntity rightHandItem = RestoreItemFromState(state.Equipment.RightHand);
                        if (rightHandItem != null)
                        {
                            inventory.SetItemInSlot(4, rightHandItem); // INVENTORY_SLOT_RIGHTWEAPON
                        }
                    }

                    if (state.Equipment.LeftHand != null)
                    {
                        IEntity leftHandItem = RestoreItemFromState(state.Equipment.LeftHand);
                        if (leftHandItem != null)
                        {
                            inventory.SetItemInSlot(5, leftHandItem); // INVENTORY_SLOT_LEFTWEAPON
                        }
                    }

                    if (state.Equipment.LeftArm != null)
                    {
                        IEntity leftArmItem = RestoreItemFromState(state.Equipment.LeftArm);
                        if (leftArmItem != null)
                        {
                            inventory.SetItemInSlot(7, leftArmItem); // INVENTORY_SLOT_LEFTARM
                        }
                    }

                    if (state.Equipment.RightArm != null)
                    {
                        IEntity rightArmItem = RestoreItemFromState(state.Equipment.RightArm);
                        if (rightArmItem != null)
                        {
                            inventory.SetItemInSlot(8, rightArmItem); // INVENTORY_SLOT_RIGHTARM
                        }
                    }

                    if (state.Equipment.Implant != null)
                    {
                        IEntity implantItem = RestoreItemFromState(state.Equipment.Implant);
                        if (implantItem != null)
                        {
                            inventory.SetItemInSlot(9, implantItem); // INVENTORY_SLOT_IMPLANT
                        }
                    }

                    if (state.Equipment.Belt != null)
                    {
                        IEntity beltItem = RestoreItemFromState(state.Equipment.Belt);
                        if (beltItem != null)
                        {
                            inventory.SetItemInSlot(10, beltItem); // INVENTORY_SLOT_BELT
                        }
                    }
                }

                // Restore inventory items (non-equipped items in inventory bag)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Inventory items restored from PARTYTABLE.res
                if (state.Inventory != null)
                {
                    foreach (ItemState itemState in state.Inventory)
                    {
                        IEntity invItem = RestoreItemFromState(itemState);
                        if (invItem != null)
                        {
                            inventory.AddItem(invItem);
                        }
                    }
                }
            }

            // Restore alignment using reflection to set Alignment property if available
            if (state.Alignment != 0)
            {
                var alignmentProperty = entity.GetType().GetProperty("Alignment", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (alignmentProperty != null && alignmentProperty.CanWrite)
                {
                    alignmentProperty.SetValue(entity, state.Alignment);
                }
            }
        }

        /// <summary>
        /// Restores an item entity from item state.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item data restored from PARTYTABLE.res
        /// Creates item entity from template ResRef and restores stack size, charges, identified flag, and upgrades
        /// Uses EntityFactory to create items from templates (via reflection to avoid direct dependency)
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_0057dcd0 @ 0x0057dcd0 (load party data from PARTYTABLE.res)
        /// Original implementation: Items restored from save files with all properties (StackSize, Charges, Identified, Upgrades)
        /// </remarks>
        private IEntity RestoreItemFromState(ItemState itemState)
        {
            if (itemState == null || string.IsNullOrEmpty(itemState.TemplateResRef))
            {
                return null;
            }

            // Get current module - needed for EntityFactory to load item templates
            IModule currentModule = _world.CurrentModule;
            if (currentModule == null)
            {
                return null; // Cannot create items without module context
            }

            // Get EntityFactory using reflection to avoid direct dependency on Odyssey namespace
            // Based on Odyssey EntityFactory pattern: CreateItemFromTemplate(Module, string, Vector3, float)
            // EntityFactory is typically available through ModuleLoader or GameSession
            // Try to get EntityFactory from world or module using reflection
            object entityFactory = GetEntityFactory();
            if (entityFactory == null)
            {
                return null; // EntityFactory not available
            }

            // Get CreateItemFromTemplate method from EntityFactory
            var entityFactoryType = entityFactory.GetType();
            var createItemMethod = entityFactoryType.GetMethod("CreateItemFromTemplate",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (createItemMethod == null)
            {
                return null; // CreateItemFromTemplate method not found
            }

            // Convert IModule to parsing Module type if needed
            // EntityFactory.CreateItemFromTemplate expects BioWare.NET.Extract.Installation.Module
            object parsingModule = GetParsingModule(currentModule);
            if (parsingModule == null)
            {
                return null; // Cannot convert IModule to parsing Module type
            }

            // Create item entity at origin (position doesn't matter for inventory items)
            // Items in inventory don't need a world position
            System.Numerics.Vector3 itemPosition = System.Numerics.Vector3.Zero;
            float itemFacing = 0.0f;

            // Call EntityFactory.CreateItemFromTemplate
            IEntity itemEntity = null;
            try
            {
                object result = createItemMethod.Invoke(entityFactory, new object[] { parsingModule, itemState.TemplateResRef, itemPosition, itemFacing });
                itemEntity = result as IEntity;
            }
            catch (Exception)
            {
                // Failed to create item - return null
                return null;
            }

            if (itemEntity == null)
            {
                return null; // Item creation failed
            }

            // Restore item properties from saved state
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item properties restored from PARTYTABLE.res
            Interfaces.Components.IItemComponent itemComponent = itemEntity.GetComponent<Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                // Restore StackSize
                itemComponent.StackSize = itemState.StackSize;

                // Restore Charges
                itemComponent.Charges = itemState.Charges;

                // Restore Identified flag
                itemComponent.Identified = itemState.Identified;

                // Restore item upgrades
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item upgrades saved in PARTYTABLE.res
                // Upgrades are stored as ItemUpgrade structures with UpgradeSlot and UpgradeResRef
                if (itemState.Upgrades != null && itemState.Upgrades.Count > 0)
                {
                    // Clear existing upgrades first
                    var upgradesList = itemComponent.Upgrades;
                    if (upgradesList != null)
                    {
                        // Get the list type to clear it
                        var listType = upgradesList.GetType();
                        var clearMethod = listType.GetMethod("Clear");
                        if (clearMethod != null)
                        {
                            clearMethod.Invoke(upgradesList, null);
                        }

                        // Restore each upgrade
                        foreach (ItemUpgrade savedUpgrade in itemState.Upgrades)
                        {
                            // Create ItemUpgrade structure
                            // Based on Interfaces.Components.ItemUpgrade structure
                            var upgradeType = typeof(Interfaces.Components.ItemUpgrade);
                            object upgrade = System.Activator.CreateInstance(upgradeType);

                            // Set upgrade properties using reflection
                            var upgradeTypeProperty = upgradeType.GetProperty("UpgradeType");
                            var indexProperty = upgradeType.GetProperty("Index");

                            if (upgradeTypeProperty != null && upgradeTypeProperty.CanWrite)
                            {
                                upgradeTypeProperty.SetValue(upgrade, savedUpgrade.UpgradeSlot);
                            }

                            if (indexProperty != null && indexProperty.CanWrite)
                            {
                                // UpgradeResRef is stored as string, convert to int if needed
                                if (int.TryParse(savedUpgrade.UpgradeResRef, out int upgradeIndex))
                                {
                                    indexProperty.SetValue(upgrade, upgradeIndex);
                                }
                            }

                            // Add upgrade to item component
                            // ItemComponent.Upgrades is a collection, use Add method
                            var addMethod = listType.GetMethod("Add");
                            if (addMethod != null)
                            {
                                addMethod.Invoke(upgradesList, new object[] { upgrade });
                            }
                        }
                    }
                }
            }

            return itemEntity;
        }

        /// <summary>
        /// Gets EntityFactory using reflection to avoid direct dependency.
        /// </summary>
        /// <returns>EntityFactory instance or null if not available.</returns>
        private object GetEntityFactory()
        {
            // Try to get EntityFactory from world using reflection
            // EntityFactory is typically available through ModuleLoader or GameSession
            // Check if world has EntityFactory property or method
            var worldType = _world.GetType();

            // Try EntityFactory property
            var entityFactoryProperty = worldType.GetProperty("EntityFactory",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (entityFactoryProperty != null)
            {
                return entityFactoryProperty.GetValue(_world);
            }

            // Try GetEntityFactory method
            var getEntityFactoryMethod = worldType.GetMethod("GetEntityFactory",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (getEntityFactoryMethod != null)
            {
                return getEntityFactoryMethod.Invoke(_world, null);
            }

            // Try to get from CurrentModule if it has EntityFactory
            if (_world.CurrentModule != null)
            {
                var moduleType = _world.CurrentModule.GetType();
                var moduleEntityFactoryProperty = moduleType.GetProperty("EntityFactory",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (moduleEntityFactoryProperty != null)
                {
                    return moduleEntityFactoryProperty.GetValue(_world.CurrentModule);
                }
            }

            // Fallback: Create new EntityFactory instance
            // Based on Odyssey EntityFactory: new Loading.EntityFactory()
            // Use reflection to create instance to avoid direct dependency
            var entityFactoryTypeName = "Andastra.Runtime.Games.Odyssey.Loading.EntityFactory";
            var entityFactoryType = System.Type.GetType(entityFactoryTypeName);
            if (entityFactoryType != null)
            {
                try
                {
                    return System.Activator.CreateInstance(entityFactoryType);
                }
                catch (Exception)
                {
                    // Failed to create EntityFactory
                }
            }

            return null;
        }

        /// <summary>
        /// Gets parsing Module from IModule interface.
        /// </summary>
        /// <returns>Parsing Module instance or null if conversion not possible.</returns>
        private object GetParsingModule(Andastra.Runtime.Core.Interfaces.IModule module)
        {
            if (module == null)
            {
                return null;
            }

            // If module is already the parsing Module type, return it
            var parsingModuleTypeName = "BioWare.NET.Extract.Installation.Module";
            var parsingModuleType = System.Type.GetType(parsingModuleTypeName);
            if (parsingModuleType != null && parsingModuleType.IsAssignableFrom(module.GetType()))
            {
                return module;
            }

            // Try to get parsing Module from IModule using reflection
            // Some IModule implementations may have a ParsingModule property
            var moduleType = module.GetType();
            var parsingModuleProperty = moduleType.GetProperty("ParsingModule",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (parsingModuleProperty != null)
            {
                return parsingModuleProperty.GetValue(module);
            }

            // Try to get Module property (some implementations expose it directly)
            var moduleProperty = moduleType.GetProperty("Module",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (moduleProperty != null)
            {
                object moduleValue = moduleProperty.GetValue(module);
                if (parsingModuleType != null && parsingModuleType.IsAssignableFrom(moduleValue.GetType()))
                {
                    return moduleValue;
                }
            }

            // For RuntimeModule, try to get ParsingModule field
            // Based on Core.Module.RuntimeModule which may have a ParsingModule field
            var parsingModuleField = moduleType.GetField("_parsingModule",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (parsingModuleField == null)
            {
                parsingModuleField = moduleType.GetField("ParsingModule",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }
            if (parsingModuleField != null)
            {
                return parsingModuleField.GetValue(module);
            }

            // If module itself is the parsing Module, return it
            if (parsingModuleType != null && parsingModuleType.IsInstanceOfType(module))
            {
                return module;
            }

            return null;
        }

        /// <summary>
        /// Restores plot state from save data.
        /// </summary>
        /// <remarks>
        /// Plot State Restoration (swkotor2.exe):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Plot state is restored from save data
        /// - Original implementation: Plot states are restored to prevent duplicate processing
        /// - Plot state includes: plot index, label, triggered status, completed status, trigger count
        /// </remarks>
        private void RestorePlotState(SaveGameData saveData)
        {
            if (saveData.PlotStates == null || _plotSystem == null)
            {
                return;
            }

            // Clear existing plot states
            _plotSystem.Clear();

            // Restore all plot states
            foreach (var kvp in saveData.PlotStates)
            {
                PlotState plotState = kvp.Value;

                // Register the plot
                _plotSystem.RegisterPlot(plotState.PlotIndex, plotState.Label);

                // Restore plot state
                PlotState restoredState = _plotSystem.GetPlotState(plotState.PlotIndex);
                if (restoredState != null)
                {
                    restoredState.IsTriggered = plotState.IsTriggered;
                    restoredState.IsCompleted = plotState.IsCompleted;
                    restoredState.TriggerCount = plotState.TriggerCount;
                    restoredState.LastTriggered = plotState.LastTriggered;
                }
            }
        }

        /// <summary>
        /// Restores faction reputation state from save data.
        /// </summary>
        /// <remarks>
        /// Faction Reputation Restoration (swkotor2.exe):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Faction reputation is restored from REPUTE.fac file in savegame.sav
        /// - Original implementation: Faction relationships restored from GFF structures with FactionID, FactionRep fields
        /// - Faction reputation matrix: Dictionary&lt;sourceFaction, Dictionary&lt;targetFaction, reputation&gt;&gt;
        /// - Reputation values: 0-100 range (0-10=hostile, 11-89=neutral, 90-100=friendly)
        /// </remarks>
        private void RestoreFactionReputation(SaveGameData saveData)
        {
            if (saveData.FactionReputation == null || _factionManager == null)
            {
                return;
            }

            // Use reflection to access FactionManager's SetFactionReputation method
            // FactionManager has SetFactionReputation(int sourceFaction, int targetFaction, int reputation) method
            var factionManagerType = _factionManager.GetType();
            var setFactionReputationMethod = factionManagerType.GetMethod("SetFactionReputation",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (setFactionReputationMethod == null)
            {
                return;
            }

            // Restore all faction reputation entries
            if (saveData.FactionReputation.Reputations != null)
            {
                foreach (var sourceKvp in saveData.FactionReputation.Reputations)
                {
                    int sourceFaction = sourceKvp.Key;
                    Dictionary<int, int> targetReps = sourceKvp.Value;

                    if (targetReps == null)
                    {
                        continue;
                    }

                    foreach (var targetKvp in targetReps)
                    {
                        int targetFaction = targetKvp.Key;
                        int reputation = targetKvp.Value;

                        // Call SetFactionReputation method via reflection
                        setFactionReputationMethod.Invoke(_factionManager, new object[] { sourceFaction, targetFaction, reputation });
                    }
                }
            }
        }

        /// <summary>
        /// Gets the area state for a specific area from the current save.
        /// </summary>
        public AreaState GetAreaState(string areaResRef)
        {
            if (CurrentSave == null || CurrentSave.AreaStates == null)
            {
                return null;
            }

            AreaState state;
            if (CurrentSave.AreaStates.TryGetValue(areaResRef, out state))
            {
                return state;
            }

            return null;
        }

        #endregion

        #region Save Management

        /// <summary>
        /// Gets all available saves.
        /// </summary>
        public IEnumerable<SaveGameInfo> GetSaveList()
        {
            return _dataProvider.EnumerateSaves();
        }

        /// <summary>
        /// Deletes a save.
        /// </summary>
        public bool DeleteSave(string saveName)
        {
            return _dataProvider.DeleteSave(saveName);
        }

        /// <summary>
        /// Checks if a save exists.
        /// </summary>
        public bool SaveExists(string saveName)
        {
            return _dataProvider.SaveExists(saveName);
        }

        #endregion

        #region Module State Management

        /// <summary>
        /// Stores module state for runtime persistence (not in save file).
        /// Module states persist across module transitions within a game session.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state persistence
        /// Original implementation: Module states cached in memory during gameplay
        /// States are saved to save files when Save() is called
        /// </remarks>
        public void StoreModuleState(string moduleResRef, Module.ModuleState moduleState)
        {
            if (string.IsNullOrEmpty(moduleResRef) || moduleState == null)
            {
                return;
            }

            // Store in current save's area states if available
            if (CurrentSave != null)
            {
                if (CurrentSave.AreaStates == null)
                {
                    CurrentSave.AreaStates = new Dictionary<string, AreaState>();
                }

                // Convert ModuleState to AreaState for storage
                // Module states are per-module, but we store them as area states
                // since modules contain areas
                if (_world.CurrentArea != null)
                {
                    AreaState areaState = CreateAreaStateFromModuleState(moduleState);
                    CurrentSave.AreaStates[_world.CurrentArea.ResRef] = areaState;
                }
            }
        }

        /// <summary>
        /// Checks if module state exists for the given module.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state validation
        /// Original implementation: Verifies that area states belong to the specified module
        /// by checking if areas are present in the module's Mod_Area_list
        /// </remarks>
        public bool HasModuleState(string moduleResRef)
        {
            if (string.IsNullOrEmpty(moduleResRef))
            {
                return false;
            }

            // Check if we have area states for this module
            if (CurrentSave != null && CurrentSave.AreaStates != null)
            {
                // Module states are stored as area states
                // Check if any area in the current save belongs to this module
                foreach (string areaResRef in CurrentSave.AreaStates.Keys)
                {
                    if (DoesAreaBelongToModule(areaResRef, moduleResRef))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an area belongs to a module.
        /// </summary>
        /// <param name="areaResRef">The resource reference of the area to check.</param>
        /// <param name="moduleResRef">The resource reference of the module.</param>
        /// <returns>True if the area belongs to the module, false otherwise.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area-to-module relationship validation
        /// Original implementation: Checks if area ResRef exists in module's Mod_Area_list
        /// Located via string references: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
        /// Module IFO file contains Mod_Area_list field (GFF List) with area ResRefs
        /// This method verifies the relationship by checking if the module contains the area
        ///
        /// Cross-engine analysis:
        /// - Odyssey (swkotor.exe, swkotor2.exe): Mod_Area_list in IFO GFF file
        /// - Aurora (nwmain.exe): Similar area list in Module.ifo
        /// - Eclipse (daorigins.exe, DragonAge2.exe): Area list in module definition
        /// - Infinity (, ): Level area associations
        ///
        /// Common pattern: All engines maintain a list of areas that belong to each module.
        /// This method uses IModule.GetArea() to check if the area exists in the module,
        /// which internally checks the module's area list.
        /// </remarks>
        private bool DoesAreaBelongToModule(string areaResRef, string moduleResRef)
        {
            if (string.IsNullOrEmpty(areaResRef) || string.IsNullOrEmpty(moduleResRef))
            {
                return false;
            }

            // Get the module to check
            Andastra.Runtime.Core.Interfaces.IModule module = null;

            // First, check if the current module matches
            if (_world.CurrentModule != null &&
                string.Equals(_world.CurrentModule.ResRef, moduleResRef, StringComparison.OrdinalIgnoreCase))
            {
                module = _world.CurrentModule;
            }
            else
            {
                // Module is not currently loaded - use saved module-to-area mapping
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Mod_Area_list in module IFO file
                // Original implementation: Module IFO contains Mod_Area_list field (GFF List) with area ResRefs
                // Located via string references: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
                // The mapping is populated when saving by extracting the area list from the module
                if (CurrentSave != null && CurrentSave.ModuleAreaMappings != null)
                {
                    List<string> areaList;
                    if (CurrentSave.ModuleAreaMappings.TryGetValue(moduleResRef, out areaList))
                    {
                        // Check if the area ResRef is in the module's area list
                        // Use case-insensitive comparison to match original game behavior
                        foreach (string mappedAreaResRef in areaList)
                        {
                            if (string.Equals(mappedAreaResRef, areaResRef, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }

                // Module is not loaded and we don't have a saved mapping for it
                // This can happen if the save was created before the mapping was implemented,
                // or if the module was never visited during the save session
                return false;
            }

            if (module == null)
            {
                return false;
            }

            // Check if the module contains this area
            // IModule.GetArea() returns null if the area is not in the module
            IArea area = module.GetArea(areaResRef);
            return area != null;
        }

        /// <summary>
        /// Gets module state for the given module.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state retrieval
        /// Original implementation: Retrieves module state by verifying that the current area
        /// belongs to the specified module before returning the state
        /// </remarks>
        public Module.ModuleState GetModuleState(string moduleResRef)
        {
            if (string.IsNullOrEmpty(moduleResRef))
            {
                return null;
            }

            // Convert AreaState back to ModuleState
            // First verify that the current area belongs to the requested module
            if (CurrentSave != null && CurrentSave.AreaStates != null && _world.CurrentArea != null)
            {
                string currentAreaResRef = _world.CurrentArea.ResRef;

                // Verify that the current area belongs to the requested module
                if (!DoesAreaBelongToModule(currentAreaResRef, moduleResRef))
                {
                    // Current area does not belong to the requested module
                    // Try to find any area state that belongs to this module
                    foreach (var kvp in CurrentSave.AreaStates)
                    {
                        if (DoesAreaBelongToModule(kvp.Key, moduleResRef))
                        {
                            return CreateModuleStateFromAreaState(kvp.Value);
                        }
                    }

                    // No area states found that belong to this module
                    return null;
                }

                // Current area belongs to the module, return its state
                AreaState areaState;
                if (CurrentSave.AreaStates.TryGetValue(currentAreaResRef, out areaState))
                {
                    return CreateModuleStateFromAreaState(areaState);
                }
            }

            return null;
        }

        /// <summary>
        /// Converts ModuleState to AreaState for storage.
        /// </summary>
        private AreaState CreateAreaStateFromModuleState(Module.ModuleState moduleState)
        {
            var areaState = new AreaState();
            if (_world.CurrentArea != null)
            {
                areaState.AreaResRef = _world.CurrentArea.ResRef;
            }

            // Convert creature states
            foreach (Module.CreatureState creatureState in moduleState.Creatures)
            {
                var entityState = new EntityState
                {
                    Tag = creatureState.Tag,
                    Position = creatureState.Position,
                    Facing = creatureState.Facing,
                    CurrentHP = creatureState.CurrentHP,
                    MaxHP = creatureState.CurrentHP, // Use CurrentHP as fallback
                    ObjectType = ObjectType.Creature
                };
                areaState.CreatureStates.Add(entityState);
            }

            // Convert door states
            foreach (Module.DoorState doorState in moduleState.Doors)
            {
                var entityState = new EntityState
                {
                    Tag = doorState.Tag,
                    IsOpen = doorState.IsOpen,
                    IsLocked = doorState.IsLocked,
                    ObjectType = ObjectType.Door
                };
                areaState.DoorStates.Add(entityState);
            }

            // Convert placeable states
            foreach (Module.PlaceableState placeableState in moduleState.Placeables)
            {
                var entityState = new EntityState
                {
                    Tag = placeableState.Tag,
                    IsOpen = placeableState.IsOpen,
                    ObjectType = ObjectType.Placeable
                };
                areaState.PlaceableStates.Add(entityState);
            }

            return areaState;
        }

        /// <summary>
        /// Converts AreaState back to ModuleState.
        /// </summary>
        private Module.ModuleState CreateModuleStateFromAreaState(AreaState areaState)
        {
            var moduleState = new Module.ModuleState();

            // Convert creature states
            foreach (EntityState entityState in areaState.CreatureStates)
            {
                if (entityState.ObjectType == ObjectType.Creature)
                {
                    moduleState.Creatures.Add(new Module.CreatureState
                    {
                        Tag = entityState.Tag,
                        Position = entityState.Position,
                        Facing = entityState.Facing,
                        CurrentHP = entityState.CurrentHP,
                        IsDead = entityState.CurrentHP <= 0
                    });
                }
            }

            // Convert door states
            foreach (EntityState entityState in areaState.DoorStates)
            {
                if (entityState.ObjectType == ObjectType.Door)
                {
                    moduleState.Doors.Add(new Module.DoorState
                    {
                        Tag = entityState.Tag,
                        IsOpen = entityState.IsOpen,
                        IsLocked = entityState.IsLocked
                    });
                }
            }

            // Convert placeable states
            foreach (EntityState entityState in areaState.PlaceableStates)
            {
                if (entityState.ObjectType == ObjectType.Placeable)
                {
                    moduleState.Placeables.Add(new Module.PlaceableState
                    {
                        Tag = entityState.Tag,
                        IsOpen = entityState.IsOpen,
                        HasInventory = false // Not stored in EntityState
                    });
                }
            }

            return moduleState;
        }

        #endregion
    }

    /// <summary>
    /// Interface for save data persistence.
    /// </summary>
    public interface ISaveDataProvider
    {
        /// <summary>
        /// Writes save data to storage.
        /// </summary>
        bool WriteSave(SaveGameData saveData);

        /// <summary>
        /// Reads save data from storage.
        /// </summary>
        SaveGameData ReadSave(string saveName);

        /// <summary>
        /// Enumerates available saves.
        /// </summary>
        IEnumerable<SaveGameInfo> EnumerateSaves();

        /// <summary>
        /// Deletes a save.
        /// </summary>
        bool DeleteSave(string saveName);

        /// <summary>
        /// Checks if a save exists.
        /// </summary>
        bool SaveExists(string saveName);
    }
}
