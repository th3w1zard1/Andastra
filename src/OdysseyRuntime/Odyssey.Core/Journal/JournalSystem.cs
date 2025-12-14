using System;
using System.Collections.Generic;

namespace Odyssey.Core.Journal
{
    /// <summary>
    /// Manages the quest/journal system.
    /// </summary>
    /// <remarks>
    /// KOTOR Journal System:
    /// - Quests organized by planet/category
    /// - Multiple states per quest (progress stages)
    /// - Can be marked active/completed
    /// - Journal entries from JRL files
    /// - Plot manager (PTT/PTM) integration for story flags
    /// </remarks>
    public class JournalSystem
    {
        private readonly Dictionary<string, QuestData> _quests;
        private readonly Dictionary<string, int> _questStates;
        private readonly List<JournalEntry> _entries;

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

        public JournalSystem()
        {
            _quests = new Dictionary<string, QuestData>(StringComparer.OrdinalIgnoreCase);
            _questStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _entries = new List<JournalEntry>();
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
            var quest = GetQuest(questTag);
            if (quest != null)
            {
                var stageData = quest.GetStage(state);
                if (stageData != null)
                {
                    AddEntry(questTag, state, stageData.Text, stageData.XPReward);
                }
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
            var quest = GetQuest(questTag);
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

            foreach (var entry in _entries)
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

            foreach (var entry in _entries)
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

        #endregion

        #region Quest Categories

        /// <summary>
        /// Gets quests by category.
        /// </summary>
        public List<QuestData> GetQuestsByCategory(QuestCategory category)
        {
            var result = new List<QuestData>();

            foreach (var quest in _quests.Values)
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

            foreach (var quest in _quests.Values)
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

            foreach (var quest in _quests.Values)
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

            foreach (var kvp in states)
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
            foreach (var questTag in _quests.Keys)
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
