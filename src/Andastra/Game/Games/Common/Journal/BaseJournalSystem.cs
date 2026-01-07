using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common.Journal
{
    /// <summary>
    /// Base implementation of journal/quest system shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Journal System Implementation:
    /// - Common quest/journal functionality across all engines
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyJournalSystem, AuroraJournalSystem, EclipseJournalSystem)
    ///
    /// Based on reverse engineering of journal/quest systems across multiple BioWare engines.
    ///
    /// Common functionality across engines:
    /// - Quest registration and lookup by tag
    /// - Quest state management (not started, in progress, completed)
    /// - Journal entry storage and retrieval
    /// - Quest completion tracking
    /// - Event notifications for quest state changes
    /// - Journal entry addition and updates
    ///
    /// Engine-specific differences:
    /// - Odyssey (swkotor.exe, swkotor2.exe): JRL files (GFF with "JRL " signature), quest states in global variables
    /// - Aurora (nwmain.exe): JRL files (different format), CNWSJournal class, per-creature journal storage
    /// - Eclipse (daorigins.exe): Different quest system structure, may use different file format
    /// </remarks>
    public abstract class BaseJournalSystem
    {
        protected readonly Dictionary<string, BaseQuestData> _quests;
        protected readonly Dictionary<string, int> _questStates;
        protected readonly List<BaseJournalEntry> _entries;

        /// <summary>
        /// Event fired when quest state changes.
        /// </summary>
        public event Action<string, int, int> OnQuestStateChanged;

        /// <summary>
        /// Event fired when new journal entry added.
        /// </summary>
        public event Action<BaseJournalEntry> OnEntryAdded;

        /// <summary>
        /// Event fired when quest completed.
        /// </summary>
        public event Action<string> OnQuestCompleted;

        /// <summary>
        /// Invokes the OnQuestStateChanged event (for use by derived classes).
        /// </summary>
        protected void InvokeOnQuestStateChanged(string questTag, int oldState, int newState)
        {
            OnQuestStateChanged?.Invoke(questTag, oldState, newState);
        }

        /// <summary>
        /// Invokes the OnQuestCompleted event (for use by derived classes).
        /// </summary>
        protected void InvokeOnQuestCompleted(string questTag)
        {
            OnQuestCompleted?.Invoke(questTag);
        }

        /// <summary>
        /// Invokes the OnEntryAdded event (for use by derived classes).
        /// </summary>
        protected void InvokeOnEntryAdded(BaseJournalEntry entry)
        {
            OnEntryAdded?.Invoke(entry);
        }

        protected BaseJournalSystem()
        {
            _quests = new Dictionary<string, BaseQuestData>(StringComparer.OrdinalIgnoreCase);
            _questStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _entries = new List<BaseJournalEntry>();
        }

        #region Quest Registration

        /// <summary>
        /// Registers a quest definition.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Quest registration with tag and completion state.
        /// Engine-specific subclasses handle engine-specific quest data structures.
        /// </remarks>
        public virtual void RegisterQuest(BaseQuestData quest)
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
        /// <remarks>
        /// Common across all engines: Quest lookup by tag (case-insensitive).
        /// </remarks>
        [CanBeNull]
        public BaseQuestData GetQuest(string questTag)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return null;
            }

            BaseQuestData quest;
            if (_quests.TryGetValue(questTag, out quest))
            {
                return quest;
            }
            return null;
        }

        /// <summary>
        /// Gets all registered quests.
        /// </summary>
        public IEnumerable<BaseQuestData> GetAllQuests()
        {
            return _quests.Values;
        }

        #endregion

        #region Quest State

        /// <summary>
        /// Gets the current state of a quest.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Quest state retrieval (0 = not started, 1+ = in progress, -1 or max = completed).
        /// </remarks>
        public virtual int GetQuestState(string questTag)
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
        /// <remarks>
        /// Common across all engines: Quest state management with journal entry updates.
        /// Engine-specific subclasses handle engine-specific journal entry text lookup and formatting.
        /// </remarks>
        public virtual void SetQuestState(string questTag, int state)
        {
            if (string.IsNullOrEmpty(questTag))
            {
                return;
            }

            int oldState = GetQuestState(questTag);
            _questStates[questTag] = state;

            // Add journal entry for this state (engine-specific implementation)
            ProcessQuestStateChange(questTag, state, oldState);

            OnQuestStateChanged?.Invoke(questTag, oldState, state);

            // Check for completion
            BaseQuestData quest = GetQuest(questTag);
            if (quest != null && state == quest.CompletionState)
            {
                OnQuestCompleted?.Invoke(questTag);
            }
        }

        /// <summary>
        /// Processes quest state change (engine-specific implementation).
        /// </summary>
        /// <remarks>
        /// Engine-specific subclasses implement this to handle journal entry text lookup and formatting.
        /// </remarks>
        protected abstract void ProcessQuestStateChange(string questTag, int newState, int oldState);

        /// <summary>
        /// Advances quest to next state.
        /// </summary>
        public virtual void AdvanceQuest(string questTag)
        {
            int currentState = GetQuestState(questTag);
            SetQuestState(questTag, currentState + 1);
        }

        /// <summary>
        /// Checks if quest has been started.
        /// </summary>
        public virtual bool IsQuestStarted(string questTag)
        {
            return GetQuestState(questTag) > 0;
        }

        /// <summary>
        /// Checks if quest is completed.
        /// </summary>
        public virtual bool IsQuestCompleted(string questTag)
        {
            BaseQuestData quest = GetQuest(questTag);
            if (quest == null)
            {
                return false;
            }

            return GetQuestState(questTag) >= quest.CompletionState;
        }

        #endregion

        #region Journal Entries

        /// <summary>
        /// Adds a journal entry.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Journal entry addition with quest tag, state, text, and XP reward.
        /// </remarks>
        public virtual void AddEntry(string questTag, int state, string text, int xpReward = 0)
        {
            var entry = CreateJournalEntry(questTag, state, text, xpReward);
            _entries.Add(entry);

            OnEntryAdded?.Invoke(entry);
        }

        /// <summary>
        /// Creates a journal entry (engine-specific implementation).
        /// </summary>
        /// <remarks>
        /// Engine-specific subclasses implement this to create engine-specific journal entry types.
        /// </remarks>
        protected abstract BaseJournalEntry CreateJournalEntry(string questTag, int state, string text, int xpReward);

        /// <summary>
        /// Gets all journal entries.
        /// </summary>
        public IReadOnlyList<BaseJournalEntry> GetAllEntries()
        {
            return _entries;
        }

        /// <summary>
        /// Gets journal entries for a specific quest.
        /// </summary>
        public virtual List<BaseJournalEntry> GetEntriesForQuest(string questTag)
        {
            var result = new List<BaseJournalEntry>();
            foreach (BaseJournalEntry entry in _entries)
            {
                if (string.Equals(entry.QuestTag, questTag, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the latest journal entry for a quest.
        /// </summary>
        [CanBeNull]
        public virtual BaseJournalEntry GetLatestEntryForQuest(string questTag)
        {
            BaseJournalEntry latest = null;
            foreach (BaseJournalEntry entry in _entries)
            {
                if (string.Equals(entry.QuestTag, questTag, StringComparison.OrdinalIgnoreCase))
                {
                    if (latest == null || entry.State > latest.State)
                    {
                        latest = entry;
                    }
                }
            }
            return latest;
        }

        /// <summary>
        /// Gets a specific journal entry by quest tag and state.
        /// </summary>
        [CanBeNull]
        public virtual BaseJournalEntry GetEntryByState(string questTag, int state)
        {
            foreach (BaseJournalEntry entry in _entries)
            {
                if (string.Equals(entry.QuestTag, questTag, StringComparison.OrdinalIgnoreCase) && entry.State == state)
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// Updates an existing journal entry.
        /// </summary>
        public virtual bool UpdateEntry(string questTag, int state, string text, int xpReward = 0)
        {
            BaseJournalEntry entry = GetEntryByState(questTag, state);
            if (entry != null)
            {
                entry.Text = text ?? string.Empty;
                entry.XPReward = xpReward;
                return true;
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// Base quest data structure shared across all engines.
    /// </summary>
    public abstract class BaseQuestData
    {
        /// <summary>
        /// Quest tag/identifier.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// State value that indicates quest completion.
        /// </summary>
        public int CompletionState { get; set; }

        /// <summary>
        /// Gets quest stage data for a specific state (engine-specific implementation).
        /// </summary>
        public abstract BaseQuestStage GetStage(int state);
    }

    /// <summary>
    /// Base quest stage data structure shared across all engines.
    /// </summary>
    public abstract class BaseQuestStage
    {
        /// <summary>
        /// Stage state value.
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// Journal entry text for this stage.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// XP reward for this stage.
        /// </summary>
        public int XPReward { get; set; }
    }

    /// <summary>
    /// Base journal entry structure shared across all engines.
    /// </summary>
    public class BaseJournalEntry
    {
        /// <summary>
        /// Quest tag this entry belongs to.
        /// </summary>
        public string QuestTag { get; set; }

        /// <summary>
        /// Quest state this entry represents.
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// Journal entry text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// XP reward for this entry.
        /// </summary>
        public int XPReward { get; set; }

        /// <summary>
        /// When this entry was added.
        /// </summary>
        public DateTime DateAdded { get; set; }
    }
}

