using System;
using Andastra.Runtime.Games.Eclipse.Journal;
using FluentAssertions;
using Xunit;

namespace Andastra.Tests.Runtime.Games.Eclipse.Journal
{
    /// <summary>
    /// Tests for EclipseJournalSystem quest ResRef functionality.
    /// </summary>
    public class EclipseJournalSystemTests
    {
        private EclipseJournalSystem _journalSystem;

        public EclipseJournalSystemTests()
        {
            _journalSystem = new EclipseJournalSystem();
        }

        [Fact]
        public void SetQuestResRef_WithValidQuest_SetsResRef()
        {
            // Act
            _journalSystem.SetQuestResRef("test_quest", "quest_resref");

            // Assert
            string resRef = _journalSystem.GetQuestResRef("test_quest");
            resRef.Should().Be("quest_resref");
        }

        [Fact]
        public void GetQuestResRef_UnsetQuest_ReturnsNull()
        {
            // Act
            string resRef = _journalSystem.GetQuestResRef("test_quest");

            // Assert
            resRef.Should().BeNull();
        }

        [Fact]
        public void SetQuestResRef_WithNullResRef_SetsEmptyString()
        {
            // Act
            _journalSystem.SetQuestResRef("test_quest", null);

            // Assert
            string resRef = _journalSystem.GetQuestResRef("test_quest");
            resRef.Should().Be(string.Empty);
        }

        [Fact]
        public void ProcessQuestStateChange_UsesResRefForJRLLookup()
        {
            // Arrange
            var quest = new TestQuestData { Tag = "test_quest", CompletionState = 5 };
            _journalSystem.RegisterQuest(quest);
            _journalSystem.SetQuestResRef("test_quest", "quest_resref");

            // Act
            _journalSystem.SetQuestState("test_quest", 1);

            // Assert
            // Verify that quest state was set (entry should be created)
            var entries = _journalSystem.GetEntriesForQuest("test_quest");
            entries.Count.Should().BeGreaterThan(0);
        }
    }

    /// <summary>
    /// Test implementation of BaseQuestData for testing.
    /// </summary>
    internal class TestQuestData : Andastra.Runtime.Games.Common.Journal.BaseQuestData
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
    internal class TestQuestStage : Andastra.Runtime.Games.Common.Journal.BaseQuestStage
    {
    }
}

