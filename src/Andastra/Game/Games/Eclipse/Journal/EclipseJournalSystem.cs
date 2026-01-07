using System;
using System.Collections.Generic;
using Andastra.Runtime.Games.Common.Journal;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Journal
{
    /// <summary>
    /// Eclipse-specific journal system implementation (daorigins.exe, DragonAge2.exe - Dragon Age series).
    /// </summary>
    /// <remarks>
    /// Eclipse Journal System (daorigins.exe, DragonAge2.exe):
    /// - Based on daorigins.exe: Quest system
    /// - Located via string references: "Quest" @ 0x00b0849c, "QuestCompleted" @ 0x00b0847c, "QuestName" @ 0x00b0849c, "QuestResRef" @ 0x00b084b0
    /// - "VJournal" @ 0x00ae88da, "JournalText" @ 0x00afac68, "GUIJournal" @ 0x00afc938
    /// - "ShowJournalGUIMessage" @ 0x00ae89f8, "HideJournalGUIMessage" @ 0x00ae8a24
    /// - "GoToQuestMessage" @ 0x00ae8bbc, "VSetActiveQuestMessage" @ 0x00afa6fa
    /// - "ExpandedInCompletedQuests" @ 0x00afaa70, "ExpandedInCurrentQuests" @ 0x00afaaa4
    /// - "LastSelectedQuest" @ 0x00afc830, "LastSelectedCompletedQuest" @ 0x00afc7c8
    /// - "NotificationQuest" @ 0x00afc914, "NotificationQuestIsCompleted" @ 0x00afc8a8
    /// - Eclipse uses a different quest system structure than Odyssey/Aurora
    /// - Quest system may use different file format or storage mechanism
    /// - Journal entries may be stored differently (possibly in save files or different format)
    /// - Quest state management similar to Odyssey but with Eclipse-specific differences
    /// - GUI integration: "GUIJournal", "ShowJournalGUIMessage", "HideJournalGUIMessage"
    /// - Quest organization: "ExpandedInCompletedQuests", "ExpandedInCurrentQuests" suggests tree/category structure
    /// </remarks>
    public class EclipseJournalSystem : BaseJournalSystem
    {
        [CanBeNull]
        private readonly EclipseJRLLoader _jrlLoader;
        private readonly Dictionary<string, string> _questResRefs; // questTag -> resRef mapping

        public EclipseJournalSystem([CanBeNull] EclipseJRLLoader jrlLoader = null)
            : base()
        {
            _jrlLoader = jrlLoader;
            _questResRefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sets quest ResRef (Eclipse-specific: quests have ResRef identifiers).
        /// </summary>
        /// <param name="questTag">Quest tag.</param>
        /// <param name="resRef">Quest ResRef.</param>
        /// <remarks>
        /// Based on daorigins.exe: "QuestResRef" @ 0x00b084b0
        /// Original implementation: Quests have ResRef identifiers in addition to tags
        /// </remarks>
        public void SetQuestResRef(string questTag, string resRef)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return;
            }

            _questResRefs[questTag] = resRef ?? string.Empty;
        }

        /// <summary>
        /// Gets quest ResRef (Eclipse-specific).
        /// </summary>
        /// <param name="questTag">Quest tag.</param>
        /// <returns>Quest ResRef, or null if not set.</returns>
        [CanBeNull]
        public string GetQuestResRef(string questTag)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return null;
            }

            string resRef;
            if (_questResRefs.TryGetValue(questTag, out resRef))
            {
                return resRef;
            }
            return null;
        }

        /// <summary>
        /// Processes quest state change with Eclipse-specific text lookup.
        /// </summary>
        protected override void ProcessQuestStateChange(string questTag, int newState, int oldState)
        {
            string entryText = null;

            // Try to get text from JRL file first (if Eclipse uses JRL format)
            if (_jrlLoader != null)
            {
                // Eclipse may use ResRef instead of quest tag for JRL lookup
                string jrlResRef = GetQuestResRef(questTag) ?? questTag;
                entryText = _jrlLoader.GetQuestEntryText(questTag, newState, jrlResRef);
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
        /// Creates an Eclipse-specific journal entry.
        /// </summary>
        protected override BaseJournalEntry CreateJournalEntry(string questTag, int state, string text, int xpReward)
        {
            return new EclipseJournalEntry
            {
                QuestTag = questTag,
                State = state,
                Text = text,
                XPReward = xpReward,
                DateAdded = DateTime.Now
            };
        }

        /// <summary>
        /// Eclipse-specific journal entry.
        /// </summary>
        public class EclipseJournalEntry : BaseJournalEntry
        {
            /// <summary>
            /// Quest ResRef (Eclipse-specific: quests have ResRef identifiers).
            /// </summary>
            public string QuestResRef { get; set; }
        }
    }
}

