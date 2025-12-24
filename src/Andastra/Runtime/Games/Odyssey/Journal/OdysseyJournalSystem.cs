using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Journal;
using Andastra.Runtime.Games.Common.Journal;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Odyssey.Journal
{
    /// <summary>
    /// Odyssey-specific journal system implementation (swkotor.exe, swkotor2.exe).
    /// </summary>
    /// <remarks>
    /// Odyssey Journal System:
    /// - Based on swkotor.exe: Quest processing (FUN_0059f5f0 @ 0x0059f5f0, "Quest" @ 0x0074a5dc, "QuestEntry" @ 0x0074a5d0)
    /// - Based on swkotor2.exe: Journal system ("JOURNAL" @ 0x007bdf44, "Quest" @ 0x007c35e4, "QuestEntry" @ 0x007c35d8)
    /// - JRL file format: GFF with "JRL " signature containing journal entry definitions
    /// - Quest state storage: Quest states stored as global variables (e.g., "Q_QUESTNAME" = state value)
    /// - Quests organized by planet/category (Main, Taris, Dantooine, Kashyyyk, Manaan, Tatooine, Korriban, Party, Peragus, Telos, NarShaddaa, Dxun, Onderon, Malachor)
    /// - Multiple states per quest (progress stages: 0 = not started, 1+ = in progress, -1 = completed)
    /// - Journal entries from JRL files (GFF with "JRL " signature)
    /// - Plot manager (PTT/PTM) integration for story flags - quest state changes update plot flags
    /// </remarks>
    public class OdysseyJournalSystem : BaseJournalSystem
    {
        [CanBeNull]
        private readonly JRLLoader _jrlLoader;

        public OdysseyJournalSystem([CanBeNull] JRLLoader jrlLoader = null)
            : base()
        {
            _jrlLoader = jrlLoader;
        }

        /// <summary>
        /// Processes quest state change with JRL text lookup.
        /// </summary>
        protected override void ProcessQuestStateChange(string questTag, int newState, int oldState)
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
        /// Creates an Odyssey-specific journal entry.
        /// </summary>
        protected override BaseJournalEntry CreateJournalEntry(string questTag, int state, string text, int xpReward)
        {
            // Create a BaseJournalEntry (not CoreJournalEntry) to match the base class contract
            return new BaseJournalEntry
            {
                QuestTag = questTag,
                State = state,
                Text = text,
                XPReward = xpReward,
                DateAdded = DateTime.Now
            };
        }

        /// <summary>
        /// Registers a quest using Odyssey-specific quest data.
        /// </summary>
        public void RegisterQuest(QuestData quest)
        {
            base.RegisterQuest(new OdysseyQuestDataAdapter(quest));
        }

        /// <summary>
        /// Gets quest definition by tag (Odyssey-specific type).
        /// </summary>
        [CanBeNull]
        public QuestData GetQuestData(string questTag)
        {
            BaseQuestData baseQuest = GetQuest(questTag);
            if (baseQuest is OdysseyQuestDataAdapter adapter)
            {
                return adapter.QuestData;
            }
            return null;
        }

        /// <summary>
        /// Gets journal entries (Odyssey-specific type).
        /// </summary>
        public new IReadOnlyList<BaseJournalEntry> GetAllEntries()
        {
            return base.GetAllEntries();
        }

        /// <summary>
        /// Gets journal entries for a quest (Odyssey-specific type).
        /// </summary>
        public new List<BaseJournalEntry> GetEntriesForQuest(string questTag)
        {
            return base.GetEntriesForQuest(questTag);
        }

        /// <summary>
        /// Gets latest entry for a quest (Odyssey-specific type).
        /// </summary>
        [CanBeNull]
        public new BaseJournalEntry GetLatestEntryForQuest(string questTag)
        {
            return base.GetLatestEntryForQuest(questTag);
        }

        /// <summary>
        /// Gets entry by state (Odyssey-specific type).
        /// </summary>
        [CanBeNull]
        public new BaseJournalEntry GetEntryByState(string questTag, int state)
        {
            return base.GetEntryByState(questTag, state);
        }

        /// <summary>
        /// Updates an entry (Odyssey-specific type).
        /// </summary>
        public new bool UpdateEntry(string questTag, int state, string text, int xpReward = 0)
        {
            return base.UpdateEntry(questTag, state, text, xpReward);
        }

        /// <summary>
        /// Adapter to convert Odyssey QuestData to BaseQuestData.
        /// </summary>
        private class OdysseyQuestDataAdapter : BaseQuestData
        {
            public QuestData QuestData { get; }

            public OdysseyQuestDataAdapter(QuestData questData)
            {
                QuestData = questData ?? throw new ArgumentNullException("questData");
                Tag = questData.Tag;
                CompletionState = questData.CompletionState;
            }

            public override BaseQuestStage GetStage(int state)
            {
                QuestStage stage = QuestData.GetStage(state);
                if (stage == null)
                {
                    return null;
                }

                return new OdysseyQuestStageAdapter(stage);
            }
        }

        /// <summary>
        /// Adapter to convert Odyssey QuestStage to BaseQuestStage.
        /// </summary>
        private class OdysseyQuestStageAdapter : BaseQuestStage
        {
            public OdysseyQuestStageAdapter(QuestStage stage)
            {
                State = stage.State;
                Text = stage.Text;
                XPReward = stage.XPReward;
            }
        }
    }
}

