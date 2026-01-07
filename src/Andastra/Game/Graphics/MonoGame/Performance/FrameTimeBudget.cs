using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Performance
{
    /// <summary>
    /// Frame time budgeting system for maintaining consistent frame rates.
    /// 
    /// Allocates time budgets to different systems (rendering, physics, scripts, etc.)
    /// and tracks actual usage to ensure frame time targets are met.
    /// 
    /// Features:
    /// - Per-system time budgets
    /// - Frame time tracking
    /// - Budget enforcement
    /// - Quality scaling triggers
    /// </summary>
    public class FrameTimeBudget
    {
        /// <summary>
        /// System budget category.
        /// </summary>
        public enum BudgetCategory
        {
            Rendering,
            Scripting,
            Physics,
            Audio,
            Networking,
            AI,
            Other
        }

        /// <summary>
        /// Budget allocation.
        /// </summary>
        private class BudgetAllocation
        {
            public BudgetCategory Category;
            public double AllocatedMs;
            public double ActualMs;
            public double MaxMs;
        }

        private readonly Dictionary<BudgetCategory, BudgetAllocation> _budgets;
        private double _targetFrameTimeMs;
        private double _actualFrameTimeMs;

        /// <summary>
        /// Gets or sets the target frame time in milliseconds.
        /// </summary>
        public double TargetFrameTimeMs
        {
            get { return _targetFrameTimeMs; }
            set { _targetFrameTimeMs = Math.Max(1.0, value); }
        }

        /// <summary>
        /// Gets the actual frame time in milliseconds.
        /// </summary>
        public double ActualFrameTimeMs
        {
            get { return _actualFrameTimeMs; }
        }

        /// <summary>
        /// Initializes a new frame time budget system.
        /// </summary>
        public FrameTimeBudget(double targetFrameTimeMs = 16.67) // 60 FPS default
        {
            _targetFrameTimeMs = targetFrameTimeMs;
            _actualFrameTimeMs = targetFrameTimeMs;
            _budgets = new Dictionary<BudgetCategory, BudgetAllocation>();

            // Initialize default allocations (percentages of frame time)
            InitializeDefaultBudgets();
        }

        private void InitializeDefaultBudgets()
        {
            _budgets[BudgetCategory.Rendering] = new BudgetAllocation
            {
                Category = BudgetCategory.Rendering,
                AllocatedMs = _targetFrameTimeMs * 0.7, // 70% for rendering
                MaxMs = _targetFrameTimeMs * 0.85
            };

            _budgets[BudgetCategory.Scripting] = new BudgetAllocation
            {
                Category = BudgetCategory.Scripting,
                AllocatedMs = _targetFrameTimeMs * 0.1, // 10% for scripts
                MaxMs = _targetFrameTimeMs * 0.15
            };

            _budgets[BudgetCategory.Physics] = new BudgetAllocation
            {
                Category = BudgetCategory.Physics,
                AllocatedMs = _targetFrameTimeMs * 0.05, // 5% for physics
                MaxMs = _targetFrameTimeMs * 0.1
            };

            _budgets[BudgetCategory.Audio] = new BudgetAllocation
            {
                Category = BudgetCategory.Audio,
                AllocatedMs = _targetFrameTimeMs * 0.05, // 5% for audio
                MaxMs = _targetFrameTimeMs * 0.1
            };

            _budgets[BudgetCategory.AI] = new BudgetAllocation
            {
                Category = BudgetCategory.AI,
                AllocatedMs = _targetFrameTimeMs * 0.05, // 5% for AI
                MaxMs = _targetFrameTimeMs * 0.1
            };

            _budgets[BudgetCategory.Other] = new BudgetAllocation
            {
                Category = BudgetCategory.Other,
                AllocatedMs = _targetFrameTimeMs * 0.05, // 5% for other
                MaxMs = _targetFrameTimeMs * 0.1
            };
        }

        /// <summary>
        /// Records actual time spent in a category.
        /// </summary>
        /// <param name="category">Budget category.</param>
        /// <param name="timeMs">Time spent in milliseconds. Must be non-negative.</param>
        public void RecordTime(BudgetCategory category, double timeMs)
        {
            if (timeMs < 0.0)
            {
                timeMs = 0.0; // Clamp negative values
            }

            BudgetAllocation budget;
            if (_budgets.TryGetValue(category, out budget))
            {
                budget.ActualMs = timeMs;
            }
        }

        /// <summary>
        /// Updates total frame time.
        /// </summary>
        /// <param name="frameTimeMs">Frame time in milliseconds. Must be non-negative.</param>
        public void UpdateFrameTime(double frameTimeMs)
        {
            if (frameTimeMs < 0.0)
            {
                frameTimeMs = 0.0; // Clamp negative values
            }
            _actualFrameTimeMs = frameTimeMs;
        }

        /// <summary>
        /// Checks if a category exceeded its budget.
        /// </summary>
        public bool IsOverBudget(BudgetCategory category)
        {
            BudgetAllocation budget;
            if (_budgets.TryGetValue(category, out budget))
            {
                return budget.ActualMs > budget.AllocatedMs;
            }
            return false;
        }

        /// <summary>
        /// Gets the allocated budget for a category.
        /// </summary>
        public double GetAllocatedBudget(BudgetCategory category)
        {
            BudgetAllocation budget;
            if (_budgets.TryGetValue(category, out budget))
            {
                return budget.AllocatedMs;
            }
            return 0.0;
        }

        /// <summary>
        /// Gets the actual time spent in a category.
        /// </summary>
        public double GetActualTime(BudgetCategory category)
        {
            BudgetAllocation budget;
            if (_budgets.TryGetValue(category, out budget))
            {
                return budget.ActualMs;
            }
            return 0.0;
        }

        /// <summary>
        /// Checks if overall frame time is within target.
        /// </summary>
        public bool IsFrameTimeWithinTarget()
        {
            return _actualFrameTimeMs <= _targetFrameTimeMs * 1.1; // 10% tolerance
        }
    }
}

