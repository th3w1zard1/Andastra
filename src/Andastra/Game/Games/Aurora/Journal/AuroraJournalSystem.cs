using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Journal;
using Andastra.Game.Scripting.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora.Journal
{
    /// <summary>
    /// Aurora-specific journal system implementation (nwmain.exe - Neverwinter Nights).
    /// </summary>
    /// <remarks>
    /// Aurora Journal System (nwmain.exe):
    /// - Based on nwmain.exe: CNWSJournal, AddJournalEntry, GetJournal, ReloadJournalEntries
    /// - Located via string references: "Journal" @ 0x140dc9e38, "Quest" @ 0x140de6ff8, "QuestEntry" @ 0x140de7000
    /// - "NW_JOURNAL" @ 0x140ddf0e8, "NW_JOURNAL_ENTRY%s" @ 0x140de7108, "NW_JOURNAL_DATE%s" @ 0x140de7120
    /// - "Journal_AddQuest" @ 0x140dcb980, "Journal_RemoveQuest" @ 0x140dcb998, "Journal_SetQuestPicture" @ 0x140dcb9b0
    /// - "Journal_FullUpdate" @ 0x140dcb9c8, "Journal_Updated" @ 0x140dcba18
    /// - CNWSJournal class: Per-creature journal storage (CNWSCreature::GetJournal @ 0x140391790)
    /// - CNWSCreature::ReloadJournalEntries @ 0x14039ddb0 - reloads journal entries from JRL files
    /// - CNWSDialog::AddJournalEntry @ 0x140419e70 - adds journal entry from dialogue
    /// - HandlePlayerToServerJournalMessage @ 0x140456d30 - handles journal updates from client
    /// - JRL file format: Similar to Odyssey but may have differences in structure
    /// - Journal entries stored per-creature (each player has their own journal)
    /// - Quest state management similar to Odyssey but with per-creature storage
    /// - Journal synchronization between client and server in multiplayer
    /// </remarks>
    public class AuroraJournalSystem : BaseJournalSystem
    {
        [CanBeNull]
        private readonly AuroraJRLLoader _jrlLoader;
        private readonly Dictionary<uint, Dictionary<string, int>> _creatureQuestStates; // creatureId -> questTag -> state
        [CanBeNull]
        private readonly IScriptGlobals _scriptGlobals;
        [CanBeNull]
        private readonly IWorld _world;

        public AuroraJournalSystem([CanBeNull] AuroraJRLLoader jrlLoader = null, [CanBeNull] IScriptGlobals scriptGlobals = null, [CanBeNull] IWorld world = null)
            : base()
        {
            _jrlLoader = jrlLoader;
            _scriptGlobals = scriptGlobals;
            _world = world;
            _creatureQuestStates = new Dictionary<uint, Dictionary<string, int>>();
        }

        /// <summary>
        /// Gets quest state for a specific creature (Aurora-specific: per-creature journal storage).
        /// </summary>
        /// <param name="creatureId">Creature ID (player/character ID).</param>
        /// <param name="questTag">Quest tag.</param>
        /// <returns>Quest state for the creature.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreature::GetJournal - each creature has its own journal
        /// Original implementation: Journal state is stored per-creature, not globally
        /// </remarks>
        public int GetQuestStateForCreature(uint creatureId, string questTag)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return 0;
            }

            Dictionary<string, int> creatureStates;
            if (_creatureQuestStates.TryGetValue(creatureId, out creatureStates))
            {
                int state;
                if (creatureStates.TryGetValue(questTag, out state))
                {
                    return state;
                }
            }

            return 0;
        }

        /// <summary>
        /// Sets quest state for a specific creature (Aurora-specific: per-creature journal storage).
        /// </summary>
        /// <param name="creatureId">Creature ID (player/character ID).</param>
        /// <param name="questTag">Quest tag.</param>
        /// <param name="state">New quest state.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreature journal state management
        /// Original implementation: Each creature maintains its own quest states
        /// </remarks>
        public void SetQuestStateForCreature(uint creatureId, string questTag, int state)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return;
            }

            Dictionary<string, int> creatureStates;
            if (!_creatureQuestStates.TryGetValue(creatureId, out creatureStates))
            {
                creatureStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _creatureQuestStates[creatureId] = creatureStates;
            }

            int oldState = GetQuestStateForCreature(creatureId, questTag);
            creatureStates[questTag] = state;

            // Process quest state change (engine-specific implementation)
            ProcessQuestStateChangeForCreature(creatureId, questTag, state, oldState);

            InvokeOnQuestStateChanged(questTag, oldState, state);

            // Check for completion
            BaseQuestData quest = GetQuest(questTag);
            if (quest != null && state == quest.CompletionState)
            {
                InvokeOnQuestCompleted(questTag);
            }
        }

        /// <summary>
        /// Processes quest state change for a specific creature (Aurora-specific).
        /// </summary>
        private void ProcessQuestStateChangeForCreature(uint creatureId, string questTag, int newState, int oldState)
        {
            string entryText = null;

            // Try to get text from JRL file first
            if (_jrlLoader != null)
            {
                entryText = _jrlLoader.GetQuestEntryText(questTag, newState, questTag);
                if (string.IsNullOrEmpty(entryText))
                {
                    entryText = _jrlLoader.GetQuestEntryTextFromGlobal(questTag, newState);
                }
            }

            // Fallback to quest stage data
            if (string.IsNullOrEmpty(entryText))
            {
                BaseQuestData quest = GetQuest(questTag);
                if (quest != null)
                {
                    BaseQuestStage stage = quest.GetStage(newState);
                    if (stage != null)
                    {
                        entryText = stage.Text;
                    }
                }
            }

            // Add entry if we have text
            if (!string.IsNullOrEmpty(entryText))
            {
                int xpReward = 0;
                BaseQuestData quest = GetQuest(questTag);
                if (quest != null)
                {
                    BaseQuestStage stage = quest.GetStage(newState);
                    if (stage != null)
                    {
                        xpReward = stage.XPReward;
                    }
                }
                AddEntry(questTag, newState, entryText, xpReward);
            }
        }

        /// <summary>
        /// Processes quest state change (base implementation - uses global state).
        /// </summary>
        protected override void ProcessQuestStateChange(string questTag, int newState, int oldState)
        {
            // Aurora uses per-creature state, but we still support global state for compatibility
            string entryText = null;

            // Try to get text from JRL file first
            if (_jrlLoader != null)
            {
                entryText = _jrlLoader.GetQuestEntryText(questTag, newState, questTag);
                if (string.IsNullOrEmpty(entryText))
                {
                    entryText = _jrlLoader.GetQuestEntryTextFromGlobal(questTag, newState);
                }
            }

            // Fallback to quest stage data
            if (string.IsNullOrEmpty(entryText))
            {
                BaseQuestData quest = GetQuest(questTag);
                if (quest != null)
                {
                    BaseQuestStage stage = quest.GetStage(newState);
                    if (stage != null)
                    {
                        entryText = stage.Text;
                    }
                }
            }

            // Add entry if we have text
            if (!string.IsNullOrEmpty(entryText))
            {
                int xpReward = 0;
                BaseQuestData quest = GetQuest(questTag);
                if (quest != null)
                {
                    BaseQuestStage stage = quest.GetStage(newState);
                    if (stage != null)
                    {
                        xpReward = stage.XPReward;
                    }
                }
                AddEntry(questTag, newState, entryText, xpReward);
            }
        }

        /// <summary>
        /// Creates an Aurora-specific journal entry.
        /// </summary>
        protected override BaseJournalEntry CreateJournalEntry(string questTag, int state, string text, int xpReward)
        {
            return new AuroraJournalEntry
            {
                QuestTag = questTag,
                State = state,
                Text = text,
                XPReward = xpReward,
                DateAdded = DateTime.Now
            };
        }

        /// <summary>
        /// Reloads journal entries from JRL files (Aurora-specific).
        /// </summary>
        /// <param name="creatureId">Creature ID to reload entries for.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreature::ReloadJournalEntries @ 0x14039ddb0
        /// Original implementation: Reloads journal entries from JRL files for a specific creature
        ///
        /// Implementation details (nwmain.exe: 0x14039ddb0):
        /// - Scans creature's local script variables for variables matching "NW_JOURNAL_ENTRY%s" pattern
        /// - For each matching variable, extracts quest tag from variable name and state from variable value
        /// - Loads corresponding JRL file and rebuilds journal entries for all quest states found
        /// - Clears existing journal entries for this creature before reloading
        /// - Variable format: "NW_JOURNAL_ENTRY<questTag>" = <entryId/state>
        /// - Also checks "NW_JOURNAL_DATE%s" variables for entry timestamps (if applicable)
        /// </remarks>
        public void ReloadJournalEntries(uint creatureId)
        {
            if (_scriptGlobals == null || _world == null || _jrlLoader == null)
            {
                // If dependencies not available, just clear quest states (fallback behavior)
                if (_creatureQuestStates.ContainsKey(creatureId))
                {
                    _creatureQuestStates[creatureId].Clear();
                }
                return;
            }

            // Get the creature entity
            IEntity creature = _world.GetEntity(creatureId);
            if (creature == null)
            {
                return;
            }

            // Clear existing journal entries for this creature
            // Based on nwmain.exe: CNWSCreature::ReloadJournalEntries clears existing entries before reloading
            RemoveEntriesForCreature(creatureId);

            // Clear quest states for this creature (will be rebuilt from script variables)
            if (_creatureQuestStates.ContainsKey(creatureId))
            {
                _creatureQuestStates[creatureId].Clear();
            }

            // Scan creature's local integer variables for journal entry variables
            // Pattern: "NW_JOURNAL_ENTRY<questTag>" = <entryId/state>
            // Based on nwmain.exe: ReloadJournalEntries scans all local variables matching this pattern
            const string journalEntryPrefix = "NW_JOURNAL_ENTRY";

            foreach (string varName in _scriptGlobals.EnumerateLocalInts(creature))
            {
                // Check if variable matches journal entry pattern
                if (!varName.StartsWith(journalEntryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Extract quest tag from variable name (everything after "NW_JOURNAL_ENTRY")
                string questTag = varName.Substring(journalEntryPrefix.Length);
                if (string.IsNullOrEmpty(questTag))
                {
                    continue;
                }

                // Get entry ID/state from variable value
                int entryId = _scriptGlobals.GetLocalInt(creature, varName);
                if (entryId <= 0)
                {
                    // Skip entries with invalid state (0 or negative typically means not started/invalid)
                    continue;
                }

                // Load journal entry text from JRL file
                // Based on nwmain.exe: JRL files are loaded using quest tag as ResRef
                string entryText = _jrlLoader.GetQuestEntryText(questTag, entryId, questTag);
                if (string.IsNullOrEmpty(entryText))
                {
                    // Try global.jrl as fallback
                    entryText = _jrlLoader.GetQuestEntryTextFromGlobal(questTag, entryId);
                }

                // If we have entry text, add the journal entry
                if (!string.IsNullOrEmpty(entryText))
                {
                    // Get XP reward from quest data if available
                    int xpReward = 0;
                    BaseQuestData quest = GetQuest(questTag);
                    if (quest != null)
                    {
                        BaseQuestStage stage = quest.GetStage(entryId);
                        if (stage != null)
                        {
                            xpReward = stage.XPReward;
                        }
                    }

                    // Create and add journal entry with creature association
                    var entry = new AuroraJournalEntry
                    {
                        QuestTag = questTag,
                        State = entryId,
                        Text = entryText,
                        XPReward = xpReward,
                        DateAdded = DateTime.Now,
                        CreatureId = creatureId
                    };

                    // Add to entries list (base class stores entries globally, but we track creatureId)
                    _entries.Add(entry);

                    // Update quest state for this creature
                    Dictionary<string, int> creatureStates;
                    if (!_creatureQuestStates.TryGetValue(creatureId, out creatureStates))
                    {
                        creatureStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        _creatureQuestStates[creatureId] = creatureStates;
                    }
                    creatureStates[questTag] = entryId;

                    // Fire entry added event
                    InvokeOnEntryAdded(entry);
                }
            }
        }

        /// <summary>
        /// Removes all journal entries for a specific creature.
        /// </summary>
        private void RemoveEntriesForCreature(uint creatureId)
        {
            // Based on nwmain.exe: CNWSCreature::ReloadJournalEntries removes existing entries for creature
            // We need to remove entries from the base class's _entries list that belong to this creature
            // Since _entries is protected in base class, we access it directly
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                AuroraJournalEntry entry = _entries[i] as AuroraJournalEntry;
                if (entry != null && entry.CreatureId == creatureId)
                {
                    _entries.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Aurora-specific journal entry with creature association.
        /// </summary>
        public class AuroraJournalEntry : BaseJournalEntry
        {
            /// <summary>
            /// Creature ID this entry belongs to (Aurora-specific: per-creature journals).
            /// </summary>
            public uint CreatureId { get; set; }
        }
    }
}
