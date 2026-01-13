using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Andastra.Game.Graphics.MonoGame.Memory
{
    /// <summary>
    /// Memory tracking system for detailed memory usage monitoring.
    /// 
    /// Tracks memory usage across different categories, enabling
    /// memory leak detection and optimization.
    /// 
    /// Features:
    /// - Category-based tracking
    /// - Peak memory tracking
    /// - Memory leak detection
    /// - Detailed statistics
    /// </summary>
    public class MemoryTracker
    {
        /// <summary>
        /// Memory category.
        /// </summary>
        public enum Category
        {
            Textures,
            Meshes,
            Shaders,
            RenderTargets,
            Buffers,
            Audio,
            Scripts,
            Other
        }

        /// <summary>
        /// Memory usage statistics.
        /// </summary>
        public struct MemoryStats
        {
            public long CurrentBytes;
            public long PeakBytes;
            public int AllocationCount;
            public int DeallocationCount;
        }

        private readonly Dictionary<Category, MemoryStats> _categoryStats;
        private readonly Dictionary<string, long> _namedAllocations;
        private long _totalMemory;
        private long _peakMemory;

        /// <summary>
        /// Gets total memory usage.
        /// </summary>
        public long TotalMemory
        {
            get { return _totalMemory; }
        }

        /// <summary>
        /// Gets peak memory usage.
        /// </summary>
        public long PeakMemory
        {
            get { return _peakMemory; }
        }

        /// <summary>
        /// Initializes a new memory tracker.
        /// </summary>
        public MemoryTracker()
        {
            _categoryStats = new Dictionary<Category, MemoryStats>();
            _namedAllocations = new Dictionary<string, long>();
            foreach (Category category in Enum.GetValues(typeof(Category)))
            {
                _categoryStats[category] = new MemoryStats();
            }
        }

        /// <summary>
        /// Records a memory allocation.
        /// </summary>
        public void Allocate(Category category, long bytes, string name = null)
        {
            MemoryStats stats = _categoryStats[category];
            stats.CurrentBytes += bytes;
            stats.AllocationCount++;
            stats.PeakBytes = Math.Max(stats.PeakBytes, stats.CurrentBytes);
            _categoryStats[category] = stats;

            _totalMemory += bytes;
            _peakMemory = Math.Max(_peakMemory, _totalMemory);

            if (!string.IsNullOrEmpty(name))
            {
                _namedAllocations[name] = bytes;
            }
        }

        /// <summary>
        /// Records a memory deallocation.
        /// </summary>
        public void Deallocate(Category category, long bytes, string name = null)
        {
            MemoryStats stats = _categoryStats[category];
            stats.CurrentBytes -= bytes;
            stats.DeallocationCount++;
            _categoryStats[category] = stats;

            _totalMemory -= bytes;

            if (!string.IsNullOrEmpty(name))
            {
                _namedAllocations.Remove(name);
            }
        }

        /// <summary>
        /// Gets memory statistics for a category.
        /// </summary>
        public MemoryStats GetStats(Category category)
        {
            MemoryStats stats;
            if (_categoryStats.TryGetValue(category, out stats))
            {
                return stats;
            }
            return new MemoryStats();
        }

        /// <summary>
        /// Gets all category statistics.
        /// </summary>
        public Dictionary<Category, MemoryStats> GetAllStats()
        {
            return new Dictionary<Category, MemoryStats>(_categoryStats);
        }

        /// <summary>
        /// Resets peak memory tracking.
        /// </summary>
        public void ResetPeaks()
        {
            foreach (Category category in Enum.GetValues(typeof(Category)))
            {
                MemoryStats stats = _categoryStats[category];
                stats.PeakBytes = stats.CurrentBytes;
                _categoryStats[category] = stats;
            }
            _peakMemory = _totalMemory;
        }
    }
}

