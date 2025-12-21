using System;
using System.Collections.Generic;
using Andastra.Runtime.Games.Common.Journal;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora.Journal
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

        public AuroraJournalSystem([CanBeNull] AuroraJRLLoader jrlLoader = null)
            : base()
        {
            _jrlLoader = jrlLoader;
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

            if (OnQuestStateChanged != null)
            {
                OnQuestStateChanged(questTag, oldState, state);
            }

            // Check for completion
            BaseQuestData quest = GetQuest(questTag);
            if (quest != null && state == quest.CompletionState)
            {
                if (OnQuestCompleted != null)
                {
                    OnQuestCompleted(questTag);
                }
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
        /// </remarks>
        public void ReloadJournalEntries(uint creatureId)
        {
            // Based on nwmain.exe: ReloadJournalEntries scans creature's script variables
            // and reloads journal entries from JRL files based on quest tags found
            // This is a simplified implementation - full implementation would scan script variables
            // and reload entries from JRL files matching quest tags

            // TODO: STUB - For now, we'll just clear the creature's quest states and let them be re-established
            if (_creatureQuestStates.ContainsKey(creatureId))
            {
                _creatureQuestStates[creatureId].Clear();
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
