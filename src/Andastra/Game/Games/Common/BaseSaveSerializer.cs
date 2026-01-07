using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Save;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of save game serialization shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Save Serializer Implementation:
    /// - Common save game format handling across all engines
    /// - GFF-based serialization with engine-specific variations
    /// - Handles save metadata, entity state, area state, global variables
    ///
    /// Based on reverse engineering of:
    /// - swkotor2.exe: SerializeSaveNfo @ 0x004eb750 for metadata
    /// - All engines: GFF save format with "SAV " signature
    /// - Entity serialization functions across all executables
    /// - Global variable save/load functions
    ///
    /// Common save components:
    /// - Save metadata (NFO file): Time played, area name, screenshot
    /// - Entity states: Position, stats, inventory, scripts
    /// - Area states: Dynamic objects, effects, modifications
    /// - Global variables: Quest states, player choices
    /// - Party information: Companion states and relationships
    /// - Module state: Area loading status, transition data
    /// </remarks>
    [PublicAPI]
    public abstract class BaseSaveSerializer
    {
        /// <summary>
        /// Serializes save game metadata to NFO format.
        /// </summary>
        /// <remarks>
        /// Based on SerializeSaveNfo @ 0x004eb750 in swkotor2.exe.
        /// Creates GFF with "NFO " signature containing save information.
        /// Common across all engines with engine-specific metadata.
        ///
        /// Standard NFO fields:
        /// - SAVEGAMENAME: Display name of the save
        /// - TIMEPLAYED: Total play time in seconds
        /// - AREANAME: Current area resource name
        /// - LASTMODIFIED: Save timestamp
        /// - PORTRAIT: Player portrait data
        /// </remarks>
        public abstract byte[] SerializeSaveNfo(SaveGameData saveData);

        /// <summary>
        /// Deserializes save game metadata from NFO format.
        /// </summary>
        /// <remarks>
        /// Reads NFO GFF and extracts save metadata.
        /// Validates save compatibility and extracts display information.
        /// </remarks>
        public abstract SaveGameMetadata DeserializeSaveNfo(byte[] nfoData);

        /// <summary>
        /// Serializes global game state.
        /// </summary>
        /// <remarks>
        /// Saves global variables, quest states, player choices.
        /// Based on global variable serialization functions in all engines.
        /// Common format: GFF with variable name/value pairs.
        /// </remarks>
        public abstract byte[] SerializeGlobals(IGameState gameState);

        /// <summary>
        /// Deserializes global game state.
        /// </summary>
        /// <remarks>
        /// Restores global variables and persistent state.
        /// Updates quest states and player choice consequences.
        /// </remarks>
        public abstract void DeserializeGlobals(byte[] globalsData, IGameState gameState);

        /// <summary>
        /// Serializes party information.
        /// </summary>
        /// <remarks>
        /// Saves companion states, relationships, equipment.
        /// Engine-specific party mechanics (Odyssey companions vs Eclipse squad).
        /// Includes NPC states, approval ratings, romance flags.
        /// </remarks>
        public abstract byte[] SerializeParty(IPartyState partyState);

        /// <summary>
        /// Deserializes party information.
        /// </summary>
        /// <remarks>
        /// Restores companion states and relationships.
        /// Recreates party member equipment and positions.
        /// </remarks>
        public abstract void DeserializeParty(byte[] partyData, IPartyState partyState);

        /// <summary>
        /// Serializes area state.
        /// </summary>
        /// <remarks>
        /// Saves dynamic area changes, object states, effects.
        /// Eclipse engine: Includes destruction, moved objects.
        /// Aurora engine: Includes tile modifications.
        /// Odyssey engine: Includes placed objects, area effects.
        /// </remarks>
        public abstract byte[] SerializeArea(IArea area);

        /// <summary>
        /// Deserializes area state.
        /// </summary>
        /// <remarks>
        /// Restores dynamic area changes and object modifications.
        /// Recreates placed objects and environmental changes.
        /// </remarks>
        public abstract void DeserializeArea(byte[] areaData, IArea area);

        /// <summary>
        /// Serializes entity collection.
        /// </summary>
        /// <remarks>
        /// Based on entity serialization functions in all engines.
        /// Saves creature stats, inventory, position, scripts.
        /// Uses GFF format with entity ObjectId as key.
        /// </remarks>
        public abstract byte[] SerializeEntities(IEnumerable<IEntity> entities);

        /// <summary>
        /// Deserializes entity collection.
        /// </summary>
        /// <remarks>
        /// Recreates entities from save data.
        /// Restores stats, inventory, position, script state.
        /// Handles entity references and dependencies.
        /// </remarks>
        public abstract IEnumerable<IEntity> DeserializeEntities(byte[] entitiesData);

        /// <summary>
        /// Creates a save game directory structure.
        /// </summary>
        /// <remarks>
        /// Creates standard save directory with screenshots, data files.
        /// Engine-specific directory naming conventions.
        /// Includes validation and error handling.
        /// </remarks>
        public abstract void CreateSaveDirectory(string saveName, SaveGameData saveData);

        /// <summary>
        /// Validates save game compatibility.
        /// </summary>
        /// <remarks>
        /// Checks save version compatibility with current engine.
        /// Validates required files and data integrity.
        /// Returns detailed compatibility information.
        /// </remarks>
        public abstract SaveCompatibility CheckCompatibility(string savePath);

        /// <summary>
        /// Gets the save file format version.
        /// </summary>
        /// <remarks>
        /// Engine-specific save format versioning.
        /// Used for compatibility checking and migration.
        /// </remarks>
        protected abstract int SaveVersion { get; }

        /// <summary>
        /// Gets the engine identifier for save files.
        /// </summary>
        /// <remarks>
        /// Identifies the engine type in save metadata.
        /// Used for cross-engine compatibility detection.
        /// </remarks>
        protected abstract string EngineIdentifier { get; }

        #region Common Party Serialization Helpers

        /// <summary>
        /// Converts IPartyState interface to PartyState class.
        /// </summary>
        /// <remarks>
        /// Common helper used by all engines to extract party data.
        /// The runtime typically passes PartyState instances which contain all the rich data.
        /// This method provides a fallback for interface-only cases.
        /// </remarks>
        protected PartyState ConvertToPartyState(IPartyState partyState)
        {
            // Convert IPartyState to PartyState if possible
            // The runtime typically passes PartyState instances which contain all the rich data
            PartyState state = partyState as PartyState;
            if (state == null)
            {
                // Fallback: Create minimal PartyState from IPartyState interface
                state = new PartyState();
                if (partyState != null)
                {
                    // Extract basic party information from interface
                    if (partyState.Leader != null)
                    {
                        state.LeaderResRef = partyState.Leader.Tag ?? "";
                        if (state.PlayerCharacter == null)
                        {
                            state.PlayerCharacter = new CreatureState();
                        }
                        state.PlayerCharacter.Tag = partyState.Leader.Tag ?? "";
                    }

                    // Extract party members
                    if (partyState.Members != null)
                    {
                        foreach (IEntity member in partyState.Members)
                        {
                            if (member != null && !string.IsNullOrEmpty(member.Tag))
                            {
                                if (!state.AvailableMembers.ContainsKey(member.Tag))
                                {
                                    state.AvailableMembers[member.Tag] = new PartyMemberState
                                    {
                                        TemplateResRef = member.Tag,
                                        IsAvailable = true,
                                        IsSelectable = true
                                    };
                                }
                                if (!state.SelectedParty.Contains(member.Tag))
                                {
                                    state.SelectedParty.Add(member.Tag);
                                }
                            }
                        }
                    }
                }
            }

            return state;
        }

        /// <summary>
        /// Gets the player character name from party state.
        /// </summary>
        /// <remarks>
        /// Common helper used by all engines to extract PC name.
        /// </remarks>
        protected string GetPlayerCharacterName(PartyState state)
        {
            if (state?.PlayerCharacter != null)
            {
                return state.PlayerCharacter.Tag ?? "";
            }
            return "";
        }

        /// <summary>
        /// Gets the number of selected party members.
        /// </summary>
        /// <remarks>
        /// Common helper used by all engines.
        /// </remarks>
        protected int GetSelectedPartyCount(PartyState state)
        {
            return state?.SelectedParty != null ? state.SelectedParty.Count : 0;
        }

        /// <summary>
        /// Gets the number of available party members.
        /// </summary>
        /// <remarks>
        /// Common helper used by all engines.
        /// </remarks>
        protected int GetAvailablePartyMemberCount(PartyState state)
        {
            return state?.AvailableMembers != null ? state.AvailableMembers.Count : 0;
        }

        #endregion
    }

    /// <summary>
    /// Save game data container.
    /// </summary>
    public class SaveGameData
    {
        /// <summary>
        /// Display name of the save game.
        /// </summary>
        public string SaveName { get; set; }

        /// <summary>
        /// Current area resource name.
        /// </summary>
        public string CurrentArea { get; set; }

        /// <summary>
        /// Total play time in seconds.
        /// </summary>
        public int TimePlayed { get; set; }

        /// <summary>
        /// Save creation timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Screenshot data (if available).
        /// </summary>
        public byte[] Screenshot { get; set; }

        /// <summary>
        /// Player portrait data.
        /// </summary>
        public byte[] Portrait { get; set; }

        /// <summary>
        /// Current game state.
        /// </summary>
        public IGameState GameState { get; set; }

        /// <summary>
        /// Current party state.
        /// </summary>
        public IPartyState PartyState { get; set; }

        /// <summary>
        /// Current area instance.
        /// </summary>
        public IArea CurrentAreaInstance { get; set; }

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
    }

    /// <summary>
    /// Save game metadata.
    /// </summary>
    public class SaveGameMetadata
    {
        /// <summary>
        /// Display name of the save.
        /// </summary>
        public string SaveName { get; set; }

        /// <summary>
        /// Current area name.
        /// </summary>
        public string AreaName { get; set; }

        /// <summary>
        /// Play time in seconds.
        /// </summary>
        public int TimePlayed { get; set; }

        /// <summary>
        /// Save timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Engine that created the save.
        /// </summary>
        public string EngineVersion { get; set; }

        /// <summary>
        /// Save format version.
        /// </summary>
        public int SaveVersion { get; set; }
    }

    /// <summary>
    /// Save compatibility information.
    /// </summary>
    public enum SaveCompatibility
    {
        /// <summary>
        /// Save is fully compatible.
        /// </summary>
        Compatible,

        /// <summary>
        /// Save is compatible with minor issues.
        /// </summary>
        CompatibleWithWarnings,

        /// <summary>
        /// Save requires migration to be compatible.
        /// </summary>
        RequiresMigration,

        /// <summary>
        /// Save is incompatible and cannot be loaded.
        /// </summary>
        Incompatible
    }

    /// <summary>
    /// Interface for game state management.
    /// </summary>
    public interface IGameState
    {
        /// <summary>
        /// Gets a global variable value.
        /// </summary>
        T GetGlobal<T>(string name, T defaultValue = default(T));

        /// <summary>
        /// Sets a global variable value.
        /// </summary>
        void SetGlobal(string name, object value);

        /// <summary>
        /// Checks if a global variable exists.
        /// </summary>
        bool HasGlobal(string name);

        /// <summary>
        /// Gets all global variable names.
        /// </summary>
        IEnumerable<string> GetGlobalNames();
    }

    /// <summary>
    /// Interface for party state management.
    /// </summary>
    public interface IPartyState
    {
        /// <summary>
        /// Gets all party members.
        /// </summary>
        IEnumerable<IEntity> Members { get; }

        /// <summary>
        /// Adds a member to the party.
        /// </summary>
        void AddMember(IEntity entity);

        /// <summary>
        /// Removes a member from the party.
        /// </summary>
        void RemoveMember(IEntity entity);

        /// <summary>
        /// Gets party leader.
        /// </summary>
        IEntity Leader { get; set; }
    }
}
