using System;
using System.Linq;
using Andastra.Runtime.Core.Plot;
using FluentAssertions;
using Xunit;

namespace Andastra.Tests.Runtime.Core.Plot
{
    /// <summary>
    /// Tests for PlotSystem plot state management.
    /// </summary>
    public class PlotSystemTests
    {
        private PlotSystem _plotSystem;

        public PlotSystemTests()
        {
            _plotSystem = new PlotSystem();
        }

        [Fact]
        public void RegisterPlot_WithValidIndex_RegistersPlot()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";

            // Act
            _plotSystem.RegisterPlot(plotIndex, label);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().NotBeNull();
            state.PlotIndex.Should().Be(plotIndex);
            state.Label.Should().Be(label);
            state.IsTriggered.Should().BeFalse();
            state.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void RegisterPlot_WithNegativeIndex_DoesNotRegister()
        {
            // Arrange
            int plotIndex = -1;
            string label = "test_quest";

            // Act
            _plotSystem.RegisterPlot(plotIndex, label);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().BeNull();
        }

        [Fact]
        public void RegisterPlot_UpdatesExistingPlot()
        {
            // Arrange
            int plotIndex = 1;
            string label1 = "test_quest_1";
            string label2 = "test_quest_2";

            // Act
            _plotSystem.RegisterPlot(plotIndex, label1);
            _plotSystem.RegisterPlot(plotIndex, label2);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().NotBeNull();
            state.Label.Should().Be(label2);
        }

        [Fact]
        public void TriggerPlot_WithValidIndex_TriggersPlot()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            bool result = _plotSystem.TriggerPlot(plotIndex, label);

            // Assert
            result.Should().BeTrue();
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().NotBeNull();
            state.IsTriggered.Should().BeTrue();
            state.TriggerCount.Should().Be(1);
            state.LastTriggered.Should().NotBeNull();
        }

        [Fact]
        public void TriggerPlot_WithNegativeIndex_ReturnsFalse()
        {
            // Arrange
            int plotIndex = -1;
            string label = "test_quest";

            // Act
            bool result = _plotSystem.TriggerPlot(plotIndex, label);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void TriggerPlot_WithoutRegistration_CreatesPlotState()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";

            // Act
            bool result = _plotSystem.TriggerPlot(plotIndex, label);

            // Assert
            result.Should().BeTrue();
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().NotBeNull();
            state.IsTriggered.Should().BeTrue();
            state.Label.Should().Be(label);
        }

        [Fact]
        public void TriggerPlot_IncrementsTriggerCount()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            _plotSystem.TriggerPlot(plotIndex, label);
            _plotSystem.TriggerPlot(plotIndex, label);
            _plotSystem.TriggerPlot(plotIndex, label);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().NotBeNull();
            state.TriggerCount.Should().Be(3);
        }

        [Fact]
        public void CompletePlot_WithValidIndex_CompletesPlot()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            _plotSystem.CompletePlot(plotIndex, label);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);

            state.Should().NotBeNull();
            state.IsCompleted.Should().BeTrue();
            state.IsTriggered.Should().BeTrue();
        }

        [Fact]
        public void CompletePlot_WithoutRegistration_CreatesPlotState()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";

            // Act
            _plotSystem.CompletePlot(plotIndex, label);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().NotBeNull();
            state.IsCompleted.Should().BeTrue();
            state.IsTriggered.Should().BeTrue();
        }

        [Fact]
        public void CompletePlot_WithNegativeIndex_DoesNothing()
        {
            // Arrange
            int plotIndex = -1;
            string label = "test_quest";

            // Act
            _plotSystem.CompletePlot(plotIndex, label);

            // Assert
            PlotState state = _plotSystem.GetPlotState(plotIndex);
            state.Should().BeNull();
        }

        [Fact]
        public void IsPlotTriggered_WithTriggeredPlot_ReturnsTrue()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);
            _plotSystem.TriggerPlot(plotIndex, label);

            // Act
            bool result = _plotSystem.IsPlotTriggered(plotIndex);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsPlotTriggered_WithUntriggeredPlot_ReturnsFalse()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            bool result = _plotSystem.IsPlotTriggered(plotIndex);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsPlotCompleted_WithCompletedPlot_ReturnsTrue()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);
            _plotSystem.CompletePlot(plotIndex, label);

            // Act
            bool result = _plotSystem.IsPlotCompleted(plotIndex);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsPlotCompleted_WithUncompletedPlot_ReturnsFalse()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            bool result = _plotSystem.IsPlotCompleted(plotIndex);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetPlotStateByLabel_WithRegisteredPlot_ReturnsState()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            PlotState state = _plotSystem.GetPlotStateByLabel(label);

            // Assert
            state.Should().NotBeNull();
            state.PlotIndex.Should().Be(plotIndex);
            state.Label.Should().Be(label);
        }

        [Fact]
        public void GetPlotStateByLabel_WithUnregisteredLabel_ReturnsNull()
        {
            // Arrange
            string label = "nonexistent_quest";

            // Act
            PlotState state = _plotSystem.GetPlotStateByLabel(label);

            // Assert
            state.Should().BeNull();
        }

        [Fact]
        public void GetPlotIndexByLabel_WithRegisteredPlot_ReturnsIndex()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);

            // Act
            int? result = _plotSystem.GetPlotIndexByLabel(label);

            // Assert
            result.HasValue.Should().BeTrue();
            result.Value.Should().Be(plotIndex);
        }

        [Fact]
        public void GetPlotIndexByLabel_WithUnregisteredLabel_ReturnsNull()
        {
            // Arrange
            string label = "nonexistent_quest";

            // Act
            int? result = _plotSystem.GetPlotIndexByLabel(label);

            // Assert
            result.HasValue.Should().BeFalse();
        }

        [Fact]
        public void TriggerPlot_FiresOnPlotTriggeredEvent()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);
            int triggeredPlotIndex = -1;
            string triggeredLabel = null;
            _plotSystem.OnPlotTriggered += (index, lbl) =>
            {
                triggeredPlotIndex = index;
                triggeredLabel = lbl;
            };

            // Act
            _plotSystem.TriggerPlot(plotIndex, label);

            // Assert
            triggeredPlotIndex.Should().Be(plotIndex);
            triggeredLabel.Should().Be(label);
        }

        [Fact]
        public void CompletePlot_FiresOnPlotCompletedEvent()
        {
            // Arrange
            int plotIndex = 1;
            string label = "test_quest";
            _plotSystem.RegisterPlot(plotIndex, label);
            int completedPlotIndex = -1;
            string completedLabel = null;
            _plotSystem.OnPlotCompleted += (index, lbl) =>
            {
                completedPlotIndex = index;
                completedLabel = lbl;
            };

            // Act
            _plotSystem.CompletePlot(plotIndex, label);

            // Assert
            completedPlotIndex.Should().Be(plotIndex);
            completedLabel.Should().Be(label);
        }

        [Fact]
        public void GetAllPlotStates_ReturnsAllRegisteredPlots()
        {
            // Arrange
            _plotSystem.RegisterPlot(1, "quest_1");
            _plotSystem.RegisterPlot(2, "quest_2");
            _plotSystem.RegisterPlot(3, "quest_3");

            // Act
            var allStates = _plotSystem.GetAllPlotStates();

            // Assert
            allStates.Count.Should().Be(3);
            allStates.Should().ContainKey(1);
            allStates.Should().ContainKey(2);
            allStates.Should().ContainKey(3);
        }

        [Fact]
        public void Clear_RemovesAllPlotStates()
        {
            // Arrange
            _plotSystem.RegisterPlot(1, "quest_1");
            _plotSystem.RegisterPlot(2, "quest_2");
            _plotSystem.TriggerPlot(1, "quest_1");

            // Act
            _plotSystem.Clear();

            // Assert
            var allStates = _plotSystem.GetAllPlotStates();
            allStates.Count.Should().Be(0);
            _plotSystem.GetPlotState(1).Should().BeNull();
            _plotSystem.GetPlotState(2).Should().BeNull();
        }
    }
}

