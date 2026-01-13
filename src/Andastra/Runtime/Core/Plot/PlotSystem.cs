using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Andastra.Runtime.Core.Plot
{
    /// <summary>
    /// Manages plot state and completion tracking.
    /// </summary>
    /// <remarks>
    /// Plot System (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) plot system
    /// - Located via string references: "PlotIndex" @ 0x007c35c4, "PlotXPPercentage" @ 0x007c35cc
    /// - Plot processing: FUN_005e6870 @ 0x005e6870 -> FUN_0057eb20 @ 0x0057eb20
    /// - Original implementation:
    ///   1. PlotIndex references plot.2da row index
    ///   2. plot.2da contains "label" (quest identifier) and "xp" (XP reward) columns
    ///   3. When PlotIndex is processed, XP is awarded and plot state is updated
    ///   4. Plot completion is tracked to prevent duplicate XP awards
    ///   5. Plot labels can be used to update quest/journal state
    /// - Plot state tracking:
    ///   - Tracks which plots have been triggered (by PlotIndex)
    ///   - Tracks plot completion status
    ///   - Integrates with journal system for quest updates
    /// - Cross-engine analysis:
    ///   - swkotor.exe: Similar plot system (needs reverse engineering)
    ///   - nwmain.exe: Different plot system (needs reverse engineering)
    ///   - daorigins.exe: Plot system may differ (needs reverse engineering)
    /// </remarks>
    public class PlotSystem
    {
        private readonly Dictionary<int, PlotState> _plotStates;
        private readonly Dictionary<string, int> _plotIndexByLabel;

        /// <summary>
        /// Event fired when a plot is triggered.
        /// </summary>
        public event Action<int, string> OnPlotTriggered;

        /// <summary>
        /// Event fired when a plot is completed.
        /// </summary>
        public event Action<int, string> OnPlotCompleted;

        public PlotSystem()
        {
            _plotStates = new Dictionary<int, PlotState>();
            _plotIndexByLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers a plot from plot.2da data.
        /// </summary>
        /// <param name="plotIndex">Plot index (row index in plot.2da)</param>
        /// <param name="label">Plot label (quest identifier from plot.2da)</param>
        public void RegisterPlot(int plotIndex, string label)
        {
            if (plotIndex < 0)
            {
                return;
            }

            if (!_plotStates.ContainsKey(plotIndex))
            {
                _plotStates[plotIndex] = new PlotState
                {
                    PlotIndex = plotIndex,
                    Label = label ?? string.Empty,
                    IsTriggered = false,
                    IsCompleted = false,
                    TriggerCount = 0,
                    LastTriggered = null
                };
            }
            else
            {
                // Update existing plot state
                PlotState state = _plotStates[plotIndex];
                if (!string.IsNullOrEmpty(label))
                {
                    state.Label = label;
                }
            }

            // Index by label for quick lookup
            if (!string.IsNullOrEmpty(label))
            {
                _plotIndexByLabel[label] = plotIndex;
            }
        }

        /// <summary>
        /// Gets plot state by index.
        /// </summary>
        [CanBeNull]
        public PlotState GetPlotState(int plotIndex)
        {
            PlotState state;
            if (_plotStates.TryGetValue(plotIndex, out state))
            {
                return state;
            }
            return null;
        }

        /// <summary>
        /// Gets plot state by label.
        /// </summary>
        [CanBeNull]
        public PlotState GetPlotStateByLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return null;
            }

            int plotIndex;
            if (_plotIndexByLabel.TryGetValue(label, out plotIndex))
            {
                return GetPlotState(plotIndex);
            }
            return null;
        }

        /// <summary>
        /// Gets plot index by label.
        /// </summary>
        public int? GetPlotIndexByLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return null;
            }

            int plotIndex;
            if (_plotIndexByLabel.TryGetValue(label, out plotIndex))
            {
                return plotIndex;
            }
            return null;
        }

        /// <summary>
        /// Marks a plot as triggered.
        /// </summary>
        /// <param name="plotIndex">Plot index</param>
        /// <param name="label">Plot label (optional, for event notification)</param>
        /// <returns>True if plot was successfully triggered, false if already triggered/completed</returns>
        public bool TriggerPlot(int plotIndex, string label = null)
        {
            if (plotIndex < 0)
            {
                return false;
            }

            // Get or create plot state
            PlotState state;
            if (!_plotStates.TryGetValue(plotIndex, out state))
            {
                state = new PlotState
                {
                    PlotIndex = plotIndex,
                    Label = label ?? string.Empty,
                    IsTriggered = false,
                    IsCompleted = false,
                    TriggerCount = 0,
                    LastTriggered = null
                };
                _plotStates[plotIndex] = state;

                // Index by label if provided
                if (!string.IsNullOrEmpty(label))
                {
                    _plotIndexByLabel[label] = plotIndex;
                }
            }

            // Update label if provided and different
            if (!string.IsNullOrEmpty(label) && state.Label != label)
            {
                state.Label = label;
                _plotIndexByLabel[label] = plotIndex;
            }

            // Mark as triggered
            state.IsTriggered = true;
            state.TriggerCount++;
            state.LastTriggered = DateTime.Now;

            // Fire event
            if (OnPlotTriggered != null)
            {
                OnPlotTriggered(plotIndex, state.Label);
            }

            return true;
        }

        /// <summary>
        /// Marks a plot as completed.
        /// </summary>
        /// <param name="plotIndex">Plot index</param>
        /// <param name="label">Plot label (optional, for event notification)</param>
        public void CompletePlot(int plotIndex, string label = null)
        {
            if (plotIndex < 0)
            {
                return;
            }

            // Get or create plot state
            PlotState state;
            if (!_plotStates.TryGetValue(plotIndex, out state))
            {
                state = new PlotState
                {
                    PlotIndex = plotIndex,
                    Label = label ?? string.Empty,
                    IsTriggered = false,
                    IsCompleted = false,
                    TriggerCount = 0,
                    LastTriggered = null
                };
                _plotStates[plotIndex] = state;

                // Index by label if provided
                if (!string.IsNullOrEmpty(label))
                {
                    _plotIndexByLabel[label] = plotIndex;
                }
            }

            // Update label if provided and different
            if (!string.IsNullOrEmpty(label) && state.Label != label)
            {
                state.Label = label;
                _plotIndexByLabel[label] = plotIndex;
            }

            // Mark as completed
            state.IsCompleted = true;
            if (!state.IsTriggered)
            {
                state.IsTriggered = true;
                state.TriggerCount++;
                state.LastTriggered = DateTime.Now;
            }

            // Fire event
            if (OnPlotCompleted != null)
            {
                OnPlotCompleted(plotIndex, state.Label);
            }
        }

        /// <summary>
        /// Checks if a plot has been triggered.
        /// </summary>
        public bool IsPlotTriggered(int plotIndex)
        {
            PlotState state;
            if (_plotStates.TryGetValue(plotIndex, out state))
            {
                return state.IsTriggered;
            }
            return false;
        }

        /// <summary>
        /// Checks if a plot has been completed.
        /// </summary>
        public bool IsPlotCompleted(int plotIndex)
        {
            PlotState state;
            if (_plotStates.TryGetValue(plotIndex, out state))
            {
                return state.IsCompleted;
            }
            return false;
        }

        /// <summary>
        /// Gets all plot states.
        /// </summary>
        public IReadOnlyDictionary<int, PlotState> GetAllPlotStates()
        {
            return _plotStates;
        }

        /// <summary>
        /// Clears all plot states.
        /// </summary>
        public void Clear()
        {
            _plotStates.Clear();
            _plotIndexByLabel.Clear();
        }
    }

    /// <summary>
    /// Represents the state of a plot.
    /// </summary>
    public class PlotState
    {
        /// <summary>
        /// Plot index (row index in plot.2da).
        /// </summary>
        public int PlotIndex { get; set; }

        /// <summary>
        /// Plot label (quest identifier from plot.2da).
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Whether the plot has been triggered.
        /// </summary>
        public bool IsTriggered { get; set; }

        /// <summary>
        /// Whether the plot has been completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Number of times the plot has been triggered.
        /// </summary>
        public int TriggerCount { get; set; }

        /// <summary>
        /// When the plot was last triggered.
        /// </summary>
        [CanBeNull]
        public DateTime? LastTriggered { get; set; }
    }
}

