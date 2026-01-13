using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Andastra.Runtime.Core.Journal
{
    /// <summary>
    /// Manages the quest/journal system (Odyssey-specific implementation).
    /// </summary>
    /// <remarks>
    /// Odyssey Journal System (swkotor.exe, swkotor2.exe):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) journal/quest system
    /// - Located via string references: "JOURNAL" @ 0x007bdf44, "NW_JOURNAL" @ 0x007c20e8, "Journal" @ 0x007c2490
    /// - "journal_p" @ 0x007ca9c4, "LBL_JOURNAL" @ 0x007c8c60, "LBL_JOURNAL_DESC" @ 0x007c8c4c
    /// - "Quest" @ 0x007c35e4, "QuestEntry" @ 0x007c35d8, "QuestState" @ 0x007c2458, "QuestCompleted" @ 0x007c2460
    /// - "NEWQUESTSOUND" @ 0x007bded8 (new quest notification sound)
    /// - Journal picture format: "]?NW_JOURNAL_PICTURE%s" @ 0x007c2a3e (journal picture reference format)
    /// - GUI: "BTN_QUESTS" @ 0x007d1058 (quests button)
    /// - Error: "Journal Crash! Partial Update on non-existant entry; plot:%s flags:%i" @ 0x007cbca8
    /// - JRL file format: GFF with "JRL " signature containing journal entry definitions
    /// - Original implementation: Journal entries stored in JRL files, quest states in global variables
    /// - Quest state storage: Quest states stored as global variables (e.g., "Q_QUESTNAME" = state value)
    /// - Quests organized by planet/category (Main, Taris, Dantooine, Kashyyyk, Manaan, Tatooine, Korriban, Party, Peragus, Telos, NarShaddaa, Dxun, Onderon, Malachor)
    /// - Multiple states per quest (progress stages: 0 = not started, 1+ = in progress, -1 = completed)
    /// - Can be marked active/completed via NWScript functions (SetJournalQuestEntry, GetJournalEntry)
    /// - Journal entries from JRL files (GFF with "JRL " signature)
    /// - Plot manager (PTT/PTM) integration for story flags - quest state changes update plot flags
    /// - Quest state changes trigger journal updates and UI notifications
    /// </remarks>
    /// <summary>
    /// Odyssey-specific journal system (backward compatibility wrapper).
    /// </summary>
    /// <remarks>
    /// This class is maintained for backward compatibility.
    /// New code should use OdysseyJournalSystem directly.
    /// </remarks>
    // Core cannot depend on Odyssey due to circular dependency, so this is a standalone implementation
    public class JournalSystem
    {
        private readonly Dictionary<string, QuestData> _quests;
        private readonly Dictionary<string, int> _questStates;
        private readonly List<JournalEntry> _entries;
        private readonly JRLLoader _jrlLoader;

        /// <summary>
        /// Event fired when quest state changes.
        /// </summary>
        public event Action<string, int, int> OnQuestStateChanged;

        /// <summary>
        /// Event fired when new journal entry added.
        /// </summary>
        public event Action<JournalEntry> OnEntryAdded;

        /// <summary>
        /// Event fired when quest completed.
        /// </summary>
        public event Action<string> OnQuestCompleted;

        public JournalSystem([CanBeNull] JRLLoader jrlLoader = null)
        {
            _quests = new Dictionary<string, QuestData>(StringComparer.OrdinalIgnoreCase);
            _questStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _entries = new List<JournalEntry>();
            _jrlLoader = jrlLoader;
        }

        #region Quest Registration

        /// <summary>
        /// Registers a quest definition.
        /// </summary>
        public void RegisterQuest(QuestData quest)
        {
            if (quest == null)
            {
                throw new ArgumentNullException("quest");
            }

            if (string.IsNullOrEmpty(quest.Tag))
            {
                throw new ArgumentException("Quest tag cannot be empty");
            }

            _quests[quest.Tag] = quest;

            // Initialize state to 0 (not started)
            if (!_questStates.ContainsKey(quest.Tag))
            {
                _questStates[quest.Tag] = 0;
            }
        }

        /// <summary>
        /// Gets quest definition by tag.
        /// </summary>
        public QuestData GetQuest(string questTag)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return null;
            }

            QuestData quest;
            if (_quests.TryGetValue(questTag, out quest))
            {
                return quest;
            }
            return null;
        }

        /// <summary>
        /// Gets all registered quests.
        /// </summary>
        public IEnumerable<QuestData> GetAllQuests()
        {
            return _quests.Values;
        }

        #endregion

        #region Quest State

        /// <summary>
        /// Gets the current state of a quest.
        /// </summary>
        public int GetQuestState(string questTag)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return 0;
            }

            int state;
            if (_questStates.TryGetValue(questTag, out state))
            {
                return state;
            }
            return 0;
        }

        /// <summary>
        /// Sets the state of a quest.
        /// </summary>
        public void SetQuestState(string questTag, int state)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return;
            }

            int oldState = GetQuestState(questTag);
            _questStates[questTag] = state;

            // Add journal entry for this state
            QuestData quest = GetQuest(questTag);
            string entryText = null;

            // Try to get text from JRL file first
            if (_jrlLoader != null)
            {
                entryText = _jrlLoader.GetQuestEntryText(questTag, state, questTag);
                if (string.IsNullOrEmpty(entryText))
                {
                    entryText = _jrlLoader.GetQuestEntryTextFromGlobal(questTag, state);
                }
            }

            // Fallback to quest stage data
            if (string.IsNullOrEmpty(entryText) && quest != null)
            {
                QuestStage stageData = quest.GetStage(state);
                if (stageData != null)
                {
                    entryText = stageData.Text;
                }
            }

            // Add entry if we have text
            if (!string.IsNullOrEmpty(entryText))
            {
                int xpReward = 0;
                if (quest != null)
                {
                    QuestStage stageData = quest.GetStage(state);
                    if (stageData != null)
                    {
                        xpReward = stageData.XPReward;
                    }
                }
                AddEntry(questTag, state, entryText, xpReward);
            }

            if (OnQuestStateChanged != null)
            {
                OnQuestStateChanged(questTag, oldState, state);
            }

            // Check for completion
            if (quest != null && state == quest.CompletionState)
            {
                if (OnQuestCompleted != null)
                {
                    OnQuestCompleted(questTag);
                }
            }
        }

        /// <summary>
        /// Advances quest to next state.
        /// </summary>
        public void AdvanceQuest(string questTag)
        {
            int currentState = GetQuestState(questTag);
            SetQuestState(questTag, currentState + 1);
        }

        /// <summary>
        /// Checks if quest has been started.
        /// </summary>
        public bool IsQuestStarted(string questTag)
        {
            return GetQuestState(questTag) > 0;
        }

        /// <summary>
        /// Checks if quest is completed.
        /// </summary>
        public bool IsQuestCompleted(string questTag)
        {
            QuestData quest = GetQuest(questTag);
            if (quest == null)
            {
                return false;
            }

            return GetQuestState(questTag) >= quest.CompletionState;
        }

        /// <summary>
        /// Checks if quest is active (started but not completed).
        /// </summary>
        public bool IsQuestActive(string questTag)
        {
            return IsQuestStarted(questTag) && !IsQuestCompleted(questTag);
        }

        #endregion

        #region Journal Entries

        /// <summary>
        /// Adds a journal entry.
        /// </summary>
        public void AddEntry(string questTag, int state, string text, int xpReward = 0)
        {
            var entry = new JournalEntry
            {
                QuestTag = questTag,
                State = state,
                Text = text,
                XPReward = xpReward,
                DateAdded = DateTime.Now
            };

            _entries.Add(entry);

            if (OnEntryAdded != null)
            {
                OnEntryAdded(entry);
            }
        }

        /// <summary>
        /// Gets all journal entries.
        /// </summary>
        public IReadOnlyList<JournalEntry> GetAllEntries()
        {
            return _entries.AsReadOnly();
        }

        /// <summary>
        /// Gets entries for a specific quest.
        /// </summary>
        public List<JournalEntry> GetEntriesForQuest(string questTag)
        {
            var result = new List<JournalEntry>();

            foreach (JournalEntry entry in _entries)
            {
                if (string.Equals(entry.QuestTag, questTag, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the most recent entry for a quest.
        /// </summary>
        public JournalEntry GetLatestEntryForQuest(string questTag)
        {
            JournalEntry latest = null;

            foreach (JournalEntry entry in _entries)
            {
                if (string.Equals(entry.QuestTag, questTag, StringComparison.OrdinalIgnoreCase))
                {
                    if (latest == null || entry.DateAdded > latest.DateAdded)
                    {
                        latest = entry;
                    }
                }
            }

            return latest;
        }

        /// <summary>
        /// Gets a journal entry by quest tag and state index.
        /// </summary>
        public JournalEntry GetEntryByState(string questTag, int state)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return null;
            }

            List<JournalEntry> entries = GetEntriesForQuest(questTag);
            if (state >= 0 && state < entries.Count)
            {
                // Entries are ordered by state, so we can index directly
                return entries[state];
            }

            return null;
        }

        /// <summary>
        /// Updates an existing journal entry.
        /// </summary>
        /// <param name="questTag">Quest tag</param>
        /// <param name="state">Quest state index</param>
        /// <param name="text">New entry text</param>
        /// <param name="xpReward">New XP reward</param>
        /// <returns>True if entry was updated, false if entry not found</returns>
        public bool UpdateEntry(string questTag, int state, string text, int xpReward = 0)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return false;
            }

            JournalEntry entry = GetEntryByState(questTag, state);
            if (entry != null)
            {
                entry.Text = text ?? string.Empty;
                entry.XPReward = xpReward;
                return true;
            }

            return false;
        }

        #endregion

        #region Quest Categories

        /// <summary>
        /// Gets quests by category.
        /// </summary>
        public List<QuestData> GetQuestsByCategory(QuestCategory category)
        {
            var result = new List<QuestData>();

            foreach (QuestData quest in _quests.Values)
            {
                if (quest.Category == category)
                {
                    result.Add(quest);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets active quests by category.
        /// </summary>
        public List<QuestData> GetActiveQuestsByCategory(QuestCategory category)
        {
            var result = new List<QuestData>();

            foreach (QuestData quest in _quests.Values)
            {
                if (quest.Category == category && IsQuestActive(quest.Tag))
                {
                    result.Add(quest);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets completed quests.
        /// </summary>
        public List<QuestData> GetCompletedQuests()
        {
            var result = new List<QuestData>();

            foreach (QuestData quest in _quests.Values)
            {
                if (IsQuestCompleted(quest.Tag))
                {
                    result.Add(quest);
                }
            }

            return result;
        }

        #endregion

        #region Save/Load Support

        /// <summary>
        /// Gets current quest states for saving.
        /// </summary>
        public Dictionary<string, int> GetAllQuestStates()
        {
            return new Dictionary<string, int>(_questStates);
        }

        /// <summary>
        /// Restores quest states from save.
        /// </summary>
        public void RestoreQuestStates(Dictionary<string, int> states)
        {
            if (states == null)
            {
                return;
            }

            foreach (KeyValuePair<string, int> kvp in states)
            {
                _questStates[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Clears all quest progress.
        /// </summary>
        public void Reset()
        {
            _questStates.Clear();
            _entries.Clear();

            // Reset all quests to state 0
            foreach (string questTag in _quests.Keys)
            {
                _questStates[questTag] = 0;
            }
        }

        #endregion
    }

    /// <summary>
    /// Quest definition data.
    /// </summary>
    public class QuestData
    {
        /// <summary>
        /// Quest tag (unique identifier).
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Quest category.
        /// </summary>
        public QuestCategory Category { get; set; }

        /// <summary>
        /// Priority (for sorting).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// State that marks quest as completed.
        /// </summary>
        public int CompletionState { get; set; }

        /// <summary>
        /// Quest stages (state -> stage data).
        /// </summary>
        public Dictionary<int, QuestStage> Stages { get; set; }

        /// <summary>
        /// Whether this is a main story quest.
        /// </summary>
        public bool IsMainQuest { get; set; }

        public QuestData()
        {
            Stages = new Dictionary<int, QuestStage>();
            CompletionState = int.MaxValue;
        }

        /// <summary>
        /// Gets stage data for a state.
        /// </summary>
        public QuestStage GetStage(int state)
        {
            QuestStage stage;
            if (Stages.TryGetValue(state, out stage))
            {
                return stage;
            }
            return null;
        }

        /// <summary>
        /// Adds a stage to the quest.
        /// </summary>
        public void AddStage(int state, string text, int xpReward = 0)
        {
            Stages[state] = new QuestStage
            {
                State = state,
                Text = text,
                XPReward = xpReward
            };
        }
    }

    /// <summary>
    /// Quest stage data.
    /// </summary>
    public class QuestStage
    {
        /// <summary>
        /// Stage state number.
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// Journal entry text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// XP reward for reaching this stage.
        /// </summary>
        public int XPReward { get; set; }

        /// <summary>
        /// Whether this stage ends the quest.
        /// </summary>
        public bool IsEndState { get; set; }
    }

    /// <summary>
    /// Journal entry record.
    /// </summary>
    public class JournalEntry
    {
        /// <summary>
        /// Quest tag.
        /// </summary>
        public string QuestTag { get; set; }

        /// <summary>
        /// Quest state when entry was added.
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// Entry text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// XP reward for this entry.
        /// </summary>
        public int XPReward { get; set; }

        /// <summary>
        /// When entry was added.
        /// </summary>
        public DateTime DateAdded { get; set; }
    }

    /// <summary>
    /// Quest categories (planets/areas in KOTOR).
    /// </summary>
    public enum QuestCategory
    {
        /// <summary>
        /// Main story quests.
        /// </summary>
        Main = 0,

        /// <summary>
        /// Taris side quests (K1).
        /// </summary>
        Taris = 1,

        /// <summary>
        /// Dantooine side quests.
        /// </summary>
        Dantooine = 2,

        /// <summary>
        /// Kashyyyk side quests.
        /// </summary>
        Kashyyyk = 3,

        /// <summary>
        /// Manaan side quests.
        /// </summary>
        Manaan = 4,

        /// <summary>
        /// Tatooine side quests.
        /// </summary>
        Tatooine = 5,

        /// <summary>
        /// Korriban side quests.
        /// </summary>
        Korriban = 6,

        /// <summary>
        /// Party member quests.
        /// </summary>
        Party = 7,

        /// <summary>
        /// Peragus side quests (K2).
        /// </summary>
        Peragus = 8,

        /// <summary>
        /// Telos side quests (K2).
        /// </summary>
        Telos = 9,

        /// <summary>
        /// Nar Shaddaa side quests (K2).
        /// </summary>
        NarShaddaa = 10,

        /// <summary>
        /// Dxun side quests (K2).
        /// </summary>
        Dxun = 11,

        /// <summary>
        /// Onderon side quests (K2).
        /// </summary>
        Onderon = 12,

        /// <summary>
        /// Malachor side quests (K2).
        /// </summary>
        Malachor = 13,

        /// <summary>
        /// Miscellaneous/other.
        /// </summary>
        Misc = 99
    }
}
