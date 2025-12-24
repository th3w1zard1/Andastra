using System;
using System.Collections.Generic;
using Andastra.Runtime.Games.Common.Journal;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andastra.Tests.Runtime.Games.Common.Journal
{
    /// <summary>
    /// Tests for BaseJournalSystem common functionality.
    /// </summary>
    public class BaseJournalSystemTests
    {
        private TestJournalSystem _journalSystem;

        public BaseJournalSystemTests()
        {
            _journalSystem = new TestJournalSystem();
        }

        [Fact]
        public void RegisterQuest_WithValidQuest_RegistersQuest()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };

            // Act
            _journalSystem.RegisterQuest(quest);

            // Assert
            BaseQuestData retrieved = _journalSystem.GetQuest("test_quest");
            retrieved.Should().NotBeNull();
            retrieved.Tag.Should().Be("test_quest");
            retrieved.CompletionState.Should().Be(5);
        }

        [Fact]
        public void RegisterQuest_WithNullQuest_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _journalSystem.RegisterQuest(null));
        }

        [Fact]
        public void RegisterQuest_WithEmptyTag_ThrowsArgumentException()
        {
            // Arrange
            var quest = new TestQuestData { Tag = string.Empty };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _journalSystem.RegisterQuest(quest));
        }

        [Fact]
        public void GetQuestState_UnregisteredQuest_ReturnsZero()
        {
            // Act
            int state = _journalSystem.GetQuestState("nonexistent");

            // Assert
            state.Should().Be(0);
        }

        [Fact]
        public void SetQuestState_WithValidQuest_UpdatesState()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);

            // Act
            _journalSystem.SetQuestState("test_quest", 3);

            // Assert
            int state = _journalSystem.GetQuestState("test_quest");
            state.Should().Be(3);
        }

        [Fact]
        public void SetQuestState_WithValidQuest_FiresOnQuestStateChanged()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            string changedQuestTag = null;
            int oldState = -1;
            int newState = -1;
            _journalSystem.OnQuestStateChanged += (tag, old, @new) =>
            {
                changedQuestTag = tag;
                oldState = old;
                newState = @new;
            };

            // Act
            _journalSystem.SetQuestState("test_quest", 3);

            // Assert
            changedQuestTag.Should().Be("test_quest");
            oldState.Should().Be(0);
            newState.Should().Be(3);
        }

        [Fact]
        public void SetQuestState_CompletingQuest_FiresOnQuestCompleted()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            string completedQuestTag = null;
            _journalSystem.OnQuestCompleted += (tag) => { completedQuestTag = tag; };

            // Act
            _journalSystem.SetQuestState("test_quest", 5);

            // Assert
            completedQuestTag.Should().Be("test_quest");
        }

        [Fact]
        public void IsQuestStarted_UnstartedQuest_ReturnsFalse()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);

            // Act
            bool started = _journalSystem.IsQuestStarted("test_quest");

            // Assert
            started.Should().BeFalse();
        }

        [Fact]
        public void IsQuestStarted_StartedQuest_ReturnsTrue()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            _journalSystem.SetQuestState("test_quest", 1);

            // Act
            bool started = _journalSystem.IsQuestStarted("test_quest");

            // Assert
            started.Should().BeTrue();
        }

        [Fact]
        public void IsQuestCompleted_UncompletedQuest_ReturnsFalse()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            _journalSystem.SetQuestState("test_quest", 3);

            // Act
            bool completed = _journalSystem.IsQuestCompleted("test_quest");

            // Assert
            completed.Should().BeFalse();
        }

        [Fact]
        public void IsQuestCompleted_CompletedQuest_ReturnsTrue()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            _journalSystem.SetQuestState("test_quest", 5);

            // Act
            bool completed = _journalSystem.IsQuestCompleted("test_quest");

            // Assert
            completed.Should().BeTrue();
        }

        [Fact]
        public void AddEntry_WithValidData_AddsEntry()
        {
            // Act
            _journalSystem.AddEntry("test_quest", 1, "Test entry text", 100);

            // Assert
            IReadOnlyList<BaseJournalEntry> entries = _journalSystem.GetAllEntries();
            entries.Count.Should().Be(1);
            entries[0].QuestTag.Should().Be("test_quest");
            entries[0].State.Should().Be(1);
            entries[0].Text.Should().Be("Test entry text");
            entries[0].XPReward.Should().Be(100);
        }

        [Fact]
        public void AddEntry_WithValidData_FiresOnEntryAdded()
        {
            // Arrange
            BaseJournalEntry addedEntry = null;
            _journalSystem.OnEntryAdded += (entry) => { addedEntry = entry; };

            // Act
            _journalSystem.AddEntry("test_quest", 1, "Test entry text", 100);

            // Assert
            addedEntry.Should().NotBeNull();
            addedEntry.QuestTag.Should().Be("test_quest");
            addedEntry.Text.Should().Be("Test entry text");
        }

        [Fact]
        public void GetEntriesForQuest_WithMultipleQuests_ReturnsOnlyMatchingEntries()
        {
            // Arrange
            _journalSystem.AddEntry("quest1", 1, "Quest 1 entry 1", 0);
            _journalSystem.AddEntry("quest2", 1, "Quest 2 entry 1", 0);
            _journalSystem.AddEntry("quest1", 2, "Quest 1 entry 2", 0);

            // Act
            List<BaseJournalEntry> entries = _journalSystem.GetEntriesForQuest("quest1");

            // Assert
            entries.Count.Should().Be(2);
            entries[0].QuestTag.Should().Be("quest1");
            entries[1].QuestTag.Should().Be("quest1");
        }

        [Fact]
        public void GetEntryByState_WithMatchingEntry_ReturnsEntry()
        {
            // Arrange
            _journalSystem.AddEntry("test_quest", 1, "Entry 1", 0);
            _journalSystem.AddEntry("test_quest", 2, "Entry 2", 0);

            // Act
            BaseJournalEntry entry = _journalSystem.GetEntryByState("test_quest", 1);

            // Assert
            entry.Should().NotBeNull();
            entry.State.Should().Be(1);
            entry.Text.Should().Be("Entry 1");
        }

        [Fact]
        public void UpdateEntry_WithExistingEntry_UpdatesEntry()
        {
            // Arrange
            _journalSystem.AddEntry("test_quest", 1, "Original text", 50);

            // Act
            bool updated = _journalSystem.UpdateEntry("test_quest", 1, "Updated text", 100);

            // Assert
            updated.Should().BeTrue();
            BaseJournalEntry entry = _journalSystem.GetEntryByState("test_quest", 1);
            entry.Text.Should().Be("Updated text");
            entry.XPReward.Should().Be(100);
        }

        [Fact]
        public void UpdateEntry_WithNonExistentEntry_ReturnsFalse()
        {
            // Act
            bool updated = _journalSystem.UpdateEntry("test_quest", 1, "Text", 0);

            // Assert
            updated.Should().BeFalse();
        }

        /// <summary>
        /// Test implementation of BaseJournalSystem for testing.
        /// </summary>
        private class TestJournalSystem : BaseJournalSystem
        {
            protected override void ProcessQuestStateChange(string questTag, int newState, int oldState)
            {
                // Test implementation: Add entry with placeholder text
                BaseQuestData quest = GetQuest(questTag);
                string entryText = $"Quest {questTag} state {newState}";
                int xpReward = 0;

                if (quest != null)
                {
                    BaseQuestStage stage = quest.GetStage(newState);
                    if (stage != null)
                    {
                        entryText = stage.Text;
                        xpReward = stage.XPReward;
                    }
                }

                AddEntry(questTag, newState, entryText, xpReward);
            }

            protected override BaseJournalEntry CreateJournalEntry(string questTag, int state, string text, int xpReward)
            {
                return new BaseJournalEntry
                {
                    QuestTag = questTag,
                    State = state,
                    Text = text,
                    XPReward = xpReward,
                    DateAdded = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Test implementation of BaseQuestData for testing.
        /// </summary>
        private class TestQuestData : BaseQuestData
        {
            public override BaseQuestStage GetStage(int state)
            {
                // Test implementation: Return stage with placeholder text
                return new TestQuestStage
                {
                    State = state,
                    Text = $"Stage {state} text",
                    XPReward = state * 10
                };
            }
        }

        /// <summary>
        /// Test implementation of BaseQuestStage for testing.
        /// </summary>
        private class TestQuestStage : BaseQuestStage
        {
        }
    }
}

