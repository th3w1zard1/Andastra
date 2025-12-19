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
        /// Based on swkotor2.exe save system implementation:
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

        #region Save Operations

        /// <summary>
        /// Creates a save game.
        /// </summary>
        /// <param name="saveName">Name for the save.</param>
        /// <param name="saveType">Type of save.</param>
        /// <returns>True if save succeeded.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: SerializeSaveNfo @ 0x004eb750
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
        /// Based on swkotor2.exe: SerializeSaveNfo @ 0x004eb750
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
            IModule module = _world.CurrentModule;
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

            return saveData;
        }

        // Save global variables to save data structure
        // Based on swkotor2.exe: SaveGlobalVariables @ 0x005ac670
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
        // Based on swkotor2.exe: SavePartyTable @ 0x0057bd70
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
        /// - Based on swkotor2.exe: Plot state is saved as part of game state
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

        private void SaveAreaStates(SaveGameData saveData)
        {
            // Save state for each visited area
            saveData.AreaStates = new Dictionary<string, AreaState>();

            if (_world.CurrentArea != null)
            {
                AreaState areaState = CreateAreaState(_world.CurrentArea);
                saveData.AreaStates[_world.CurrentArea.ResRef] = areaState;
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
        private CreatureState CreateCreatureState(IEntity entity)
        {
            var state = new CreatureState();
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
                state.CurrentFP = stats.CurrentFP;
                state.MaxFP = stats.MaxFP;
                // Note: Level, XP, ClassLevels, Skills, Attributes would need to be extracted from stats component
                // TODO: SIMPLIFIED - For now, we save basic HP/FP state
            }

            // Save inventory and equipment would require IInventoryComponent access
            // TODO: SIMPLIFIED - This is a simplified version - full implementation would save all creature data

            return state;
        }

        #endregion

        #region Load Operations

        /// <summary>
        /// Loads a save game.
        /// </summary>
        /// <param name="saveName">Name of the save to load.</param>
        /// <returns>True if load succeeded.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00708990 @ 0x00708990
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

            // Load module (area states are restored when areas are loaded)
            // This would trigger the module loader
            // The area states become a resource overlay

            return true;
        }

        // Restore global variables from save data
        // Based on swkotor2.exe: FUN_005ac740 @ 0x005ac740
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
        // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0
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
        private void RestoreCreatureState(IEntity entity, CreatureState state)
        {
            if (entity == null || state == null)
            {
                return;
            }

            // Restore transform
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = state.Position;
                transform.Facing = state.Facing;
            }

            // Restore stats
            Interfaces.Components.IStatsComponent stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                stats.CurrentHP = state.CurrentHP;
                stats.MaxHP = state.MaxHP;
                stats.CurrentFP = state.CurrentFP;
                stats.MaxFP = state.MaxFP;
                // Note: Level, XP, ClassLevels, Skills, Attributes would need to be restored
                // TODO: SIMPLIFIED - This is a simplified version - full implementation would restore all creature data
            }

            // Restore inventory and equipment would require IInventoryComponent access
            // TODO: SIMPLIFIED - This is a simplified version - full implementation would restore all creature data
        }

        /// <summary>
        /// Restores plot state from save data.
        /// </summary>
        /// <remarks>
        /// Plot State Restoration (swkotor2.exe):
        /// - Based on swkotor2.exe: Plot state is restored from save data
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
        /// Based on swkotor2.exe: Module state persistence
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
        /// Based on swkotor2.exe: Module state validation
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
        /// Based on swkotor2.exe: Area-to-module relationship validation
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
            IModule module = null;

            // First, check if the current module matches
            if (_world.CurrentModule != null && 
                string.Equals(_world.CurrentModule.ResRef, moduleResRef, StringComparison.OrdinalIgnoreCase))
            {
                module = _world.CurrentModule;
            }
            else
            {
                // Module is not currently loaded - we cannot verify the relationship
                // In this case, we cannot definitively check if the area belongs to the module
                // However, for save system purposes, if we have area states saved, we assume
                // they were valid when saved. This is a limitation when checking module state
                // for modules that are not currently loaded.
                // 
                // Note: A more complete implementation would maintain a module-to-area mapping
                // in the save data or load the module IFO to check Mod_Area_list, but that
                // would require additional infrastructure (module loader access, IFO parsing).
                // For now, we return false if the module is not loaded, as we cannot verify.
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
        /// Based on swkotor2.exe: Module state retrieval
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
