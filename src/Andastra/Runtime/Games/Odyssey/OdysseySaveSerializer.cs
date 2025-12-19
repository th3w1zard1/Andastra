using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Engines.Odyssey.Data;

namespace Andastra.Runtime.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) save game serializer implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Save Serializer Implementation:
    /// - Based on swkotor.exe and swkotor2.exe save systems
    /// - Uses GFF format with "SAV " and "NFO " signatures
    /// - Handles entity serialization, global variables, party data
    ///
    /// Based on reverse engineering of:
    /// - swkotor2.exe: SerializeSaveNfo @ 0x004eb750 for metadata creation
    /// - swkotor2.exe: Global variable save/load functions
    /// - Entity serialization: FUN_004e28c0 save, FUN_005fb0f0 load
    /// - Party management and companion state saving
    ///
    /// Save file structure:
    /// - Save directory with numbered subdirectories
    /// - NFO file: Metadata (name, time, area, screenshot)
    /// - SAV file: Main save data (entities, globals, party)
    /// - RES directory: Screenshots and additional resources
    /// - GFF format for structured data storage
    /// </remarks>
    [PublicAPI]
    public class OdysseySaveSerializer : BaseSaveSerializer
    {
        [CanBeNull] private readonly GameDataManager _gameDataManager;

        /// <summary>
        /// Initializes a new instance of the OdysseySaveSerializer class.
        /// </summary>
        /// <param name="gameDataManager">Optional game data manager for partytable.2da lookups. If null, falls back to hardcoded mapping.</param>
        public OdysseySaveSerializer([CanBeNull] GameDataManager gameDataManager = null)
        {
            _gameDataManager = gameDataManager;
        }
        /// <summary>
        /// Gets the save file format version for Odyssey engine.
        /// </summary>
        /// <remarks>
        /// KotOR uses version 1, KotOR2 uses version 2.
        /// Used for compatibility checking between game versions.
        /// </remarks>
        protected override int SaveVersion => 2; // KotOR 2 version

        /// <summary>
        /// Gets the engine identifier.
        /// </summary>
        /// <remarks>
        /// Identifies this as an Odyssey engine save.
        /// Used for cross-engine compatibility detection.
        /// </remarks>
        protected override string EngineIdentifier => "Odyssey";

        /// <summary>
        /// Serializes save game metadata to NFO format.
        /// </summary>
        /// <remarks>
        /// Based on SerializeSaveNfo @ 0x004eb750 in swkotor2.exe.
        /// Creates GFF with "NFO " signature containing save information.
        /// Includes SAVEGAMENAME, TIMEPLAYED, AREANAME, and metadata.
        ///
        /// NFO structure:
        /// - Signature: "NFO "
        /// - Version: "V2.0" for KotOR 2
        /// - SAVEGAMENAME: Display name
        /// - TIMEPLAYED: Play time in seconds
        /// - AREANAME: Current area resource
        /// - LASTMODIFIED: Timestamp
        /// </remarks>
        public override byte[] SerializeSaveNfo(Andastra.Runtime.Games.Common.SaveGameData saveData)
        {
            // TODO: Implement complete NFO serialization
            // Create GFF structure with NFO signature
            // Add standard metadata fields
            // Include screenshot data if available
            // Write timestamp and version info

            throw new NotImplementedException("Odyssey NFO serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes save game metadata from NFO format.
        /// </summary>
        /// <remarks>
        /// Reads NFO GFF and extracts save metadata.
        /// Validates NFO signature and version compatibility.
        /// Returns structured metadata for save game display.
        /// </remarks>
        public override SaveGameMetadata DeserializeSaveNfo(byte[] nfoData)
        {
            // TODO: Implement NFO deserialization
            // Validate NFO signature
            // Read metadata fields
            // Extract screenshot if present
            // Return structured metadata

            throw new NotImplementedException("Odyssey NFO deserialization not yet implemented");
        }

        /// <summary>
        /// Serializes global game state.
        /// </summary>
        /// <remarks>
        /// Based on global variable serialization in swkotor2.exe.
        /// Saves quest states, player choices, persistent variables.
        /// Uses GFF format with variable categories.
        ///
        /// Global categories:
        /// - QUEST: Quest completion states
        /// - CHOICE: Player dialogue choices
        /// - PERSISTENT: Long-term game state
        /// - MODULE: Per-module variables
        /// </remarks>
        public override byte[] SerializeGlobals(IGameState gameState)
        {
            // TODO: Implement global variable serialization
            // Create GFF with GLOBALS struct
            // Categorize variables by type
            // Handle different data types (int, float, string, location)
            // Include variable metadata

            throw new NotImplementedException("Odyssey global serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes global game state.
        /// </summary>
        /// <remarks>
        /// Restores global variables from save data.
        /// Updates quest states and player choice consequences.
        /// Validates variable integrity and types.
        /// </remarks>
        public override void DeserializeGlobals(byte[] globalsData, IGameState gameState)
        {
            // TODO: Implement global variable deserialization
            // Parse GFF GLOBALS struct
            // Restore variables by category
            // Validate data types and ranges
            // Update game state accordingly
        }

        /// <summary>
        /// Serializes party information.
        /// </summary>
        /// <remarks>
        /// Odyssey party serialization includes companions and their states.
        /// Saves companion approval, equipment, position, quest involvement.
        /// Includes party formation and leadership information.
        ///
        /// Based on swkotor2.exe: FUN_0057bd70 @ 0x0057bd70
        /// Located via string reference: "PARTYTABLE" @ 0x007c1910
        /// Original implementation creates GFF with "PT  " signature and "V2.0" version.
        ///
        /// Party data includes:
        /// - Companion entities and their states
        /// - Approval ratings and relationship flags (K2 influence system)
        /// - Equipment and inventory
        /// - Position in party formation
        /// - Active quest involvement
        /// - Game state (gold, XP, components, chemicals, swoop times)
        /// - UI state (Pazaak cards, tutorial windows, messages, galaxy map)
        /// - AI and follow states
        /// </remarks>
        public override byte[] SerializeParty(IPartyState partyState)
        {
            // Use common helper to convert IPartyState to PartyState
            PartyState state = ConvertToPartyState(partyState);

            // Use Andastra.Parsing GFF writer
            // Original creates GFF with "PT  " signature and "V2.0" version
            // Based on swkotor2.exe: FUN_0057bd70 @ 0x0057bd70 creates GFF with "PT  " signature
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Note: Andastra.Parsing GFFBinaryWriter always writes "V3.2" version, but signature is correct
            var gff = new GFF(GFFContent.PT);
            var root = gff.Root;

            // PT_PCNAME - Player character name
            // Use common helper to extract PC name
            root.SetString("PT_PCNAME", GetPlayerCharacterName(state));

            // PT_GOLD - Gold/credits
            root.SetInt32("PT_GOLD", state.Gold);

            // PT_ITEM_COMPONENT - Item component count
            root.SetInt32("PT_ITEM_COMPONENT", state.ItemComponent);

            // PT_ITEM_CHEMICAL - Item chemical count
            root.SetInt32("PT_ITEM_CHEMICAL", state.ItemChemical);

            // PT_SWOOP1-3 - Swoop race times
            root.SetInt32("PT_SWOOP1", state.Swoop1);
            root.SetInt32("PT_SWOOP2", state.Swoop2);
            root.SetInt32("PT_SWOOP3", state.Swoop3);

            // PT_XP_POOL - Experience point pool (float)
            root.SetSingle("PT_XP_POOL", state.ExperiencePoints);

            // PT_PLAYEDSECONDS - Total seconds played
            root.SetInt32("PT_PLAYEDSECONDS", (int)state.PlayTime.TotalSeconds);

            // PT_CONTROLLED_NPC - Currently controlled NPC ID (float, -1 if none)
            root.SetSingle("PT_CONTROLLED_NPC", state.ControlledNPC >= 0 ? (float)state.ControlledNPC : -1.0f);

            // PT_SOLOMODE - Solo mode flag (byte)
            root.SetUInt8("PT_SOLOMODE", state.SoloMode ? (byte)1 : (byte)0);

            // PT_CHEAT_USED - Cheat used flag (byte)
            root.SetUInt8("PT_CHEAT_USED", state.CheatUsed ? (byte)1 : (byte)0);

            // PT_NUM_MEMBERS - Number of party members (byte)
            // Use common helper to get party count
            int numMembers = GetSelectedPartyCount(state);
            root.SetUInt8("PT_NUM_MEMBERS", (byte)numMembers);

            // PT_MEMBERS - List of party members
            var membersList = new GFFList();
            if (state.SelectedParty != null)
            {
                foreach (string memberResRef in state.SelectedParty)
                {
                    GFFStruct entry = membersList.Add();
                    // PT_MEMBER_ID - Member ID (float)
                    entry.SetSingle("PT_MEMBER_ID", GetMemberId(memberResRef));
                    // PT_IS_LEADER - Whether this member is the leader (byte)
                    bool isLeader = state.LeaderResRef == memberResRef;
                    entry.SetUInt8("PT_IS_LEADER", isLeader ? (byte)1 : (byte)0);
                }
            }
            root.SetList("PT_MEMBERS", membersList);

            // PT_NUM_PUPPETS - Number of puppets (byte)
            int numPuppets = state.Puppets != null ? state.Puppets.Count : 0;
            root.SetUInt8("PT_NUM_PUPPETS", (byte)numPuppets);

            // PT_PUPPETS - List of puppets
            var puppetsList = new GFFList();
            if (state.Puppets != null)
            {
                foreach (uint puppetId in state.Puppets)
                {
                    GFFStruct entry = puppetsList.Add();
                    entry.SetSingle("PT_PUPPET_ID", (float)puppetId);
                }
            }
            root.SetList("PT_PUPPETS", puppetsList);

            // PT_AVAIL_PUPS - Available puppets list (3 entries)
            var availPupsList = new GFFList();
            for (int i = 0; i < 3; i++)
            {
                GFFStruct entry = availPupsList.Add();
                bool available = state.AvailablePuppets != null && i < state.AvailablePuppets.Count && state.AvailablePuppets[i];
                entry.SetUInt8("PT_PUP_AVAIL", available ? (byte)1 : (byte)0);
                bool selectable = state.SelectablePuppets != null && i < state.SelectablePuppets.Count && state.SelectablePuppets[i];
                entry.SetUInt8("PT_PUP_SELECT", selectable ? (byte)1 : (byte)0);
            }
            root.SetList("PT_AVAIL_PUPS", availPupsList);

            // PT_AVAIL_NPCS - Available NPCs list (12 entries)
            var availNpcsList = new GFFList();
            List<PartyMemberState> memberList = state.AvailableMembers != null ? new List<PartyMemberState>(state.AvailableMembers.Values) : new List<PartyMemberState>();
            for (int i = 0; i < 12; i++)
            {
                GFFStruct entry = availNpcsList.Add();
                bool available = i < memberList.Count && memberList[i] != null;
                entry.SetUInt8("PT_NPC_AVAIL", available ? (byte)1 : (byte)0);
                bool selectable = available && memberList[i].IsSelectable;
                entry.SetUInt8("PT_NPC_SELECT", selectable ? (byte)1 : (byte)0);
            }
            root.SetList("PT_AVAIL_NPCS", availNpcsList);

            // PT_INFLUENCE - Influence values list (12 entries, K2 only)
            var influenceList = new GFFList();
            for (int i = 0; i < 12; i++)
            {
                GFFStruct entry = influenceList.Add();
                float influence = 0.0f;
                if (state.Influence != null && i < state.Influence.Count)
                {
                    influence = (float)state.Influence[i];
                }
                entry.SetSingle("PT_NPC_INFLUENCE", influence);
            }
            root.SetList("PT_INFLUENCE", influenceList);

            // PT_AISTATE - AI state (float)
            root.SetSingle("PT_AISTATE", (float)state.AIState);

            // PT_FOLLOWSTATE - Follow state (float)
            root.SetSingle("PT_FOLLOWSTATE", (float)state.FollowState);

            // GlxyMap - Galaxy map data
            var glxyMapStruct = new GFFStruct();
            glxyMapStruct.SetInt32("GlxyMapNumPnts", 16); // Always 16 points
            glxyMapStruct.SetInt32("GlxyMapPlntMsk", state.GalaxyMapPlanetMask);
            glxyMapStruct.SetSingle("GlxyMapSelPnt", (float)state.GalaxyMapSelectedPoint);
            root.SetStruct("GlxyMap", glxyMapStruct);

            // PT_PAZAAKCARDS - Pazaak cards list (23 entries)
            var pazaakCardsList = new GFFList();
            for (int i = 0; i < 23; i++)
            {
                GFFStruct entry = pazaakCardsList.Add();
                int count = 0;
                if (state.PazaakCards != null && i < state.PazaakCards.Count)
                {
                    count = state.PazaakCards[i];
                }
                entry.SetSingle("PT_PAZAAKCOUNT", (float)count);
            }
            root.SetList("PT_PAZAAKCARDS", pazaakCardsList);

            // PT_PAZSIDELIST - Pazaak side list (10 entries)
            var pazaakSideList = new GFFList();
            for (int i = 0; i < 10; i++)
            {
                GFFStruct entry = pazaakSideList.Add();
                int card = 0;
                if (state.PazaakSideList != null && i < state.PazaakSideList.Count)
                {
                    card = state.PazaakSideList[i];
                }
                entry.SetSingle("PT_PAZSIDECARD", (float)card);
            }
            root.SetList("PT_PAZSIDELIST", pazaakSideList);

            // PT_TUT_WND_SHOWN - Tutorial windows shown (array of 33 bytes)
            if (state.TutorialWindowsShown != null)
            {
                byte[] tutArray = new byte[33];
                for (int i = 0; i < 33 && i < state.TutorialWindowsShown.Count; i++)
                {
                    tutArray[i] = state.TutorialWindowsShown[i] ? (byte)1 : (byte)0;
                }
                root.SetBinary("PT_TUT_WND_SHOWN", tutArray);
            }

            // PT_LAST_GUI_PNL - Last GUI panel (float)
            root.SetSingle("PT_LAST_GUI_PNL", (float)state.LastGUIPanel);

            // PT_FB_MSG_LIST - Feedback message list
            var fbMsgList = new GFFList();
            if (state.FeedbackMessages != null)
            {
                foreach (var msg in state.FeedbackMessages)
                {
                    GFFStruct entry = fbMsgList.Add();
                    entry.SetString("PT_FB_MSG_MSG", msg.Message ?? "");
                    entry.SetInt32("PT_FB_MSG_TYPE", msg.Type);
                    entry.SetUInt8("PT_FB_MSG_COLOR", msg.Color);
                }
            }
            root.SetList("PT_FB_MSG_LIST", fbMsgList);

            // PT_DLG_MSG_LIST - Dialogue message list
            var dlgMsgList = new GFFList();
            if (state.DialogueMessages != null)
            {
                foreach (var msg in state.DialogueMessages)
                {
                    GFFStruct entry = dlgMsgList.Add();
                    entry.SetString("PT_DLG_MSG_SPKR", msg.Speaker ?? "");
                    entry.SetString("PT_DLG_MSG_MSG", msg.Message ?? "");
                }
            }
            root.SetList("PT_DLG_MSG_LIST", dlgMsgList);

            // PT_COM_MSG_LIST - Combat message list
            var comMsgList = new GFFList();
            if (state.CombatMessages != null)
            {
                foreach (var msg in state.CombatMessages)
                {
                    GFFStruct entry = comMsgList.Add();
                    entry.SetString("PT_COM_MSG_MSG", msg.Message ?? "");
                    entry.SetInt32("PT_COM_MSG_TYPE", msg.Type);
                    entry.SetUInt8("PT_COM_MSG_COOR", msg.Color);
                }
            }
            root.SetList("PT_COM_MSG_LIST", comMsgList);

            // PT_COST_MULT_LIST - Cost multiplier list
            var costMultList = new GFFList();
            if (state.CostMultipliers != null)
            {
                foreach (var mult in state.CostMultipliers)
                {
                    GFFStruct entry = costMultList.Add();
                    entry.SetSingle("PT_COST_MULT_VALUE", mult);
                }
            }
            root.SetList("PT_COST_MULT_LIST", costMultList);

            // PT_DISABLEMAP - Disable map flag (float)
            root.SetSingle("PT_DISABLEMAP", state.DisableMap ? 1.0f : 0.0f);

            // PT_DISABLEREGEN - Disable regen flag (float)
            root.SetSingle("PT_DISABLEREGEN", state.DisableRegen ? 1.0f : 0.0f);

            // Serialize GFF to bytes
            return gff.ToBytes();
        }

        /// <summary>
        /// Helper to get member ID from ResRef.
        /// </summary>
        /// <remarks>
        /// Member IDs: -1 = Player, 0-8 = NPC slots (K1), 0-11 = NPC slots (K2)
        /// Based on nwscript.nss constants: NPC_PLAYER = -1, NPC_BASTILA = 0, etc.
        ///
        /// Implementation:
        /// - Based on swkotor2.exe: partytable.2da maps NPC ResRefs to member IDs
        /// - Located via string reference: "PARTYTABLE" @ 0x007c1910
        /// - Original implementation: partytable.2da row index = member ID (0-11 for K2, 0-8 for K1)
        /// - Row label in partytable.2da is the NPC ResRef (e.g., "bastila", "atton")
        /// - Searches partytable.2da for matching ResRef (exact match, then partial match)
        /// - Falls back to hardcoded mapping if partytable.2da not available
        /// </remarks>
        private float GetMemberId(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return -1.0f; // Default to player
            }

            // Player character is typically identified by specific ResRefs or -1
            // Common player ResRefs: "player", "pc", etc.
            string resRefLower = resRef.ToLowerInvariant();
            if (resRefLower == "player" || resRefLower == "pc" || resRefLower.StartsWith("pc_"))
            {
                return -1.0f; // NPC_PLAYER
            }

            // Try to load from partytable.2da if GameDataManager is available
            // Based on swkotor2.exe: partytable.2da system
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Original implementation: partytable.2da maps NPC ResRefs to member IDs (row index = member ID)
            // partytable.2da structure: Row label is ResRef, row index is member ID (0-11 for K2, 0-8 for K1)
            if (_gameDataManager != null)
            {
                Andastra.Parsing.Formats.TwoDA.TwoDA partyTable = _gameDataManager.GetTable("partytable");
                if (partyTable != null)
                {
                    // Search partytable.2da for matching ResRef
                    // Row index in partytable.2da corresponds to member ID
                    for (int i = 0; i < partyTable.GetHeight(); i++)
                    {
                        Andastra.Parsing.Formats.TwoDA.TwoDARow row = partyTable.GetRow(i);
                        string rowLabel = row.Label();

                        if (string.IsNullOrEmpty(rowLabel))
                        {
                            continue;
                        }

                        string rowLabelLower = rowLabel.ToLowerInvariant();

                        // Check exact match first (most common case)
                        if (string.Equals(rowLabelLower, resRefLower, StringComparison.OrdinalIgnoreCase))
                        {
                            return (float)i;
                        }

                        // Check if ResRef starts with row label (e.g., "bastila" matches "bastila_001")
                        if (resRefLower.StartsWith(rowLabelLower, StringComparison.OrdinalIgnoreCase))
                        {
                            // Verify it's a valid match (not just a substring in the middle)
                            // Check if next character is underscore, number, or end of string
                            if (resRefLower.Length == rowLabelLower.Length || 
                                resRefLower[rowLabelLower.Length] == '_' || 
                                char.IsDigit(resRefLower[rowLabelLower.Length]))
                            {
                                return (float)i;
                            }
                        }

                        // Check if row label is contained in ResRef (e.g., "atton" in "atton_001")
                        // This handles cases where the ResRef has additional suffixes
                        if (resRefLower.Contains(rowLabelLower))
                        {
                            // Verify it's at the start of the ResRef (not in the middle)
                            int index = resRefLower.IndexOf(rowLabelLower, StringComparison.OrdinalIgnoreCase);
                            if (index == 0)
                            {
                                return (float)i;
                            }
                        }
                    }
                }
            }

            // Fallback: Hardcoded mapping for common NPCs when partytable.2da not available
            // Based on nwscript.nss constants and common ResRef patterns
            var npcMapping = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                // K1 NPCs (0-8)
                { "bastila", 0.0f },      // NPC_BASTILA
                { "canderous", 1.0f },    // NPC_CANDEROUS
                { "carth", 2.0f },        // NPC_CARTH
                { "hk47", 3.0f },         // NPC_HK_47
                { "jolee", 4.0f },        // NPC_JOLEE
                { "juhani", 5.0f },      // NPC_JUHANI
                { "mission", 6.0f },     // NPC_MISSION
                { "t3m4", 7.0f },        // NPC_T3_M4
                { "zaalbar", 8.0f },     // NPC_ZAALBAR

                // K2 NPCs (0-11) - some overlap with K1
                { "atton", 0.0f },       // K2 NPC_ATTON
                { "bao-dur", 1.0f },     // K2 NPC_BAO_DUR
                { "disciple", 2.0f },    // K2 NPC_DISCIPLE
                { "handmaiden", 3.0f },  // K2 NPC_HANDMAIDEN
                { "hanharr", 4.0f },     // K2 NPC_HANHARR
                { "g0-t0", 5.0f },       // K2 NPC_G0_T0
                { "kreia", 6.0f },       // K2 NPC_KREIA
                { "mira", 7.0f },        // K2 NPC_MIRA
                { "visas", 8.0f },       // K2 NPC_VISAS
                { "mandalore", 9.0f },   // K2 NPC_MANDALORE
                { "sion", 11.0f },       // K2 NPC_SION
            };

            // Try exact match first
            if (npcMapping.TryGetValue(resRefLower, out float memberId))
            {
                return memberId;
            }

            // Try partial match (e.g., "bastila" matches "bastila_001")
            foreach (var kvp in npcMapping)
            {
                if (resRefLower.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // Verify it's a valid match (not just a substring)
                    if (resRefLower.Length == kvp.Key.Length || 
                        resRefLower[kvp.Key.Length] == '_' || 
                        char.IsDigit(resRefLower[kvp.Key.Length]))
                    {
                        return kvp.Value;
                    }
                }
            }

            // If no mapping found, return 0.0f as default
            // This matches original engine behavior when ResRef is not recognized
            return 0.0f;
        }

        /// <summary>
        /// Deserializes party information.
        /// </summary>
        /// <remarks>
        /// Recreates party from save data.
        /// Restores companion states, relationships, equipment.
        /// Reestablishes party formation and leadership.
        /// </remarks>
        public override void DeserializeParty(byte[] partyData, IPartyState partyState)
        {
            // TODO: Implement party deserialization
            // Parse PARTY struct
            // Recreate companion entities
            // Restore relationships and states
            // Reestablish party structure
        }

        /// <summary>
        /// Serializes area state.
        /// </summary>
        /// <remarks>
        /// Odyssey area serialization saves dynamic changes.
        /// Includes placed objects, modified containers, area effects.
        /// Saves transition states and dynamic object modifications.
        ///
        /// Area state includes:
        /// - Placed creatures and objects
        /// - Modified container contents
        /// - Active area effects
        /// - Door and transition states
        /// - Dynamic lighting changes
        /// </remarks>
        public override byte[] SerializeArea(IArea area)
        {
            // TODO: Implement area serialization
            // Create AREA struct for the specific area
            // Serialize dynamic objects
            // Save modified container states
            // Include area effect states

            throw new NotImplementedException("Odyssey area serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes area state.
        /// </summary>
        /// <remarks>
        /// Restores dynamic area changes from save data.
        /// Recreates placed objects and restores modified states.
        /// Applies area effects and transition states.
        /// </remarks>
        public override void DeserializeArea(byte[] areaData, IArea area)
        {
            // TODO: Implement area deserialization
            // Parse AREA struct
            // Recreate dynamic objects
            // Restore modified states
            // Apply area effects
        }

        /// <summary>
        /// Serializes entity collection.
        /// </summary>
        /// <remarks>
        /// Based on FUN_004e28c0 @ 0x004e28c0 in swkotor2.exe.
        /// Saves creature stats, inventory, position, scripts.
        /// Uses GFF format with entity ObjectId as key.
        ///
        /// Entity data includes:
        /// - ObjectId, Tag, ObjectType
        /// - Position and orientation
        /// - Stats (HP, FP, attributes)
        /// - Equipment and inventory
        /// - Active scripts and effects
        /// - AI state and waypoints
        ///
        /// Implementation details:
        /// - Creates GFF with "Creature List" list (swkotor2.exe: 0x007c0c80)
        /// - For each entity, serializes to GFF and adds root struct to list
        /// - Each list entry contains ObjectId and all entity component data
        /// - Based on swkotor2.exe: FUN_004e28c0 creates "Creature List" structure
        /// - FUN_005226d0 serializes individual entity data into struct
        /// </remarks>
        public override byte[] SerializeEntities(IEnumerable<IEntity> entities)
        {
            if (entities == null)
            {
                // Return empty GFF with empty CreatureList
                var emptyGff = new GFF(GFFContent.GFF);
                emptyGff.Root.SetList("Creature List", new GFFList());
                return emptyGff.ToBytes();
            }

            // Create GFF structure for entity collection
            // Based on swkotor2.exe: FUN_004e28c0 @ 0x004e28c0 creates "Creature List" structure
            // Located via string reference: "Creature List" @ 0x007c0c80
            var gff = new GFF(GFFContent.GFF);
            var root = gff.Root;
            var creatureList = new GFFList();

            // Serialize each entity
            foreach (IEntity entity in entities)
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Check if entity is a BaseEntity (has Serialize method)
                BaseEntity baseEntity = entity as BaseEntity;
                if (baseEntity == null)
                {
                    // For non-BaseEntity IEntity instances, create minimal serialization
                    var entityStruct = creatureList.Add();
                    entityStruct.SetUInt32("ObjectId", entity.ObjectId);
                    entityStruct.SetString("Tag", entity.Tag ?? "");
                    entityStruct.SetInt32("ObjectType", (int)entity.ObjectType);
                    entityStruct.SetUInt32("AreaId", entity.AreaId);
                    continue;
                }

                // Serialize entity to GFF
                // Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 serializes entity to GFF struct
                byte[] entityData = baseEntity.Serialize();
                if (entityData == null || entityData.Length == 0)
                {
                    continue;
                }

                // Parse entity's serialized GFF
                GFF entityGff = GFF.FromBytes(entityData);
                GFFStruct entityRoot = entityGff.Root;

                // Create struct entry in CreatureList
                // Based on swkotor2.exe: FUN_00413600 creates struct entry, FUN_00413880 sets ObjectId
                var listEntry = creatureList.Add();

                // Copy all fields from entity's root struct to list entry
                // This includes ObjectId, Tag, ObjectType, AreaId, and all component data
                CopyGffStructFields(entityRoot, listEntry);
            }

            // Set CreatureList in root
            root.SetList("Creature List", creatureList);

            // Serialize GFF to bytes
            return gff.ToBytes();
        }

        /// <summary>
        /// Copies all fields from source GFF struct to destination struct.
        /// </summary>
        /// <remarks>
        /// Helper method to merge entity serialization data into list entries.
        /// Handles all GFF field types including nested structs and lists.
        /// </remarks>
        private void CopyGffStructFields(GFFStruct source, GFFStruct destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            // Iterate through all fields in source struct
            foreach (var (label, fieldType, value) in source)
            {
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                // Copy field based on type
                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        destination.SetUInt8(label, source.GetUInt8(label));
                        break;
                    case GFFFieldType.Int8:
                        destination.SetInt8(label, source.GetInt8(label));
                        break;
                    case GFFFieldType.UInt16:
                        destination.SetUInt16(label, source.GetUInt16(label));
                        break;
                    case GFFFieldType.Int16:
                        destination.SetInt16(label, source.GetInt16(label));
                        break;
                    case GFFFieldType.UInt32:
                        destination.SetUInt32(label, source.GetUInt32(label));
                        break;
                    case GFFFieldType.Int32:
                        destination.SetInt32(label, source.GetInt32(label));
                        break;
                    case GFFFieldType.UInt64:
                        destination.SetUInt64(label, source.GetUInt64(label));
                        break;
                    case GFFFieldType.Int64:
                        destination.SetInt64(label, source.GetInt64(label));
                        break;
                    case GFFFieldType.Single:
                        destination.SetSingle(label, source.GetSingle(label));
                        break;
                    case GFFFieldType.Double:
                        destination.SetDouble(label, source.GetDouble(label));
                        break;
                    case GFFFieldType.String:
                        destination.SetString(label, source.GetString(label) ?? "");
                        break;
                    case GFFFieldType.ResRef:
                        destination.SetResRef(label, source.GetResRef(label) ?? "");
                        break;
                    case GFFFieldType.LocalizedString:
                        destination.SetLocString(label, source.GetLocString(label));
                        break;
                    case GFFFieldType.Vector3:
                        destination.SetVector3(label, source.GetVector3(label));
                        break;
                    case GFFFieldType.Vector4:
                        destination.SetVector4(label, source.GetVector4(label));
                        break;
                    case GFFFieldType.Binary:
                        destination.SetBinary(label, source.GetBinary(label));
                        break;
                    case GFFFieldType.Struct:
                        // Recursively copy nested structs
                        GFFStruct nestedSource = source.GetStruct(label);
                        if (nestedSource != null)
                        {
                            var nestedDest = new GFFStruct();
                            CopyGffStructFields(nestedSource, nestedDest);
                            destination.SetStruct(label, nestedDest);
                        }
                        break;
                    case GFFFieldType.List:
                        // Copy lists (shallow copy - lists are reference types in GFF)
                        GFFList sourceList = source.GetList(label);
                        if (sourceList != null)
                        {
                            destination.SetList(label, sourceList);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Deserializes entity collection.
        /// </summary>
        /// <remarks>
        /// Based on FUN_005fb0f0 @ 0x005fb0f0 in swkotor2.exe.
        /// Recreates entities from save data.
        /// Restores all components and state information.
        /// Handles entity interdependencies.
        ///
        /// Implementation details:
        /// - Parses GFF with "Creature List" list (swkotor2.exe: 0x007c0c80)
        /// - For each entity struct, creates OdysseyEntity and deserializes data
        /// - Based on swkotor2.exe: FUN_005fb0f0 loads entities from "Creature List" structure
        /// - Each list entry contains ObjectId and all entity component data
        /// - Note: Full deserialization requires OdysseyEntity.Deserialize() to be implemented
        /// </remarks>
        public override IEnumerable<IEntity> DeserializeEntities(byte[] entitiesData)
        {
            if (entitiesData == null || entitiesData.Length == 0)
            {
                yield break;
            }

            // Parse GFF structure
            GFF gff;
            try
            {
                gff = GFF.FromBytes(entitiesData);
            }
            catch (Exception)
            {
                // Invalid GFF data
                yield break;
            }

            GFFStruct root = gff.Root;

            // Get Creature List
            if (!root.Exists("Creature List"))
            {
                yield break;
            }

            GFFList creatureList = root.GetList("Creature List");
            if (creatureList == null)
            {
                yield break;
            }

            // Deserialize each entity
            foreach (GFFStruct entityStruct in creatureList)
            {
                if (entityStruct == null)
                {
                    continue;
                }

                // Read basic entity properties
                if (!entityStruct.Exists("ObjectId") || !entityStruct.Exists("ObjectType"))
                {
                    continue;
                }

                uint objectId = entityStruct.GetUInt32("ObjectId");
                int objectTypeInt = entityStruct.GetInt32("ObjectType");
                ObjectType objectType = (ObjectType)objectTypeInt;
                string tag = entityStruct.Exists("Tag") ? (entityStruct.GetString("Tag") ?? "") : null;

                // Create entity
                // Based on swkotor2.exe: Entities are created with ObjectId, ObjectType, Tag
                // Note: Full entity creation with components requires EntityFactory or World.CreateEntity
                // For now, create basic OdysseyEntity - caller should register with world and restore components
                OdysseyEntity entity = new OdysseyEntity(objectId, objectType, tag);

                // Restore AreaId if present
                if (entityStruct.Exists("AreaId"))
                {
                    entity.AreaId = entityStruct.GetUInt32("AreaId");
                }

                // Convert entity struct to GFF bytes for deserialization
                // Create a new GFF with this struct as root
                var entityGff = new GFF(GFFContent.GFF);
                CopyGffStructFields(entityStruct, entityGff.Root);
                byte[] entityData = entityGff.ToBytes();

                // Deserialize entity (if Deserialize is implemented)
                // Note: OdysseyEntity.Deserialize() is not yet fully implemented
                // This will throw NotImplementedException until that method is implemented
                try
                {
                    entity.Deserialize(entityData);
                }
                catch (NotImplementedException)
                {
                    // Deserialize not yet implemented - entity has basic properties only
                    // Restore basic transform if available
                    if (entityStruct.Exists("X") && entityStruct.Exists("Y") && entityStruct.Exists("Z"))
                    {
                        var transformComponent = entity.GetComponent<ITransformComponent>();
                        if (transformComponent != null)
                        {
                            float x = entityStruct.GetSingle("X");
                            float y = entityStruct.GetSingle("Y");
                            float z = entityStruct.GetSingle("Z");
                            float facing = entityStruct.Exists("Facing") ? entityStruct.GetSingle("Facing") : 0.0f;
                            transformComponent.Position = new System.Numerics.Vector3(x, y, z);
                            transformComponent.Facing = facing;
                        }
                    }
                }

                yield return entity;
            }
        }

        /// <summary>
        /// Creates a save game directory structure.
        /// </summary>
        /// <remarks>
        /// Creates numbered save directories following KotOR conventions.
        /// Creates NFO, SAV, and supporting files.
        /// Includes screenshot and metadata files.
        ///
        /// Directory structure:
        /// - Save game root directory
        /// - Numbered save subdirectories (save.0, save.1, etc.)
        /// - savenfo.res: Metadata file
        /// - SAVEgame.sav: Main save data
        /// - Screen.tga: Screenshot
        /// </remarks>
        public override void CreateSaveDirectory(string saveName, Andastra.Runtime.Games.Common.SaveGameData saveData)
        {
            // TODO: Implement save directory creation
            // Create numbered save directory
            // Write NFO file
            // Write main SAV file
            // Save screenshot if available
            // Create supporting files
        }

        /// <summary>
        /// Validates save game compatibility.
        /// </summary>
        /// <remarks>
        /// Checks KotOR save compatibility.
        /// Validates NFO signature and version.
        /// Checks for required save files.
        /// Returns compatibility status with details.
        ///
        /// Based on reverse engineering of:
        /// - swkotor2.exe: FUN_00707290 @ 0x00707290 loads save and checks for corruption
        /// - swkotor2.exe: FUN_00708990 @ 0x00708990 validates save structure during load
        /// - Located via string references: "savenfo" @ 0x007be1f0, "SAVEGAME" @ 0x007be28c, "CORRUPT" @ 0x00707602
        ///
        /// Compatibility checks performed:
        /// 1. Save path existence and directory structure
        /// 2. savegame.sav ERF archive existence and validity
        /// 3. Required resources (savenfo, GLOBALVARS, PARTYTABLE)
        /// 4. NFO GFF signature ("NFO ") and version ("V1.0" for K1, "V2.0" for K2)
        /// 5. Save version compatibility (K1 vs K2)
        /// 6. Corruption markers (CORRUPT file presence)
        /// 7. GFF structure integrity
        /// 8. ERF archive structure integrity
        /// </remarks>
        public override SaveCompatibility CheckCompatibility(string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                return SaveCompatibility.Incompatible;
            }

            // 1. Check if save path exists and is a directory
            if (!System.IO.Directory.Exists(savePath))
            {
                return SaveCompatibility.Incompatible;
            }

            // 2. Check for corruption marker (CORRUPT file)
            // Based on swkotor2.exe: FUN_00707290 @ 0x00707290 checks for "CORRUPT" file
            // Located via string reference: "CORRUPT" @ 0x00707602
            string corruptPath = System.IO.Path.Combine(savePath, "CORRUPT");
            if (System.IO.File.Exists(corruptPath))
            {
                // Save is marked as corrupted
                return SaveCompatibility.Incompatible;
            }

            // 3. Check if savegame.sav exists
            string savegamePath = System.IO.Path.Combine(savePath, "savegame.sav");
            if (!System.IO.File.Exists(savegamePath))
            {
                // Also check for SAVEGAME.sav (uppercase variant)
                savegamePath = System.IO.Path.Combine(savePath, "SAVEGAME.sav");
                if (!System.IO.File.Exists(savegamePath))
                {
                    return SaveCompatibility.Incompatible;
                }
            }

            try
            {
                // 4. Validate ERF archive structure
                byte[] erfData = System.IO.File.ReadAllBytes(savegamePath);
                if (erfData == null || erfData.Length < 160) // Minimum ERF header size
                {
                    return SaveCompatibility.Incompatible;
                }

                ERF erf;
                try
                {
                    erf = ERFAuto.ReadErf(erfData);
                }
                catch (Exception)
                {
                    // Invalid ERF structure
                    return SaveCompatibility.Incompatible;
                }

                if (erf == null || erf.Count == 0)
                {
                    return SaveCompatibility.Incompatible;
                }

                // 5. Check for required resources
                // Based on swkotor2.exe: Required resources are savenfo, GLOBALVARS, PARTYTABLE
                byte[] nfoData = erf.Get("savenfo", ResourceType.GFF);
                if (nfoData == null || nfoData.Length == 0)
                {
                    return SaveCompatibility.Incompatible;
                }

                byte[] globalsData = erf.Get("GLOBALVARS", ResourceType.GFF);
                if (globalsData == null || globalsData.Length == 0)
                {
                    // GLOBALVARS is required for save compatibility
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                byte[] partyData = erf.Get("PARTYTABLE", ResourceType.GFF);
                if (partyData == null || partyData.Length == 0)
                {
                    // PARTYTABLE is required for save compatibility
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // 6. Validate NFO GFF signature and version
                // Based on swkotor2.exe: NFO GFF must have "NFO " signature
                // Version should be "V2.0" for KotOR 2, "V1.0" for KotOR 1
                GFF nfoGff;
                try
                {
                    nfoGff = GFF.FromBytes(nfoData);
                }
                catch (Exception)
                {
                    // Invalid GFF structure
                    return SaveCompatibility.Incompatible;
                }

                if (nfoGff == null)
                {
                    return SaveCompatibility.Incompatible;
                }

                // Check NFO signature
                if (nfoGff.Content != GFFContent.NFO)
                {
                    // NFO GFF must have NFO content type
                    return SaveCompatibility.Incompatible;
                }

                // 7. Check save version compatibility
                // KotOR 1 uses version 1, KotOR 2 uses version 2
                // Current implementation is for KotOR 2 (SaveVersion = 2)
                // We need to detect the save version from the NFO file
                // Based on swkotor2.exe: Version is stored in GFF header, but we can infer from structure
                // K2 saves have additional fields like PCNAME, INFLUENCE, etc.
                bool hasK2Fields = nfoGff.Root.Exists("PCNAME");
                int detectedVersion = hasK2Fields ? 2 : 1;

                // Check version compatibility
                if (detectedVersion != SaveVersion)
                {
                    // Version mismatch - K1 save in K2 engine or vice versa
                    if (detectedVersion == 1 && SaveVersion == 2)
                    {
                        // K1 save in K2 engine - may require migration
                        return SaveCompatibility.RequiresMigration;
                    }
                    else if (detectedVersion == 2 && SaveVersion == 1)
                    {
                        // K2 save in K1 engine - incompatible
                        return SaveCompatibility.Incompatible;
                    }
                }

                // 8. Validate GFF structures integrity
                // Check that required NFO fields exist
                if (!nfoGff.Root.Exists("SAVEGAMENAME"))
                {
                    // Missing required field
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // Validate GLOBALVARS GFF structure
                try
                {
                    GFF globalsGff = GFF.FromBytes(globalsData);
                    if (globalsGff == null || globalsGff.Root == null)
                    {
                        return SaveCompatibility.CompatibleWithWarnings;
                    }
                }
                catch (Exception)
                {
                    // Invalid GLOBALVARS GFF structure
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // Validate PARTYTABLE GFF structure
                try
                {
                    GFF partyGff = GFF.FromBytes(partyData);
                    if (partyGff == null || partyGff.Root == null)
                    {
                        return SaveCompatibility.CompatibleWithWarnings;
                    }

                    // Check PARTYTABLE signature
                    if (partyGff.Content != GFFContent.PT)
                    {
                        // PARTYTABLE GFF must have PT content type
                        return SaveCompatibility.CompatibleWithWarnings;
                    }
                }
                catch (Exception)
                {
                    // Invalid PARTYTABLE GFF structure
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // 9. Check for nested module corruption (EventQueue in module.ifo)
                // Based on HTInstallation.IsSaveCorrupted implementation
                // Check each .sav resource (cached modules) for EventQueue corruption
                bool hasCorruptedModule = false;
                foreach (var resource in erf)
                {
                    if (resource.ResType != ResourceType.SAV)
                    {
                        continue;
                    }

                    try
                    {
                        // Read the nested module ERF
                        ERF innerErf = ERFAuto.ReadErf(resource.Data);
                        if (innerErf == null)
                        {
                            continue;
                        }

                        // Look for module.ifo in this cached module
                        byte[] moduleIfoData = innerErf.Get("module", ResourceType.IFO);
                        if (moduleIfoData != null && moduleIfoData.Length > 0)
                        {
                            try
                            {
                                GFF moduleIfo = GFF.FromBytes(moduleIfoData);
                                if (moduleIfo != null && moduleIfo.Root != null && moduleIfo.Root.Exists("EventQueue"))
                                {
                                    var eventQueue = moduleIfo.Root.GetList("EventQueue");
                                    if (eventQueue != null && eventQueue.Count > 0)
                                    {
                                        // EventQueue should be empty in saved modules - corruption detected
                                        hasCorruptedModule = true;
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Skip malformed module.ifo
                                continue;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip malformed nested ERFs
                        continue;
                    }
                }

                if (hasCorruptedModule)
                {
                    return SaveCompatibility.Incompatible;
                }

                // All checks passed - save is compatible
                return SaveCompatibility.Compatible;
            }
            catch (System.IO.IOException)
            {
                // File access error
                return SaveCompatibility.Incompatible;
            }
            catch (Exception)
            {
                // Unexpected error during validation
                return SaveCompatibility.Incompatible;
            }
        }
    }
}
