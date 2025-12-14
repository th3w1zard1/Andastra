using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Save
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
    /// </remarks>
    public class SaveSystem
    {
        private readonly IWorld _world;
        private readonly ISaveDataProvider _dataProvider;
        private object _globals; // IScriptGlobals - stored as object to avoid dependency

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

        #region Save Operations

        /// <summary>
        /// Creates a save game.
        /// </summary>
        /// <param name="saveName">Name for the save.</param>
        /// <param name="saveType">Type of save.</param>
        /// <returns>True if save succeeded.</returns>
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

            return saveData;
        }

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

        private void SavePartyState(SaveGameData saveData)
        {
            // Save party member list and selection
            saveData.PartyState = new PartyState();

            // Would iterate through party members and save their state
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

        #endregion

        #region Load Operations

        /// <summary>
        /// Loads a save game.
        /// </summary>
        /// <param name="saveName">Name of the save to load.</param>
        /// <returns>True if load succeeded.</returns>
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

            // Load module (area states are restored when areas are loaded)
            // This would trigger the module loader
            // The area states become a resource overlay

            return true;
        }

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

        private void RestorePartyState(SaveGameData saveData)
        {
            if (saveData.PartyState == null)
            {
                return;
            }

            // Would restore party members
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
