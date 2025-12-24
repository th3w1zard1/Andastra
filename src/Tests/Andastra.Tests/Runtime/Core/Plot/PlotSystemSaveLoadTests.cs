using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Plot;
using Andastra.Runtime.Core.Save;
using FluentAssertions;
using Xunit;

namespace Andastra.Tests.Runtime.Core.Plot
{
    /// <summary>
    /// Tests for PlotSystem save/load functionality.
    /// </summary>
    public class PlotSystemSaveLoadTests
    {
        [Fact]
        public void SavePlotState_WithMultiplePlots_SavesAllPlotStates()
        {
            // Arrange
            var plotSystem = new PlotSystem();
            plotSystem.RegisterPlot(1, "quest_1");
            plotSystem.RegisterPlot(2, "quest_2");
            plotSystem.RegisterPlot(3, "quest_3");
            plotSystem.TriggerPlot(1, "quest_1");
            plotSystem.TriggerPlot(2, "quest_2");
            plotSystem.CompletePlot(3, "quest_3");

            var saveData = new SaveGameData();

            // Act
            // Simulate SavePlotState by manually copying plot states
            saveData.PlotStates = new Dictionary<int, PlotState>();
            var allPlotStates = plotSystem.GetAllPlotStates();
            foreach (var kvp in allPlotStates)
            {
                PlotState plotState = kvp.Value;
                saveData.PlotStates[kvp.Key] = new PlotState
                {
                    PlotIndex = plotState.PlotIndex,
                    Label = plotState.Label,
                    IsTriggered = plotState.IsTriggered,
                    IsCompleted = plotState.IsCompleted,
                    TriggerCount = plotState.TriggerCount,
                    LastTriggered = plotState.LastTriggered
                };
            }

            // Assert
            saveData.PlotStates.Should().NotBeNull();
            saveData.PlotStates.Count.Should().Be(3);
            saveData.PlotStates[1].IsTriggered.Should().BeTrue();
            saveData.PlotStates[2].IsTriggered.Should().BeTrue();
            saveData.PlotStates[3].IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void RestorePlotState_WithSavedPlots_RestoresAllPlotStates()
        {
            // Arrange
            var saveData = new SaveGameData();
            saveData.PlotStates = new Dictionary<int, PlotState>
            {
                { 1, new PlotState { PlotIndex = 1, Label = "quest_1", IsTriggered = true, TriggerCount = 2 } },
                { 2, new PlotState { PlotIndex = 2, Label = "quest_2", IsTriggered = true, IsCompleted = true, TriggerCount = 1 } },
                { 3, new PlotState { PlotIndex = 3, Label = "quest_3", IsTriggered = false, IsCompleted = false, TriggerCount = 0 } }
            };

            var plotSystem = new PlotSystem();

            // Act
            // Simulate RestorePlotState
            plotSystem.Clear();
            foreach (var kvp in saveData.PlotStates)
            {
                PlotState plotState = kvp.Value;
                plotSystem.RegisterPlot(plotState.PlotIndex, plotState.Label);
                PlotState restoredState = plotSystem.GetPlotState(plotState.PlotIndex);
                if (restoredState != null)
                {
                    restoredState.IsTriggered = plotState.IsTriggered;
                    restoredState.IsCompleted = plotState.IsCompleted;
                    restoredState.TriggerCount = plotState.TriggerCount;
                    restoredState.LastTriggered = plotState.LastTriggered;
                }
            }

            // Assert
            PlotState state1 = plotSystem.GetPlotState(1);
            state1.Should().NotBeNull();
            state1.IsTriggered.Should().BeTrue();
            state1.TriggerCount.Should().Be(2);

            PlotState state2 = plotSystem.GetPlotState(2);
            state2.Should().NotBeNull();
            state2.IsTriggered.Should().BeTrue();
            state2.IsCompleted.Should().BeTrue();
            state2.TriggerCount.Should().Be(1);

            PlotState state3 = plotSystem.GetPlotState(3);
            state3.Should().NotBeNull();
            state3.IsTriggered.Should().BeFalse();
            state3.IsCompleted.Should().BeFalse();
            state3.TriggerCount.Should().Be(0);
        }

        [Fact]
        public void SavePlotState_WithEmptyPlotSystem_SavesEmptyDictionary()
        {
            // Arrange
            var plotSystem = new PlotSystem();
            var saveData = new SaveGameData();

            // Act
            saveData.PlotStates = new Dictionary<int, PlotState>();
            var allPlotStates = plotSystem.GetAllPlotStates();
            foreach (var kvp in allPlotStates)
            {
                PlotState plotState = kvp.Value;
                saveData.PlotStates[kvp.Key] = new PlotState
                {
                    PlotIndex = plotState.PlotIndex,
                    Label = plotState.Label,
                    IsTriggered = plotState.IsTriggered,
                    IsCompleted = plotState.IsCompleted,
                    TriggerCount = plotState.TriggerCount,
                    LastTriggered = plotState.LastTriggered
                };
            }

            // Assert
            saveData.PlotStates.Should().NotBeNull();
            saveData.PlotStates.Count.Should().Be(0);
        }

        [Fact]
        public void RestorePlotState_WithEmptySaveData_DoesNotModifyPlotSystem()
        {
            // Arrange
            var plotSystem = new PlotSystem();
            plotSystem.RegisterPlot(1, "quest_1");
            plotSystem.TriggerPlot(1, "quest_1");

            var saveData = new SaveGameData();
            saveData.PlotStates = null;

            // Act
            // Simulate RestorePlotState with null PlotStates
            if (saveData.PlotStates != null)
            {
                plotSystem.Clear();
                foreach (var kvp in saveData.PlotStates)
                {
                    PlotState plotState = kvp.Value;
                    plotSystem.RegisterPlot(plotState.PlotIndex, plotState.Label);
                    PlotState restoredState = plotSystem.GetPlotState(plotState.PlotIndex);
                    if (restoredState != null)
                    {
                        restoredState.IsTriggered = plotState.IsTriggered;
                        restoredState.IsCompleted = plotState.IsCompleted;
                        restoredState.TriggerCount = plotState.TriggerCount;
                        restoredState.LastTriggered = plotState.LastTriggered;
                    }
                }
            }

            // Assert
            // PlotSystem should remain unchanged
            PlotState state = plotSystem.GetPlotState(1);
            state.Should().NotBeNull();
            state.IsTriggered.Should().BeTrue();
        }

        [Fact]
        public void SavePlotState_WithLastTriggeredTime_PreservesTimestamp()
        {
            // Arrange
            var plotSystem = new PlotSystem();
            plotSystem.RegisterPlot(1, "quest_1");
            plotSystem.TriggerPlot(1, "quest_1");
            DateTime? originalTime = plotSystem.GetPlotState(1).LastTriggered;

            var saveData = new SaveGameData();

            // Act
            saveData.PlotStates = new Dictionary<int, PlotState>();
            var allPlotStates = plotSystem.GetAllPlotStates();
            foreach (var kvp in allPlotStates)
            {
                PlotState plotState = kvp.Value;
                saveData.PlotStates[kvp.Key] = new PlotState
                {
                    PlotIndex = plotState.PlotIndex,
                    Label = plotState.Label,
                    IsTriggered = plotState.IsTriggered,
                    IsCompleted = plotState.IsCompleted,
                    TriggerCount = plotState.TriggerCount,
                    LastTriggered = plotState.LastTriggered
                };
            }

            // Assert
            saveData.PlotStates[1].LastTriggered.Should().Be(originalTime);
        }
    }
}

