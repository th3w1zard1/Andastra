using System;
using Andastra.Runtime.Games.Aurora.Journal;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andastra.Tests.Runtime.Games.Aurora.Journal
{
    /// <summary>
    /// Tests for AuroraJournalSystem per-creature journal functionality.
    /// </summary>
    public class AuroraJournalSystemTests
    {
        private AuroraJournalSystem _journalSystem;

        public AuroraJournalSystemTests()
        {
            _journalSystem = new AuroraJournalSystem();
        }

        [Fact]
        public void GetQuestStateForCreature_UnregisteredQuest_ReturnsZero()
        {
            // Act
            int state = _journalSystem.GetQuestStateForCreature(1, "test_quest");

            // Assert
            state.Should().Be(0);
        }

        [Fact]
        public void SetQuestStateForCreature_WithValidQuest_UpdatesState()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);

            // Act
            _journalSystem.SetQuestStateForCreature(1, "test_quest", 3);

            // Assert
            int state = _journalSystem.GetQuestStateForCreature(1, "test_quest");
            state.Should().Be(3);
        }

        [Fact]
        public void SetQuestStateForCreature_DifferentCreatures_HaveSeparateStates()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);

            // Act
            _journalSystem.SetQuestStateForCreature(1, "test_quest", 2);
            _journalSystem.SetQuestStateForCreature(2, "test_quest", 4);

            // Assert
            _journalSystem.GetQuestStateForCreature(1, "test_quest").Should().Be(2);
            _journalSystem.GetQuestStateForCreature(2, "test_quest").Should().Be(4);
        }

        [Fact]
        public void ReloadJournalEntries_ClearsCreatureStates()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            _journalSystem.SetQuestStateForCreature(1, "test_quest", 3);

            // Act
            _journalSystem.ReloadJournalEntries(1);

            // Assert
            int state = _journalSystem.GetQuestStateForCreature(1, "test_quest");
            state.Should().Be(0);
        }

        /// <summary>
        /// Test implementation of BaseQuestData for testing.
        /// </summary>
        private class TestQuestData : Andastra.Runtime.Games.Common.Journal.BaseQuestData
        {
            public override Andastra.Runtime.Games.Common.Journal.BaseQuestStage GetStage(int state)
            {
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
        private class TestQuestStage : Andastra.Runtime.Games.Common.Journal.BaseQuestStage
        {
        }
    }
}

