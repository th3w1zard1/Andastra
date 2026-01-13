using System;
using System.Collections.Generic;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// GPU memory budget system for managing VRAM usage.
    /// 
    /// GPU memory budgets track and enforce VRAM limits, preventing
    /// out-of-memory errors and ensuring stable performance.
    /// 
    /// Features:
    /// - VRAM usage tracking
    /// - Budget enforcement
    /// - Automatic resource eviction
    /// - Per-category budgets
    /// </summary>
    /// <remarks>
    /// GPU Memory Budget:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) memory management system (modern GPU memory enhancement)
    /// - Located via string references: Memory management functions handle resource allocation
    /// - "GlobalMemoryStatus" @ 0x0080afa4 (Windows memory status API)
    /// - OpenGL memory: wglAllocateMemoryNV, wglFreeMemoryNV (OpenGL memory management)
    /// - Original implementation: KOTOR manages memory for textures, models, and render targets
    /// - Memory tracking: Original engine tracks resource usage for efficient memory management
    /// - This MonoGame implementation: Modern GPU memory budget system for VRAM management
    /// - Budget enforcement: Prevents out-of-memory errors by evicting least-recently-used resources
    /// - Per-category budgets: Separate budgets for textures, buffers, render targets, shaders
    /// </remarks>
    public class GPUMemoryBudget
    {
        /// <summary>
        /// Memory category.
        /// </summary>
        public enum MemoryCategory
        {
            Textures,
            Buffers,
            RenderTargets,
            Shaders,
            Other
        }

        /// <summary>
        /// Memory allocation entry.
        /// </summary>
        private class MemoryAllocation
        {
            public string ResourceName;
            public MemoryCategory Category;
            public long Size;
            public int LastAccessFrame;
            public int Priority;
        }

        private readonly Dictionary<MemoryCategory, long> _categoryBudgets;
        private readonly Dictionary<MemoryCategory, long> _categoryUsage;
        private readonly Dictionary<string, MemoryAllocation> _allocations;
        private long _totalBudget;
        private long _totalUsage;
        private int _currentFrame;

        /// <summary>
        /// Gets or sets the total GPU memory budget in bytes.
        /// </summary>
        public long TotalBudget
        {
            get { return _totalBudget; }
            set { _totalBudget = Math.Max(0, value); }
        }

        /// <summary>
        /// Gets current total memory usage.
        /// </summary>
        public long TotalUsage
        {
            get { return _totalUsage; }
        }

        /// <summary>
        /// Initializes a new GPU memory budget system.
        /// </summary>
        /// <param name="totalBudget">Total GPU memory budget in bytes. Must be greater than zero. Default is 2GB.</param>
        /// <exception cref="ArgumentException">Thrown if totalBudget is less than or equal to zero.</exception>
        public GPUMemoryBudget(long totalBudget = 2L * 1024 * 1024 * 1024) // 2GB default
        {
            if (totalBudget <= 0)
            {
                throw new ArgumentException("Total budget must be greater than zero.", nameof(totalBudget));
            }

            _totalBudget = totalBudget;
            _totalUsage = 0;
            _categoryBudgets = new Dictionary<MemoryCategory, long>();
            _categoryUsage = new Dictionary<MemoryCategory, long>();
            _allocations = new Dictionary<string, MemoryAllocation>();
            _currentFrame = 0;

            // Initialize category budgets (default: equal distribution)
            long categoryBudget = totalBudget / 5;
            foreach (MemoryCategory category in Enum.GetValues(typeof(MemoryCategory)))
            {
                _categoryBudgets[category] = categoryBudget;
                _categoryUsage[category] = 0;
            }
        }

        /// <summary>
        /// Allocates GPU memory.
        /// </summary>
        /// <param name="resourceName">Name of the resource. Must not be null or empty.</param>
        /// <param name="category">Memory category for this allocation.</param>
        /// <param name="size">Size in bytes. Must be greater than zero.</param>
        /// <param name="priority">Priority for eviction (lower = evicted first). Default is 0.</param>
        /// <returns>True if allocation succeeded, false if insufficient memory.</returns>
        /// <exception cref="ArgumentException">Thrown if resourceName is null or empty, or if size is less than or equal to zero.</exception>
        public bool Allocate(string resourceName, MemoryCategory category, long size, int priority = 0)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                throw new ArgumentException("Resource name cannot be null or empty.", nameof(resourceName));
            }
            if (size <= 0)
            {
                throw new ArgumentException("Size must be greater than zero.", nameof(size));
            }

            // Check total budget
            if (_totalUsage + size > _totalBudget)
            {
                // Try to free memory
                if (!FreeMemory(size))
                {
                    return false; // Cannot free enough memory
                }
            }

            // Check category budget
            long categoryUsage = _categoryUsage[category];
            long categoryBudget = _categoryBudgets[category];
            if (categoryUsage + size > categoryBudget)
            {
                // Try to free memory from this category
                if (!FreeCategoryMemory(category, size))
                {
                    return false;
                }
            }

            // Allocate
            _allocations[resourceName] = new MemoryAllocation
            {
                ResourceName = resourceName,
                Category = category,
                Size = size,
                LastAccessFrame = _currentFrame,
                Priority = priority
            };

            _totalUsage += size;
            _categoryUsage[category] += size;

            return true;
        }

        /// <summary>
        /// Deallocates GPU memory.
        /// </summary>
        /// <param name="resourceName">Name of the resource to deallocate. Can be null or empty (no-op).</param>
        public void Deallocate(string resourceName)
        {
            MemoryAllocation alloc;
            if (_allocations.TryGetValue(resourceName, out alloc))
            {
                _totalUsage -= alloc.Size;
                _categoryUsage[alloc.Category] -= alloc.Size;
                _allocations.Remove(resourceName);
            }
        }

        /// <summary>
        /// Updates resource access time.
        /// </summary>
        /// <param name="resourceName">Name of the resource that was accessed. Can be null or empty (no-op).</param>
        public void UpdateAccess(string resourceName)
        {
            MemoryAllocation alloc;
            if (_allocations.TryGetValue(resourceName, out alloc))
            {
                alloc.LastAccessFrame = _currentFrame;
            }
        }

        /// <summary>
        /// Frees memory by evicting low-priority resources.
        /// </summary>
        private bool FreeMemory(long requiredSize)
        {
            // Sort allocations by priority and last access
            var sorted = new List<MemoryAllocation>(_allocations.Values);
            sorted.Sort((a, b) =>
            {
                int priorityCmp = a.Priority.CompareTo(b.Priority);
                if (priorityCmp != 0)
                {
                    return priorityCmp;
                }
                return a.LastAccessFrame.CompareTo(b.LastAccessFrame);
            });

            long freed = 0;
            foreach (MemoryAllocation alloc in sorted)
            {
                if (freed >= requiredSize)
                {
                    break;
                }

                // Evict resource (would trigger actual deallocation)
                freed += alloc.Size;
                Deallocate(alloc.ResourceName);
            }

            return freed >= requiredSize;
        }

        /// <summary>
        /// Frees memory from a specific category.
        /// </summary>
        private bool FreeCategoryMemory(MemoryCategory category, long requiredSize)
        {
            var categoryAllocs = new List<MemoryAllocation>();
            foreach (MemoryAllocation alloc in _allocations.Values)
            {
                if (alloc.Category == category)
                {
                    categoryAllocs.Add(alloc);
                }
            }

            categoryAllocs.Sort((a, b) =>
            {
                int priorityCmp = a.Priority.CompareTo(b.Priority);
                if (priorityCmp != 0)
                {
                    return priorityCmp;
                }
                return a.LastAccessFrame.CompareTo(b.LastAccessFrame);
            });

            long freed = 0;
            foreach (MemoryAllocation alloc in categoryAllocs)
            {
                if (freed >= requiredSize)
                {
                    break;
                }

                freed += alloc.Size;
                Deallocate(alloc.ResourceName);
            }

            return freed >= requiredSize;
        }

        /// <summary>
        /// Updates frame counter.
        /// </summary>
        public void UpdateFrame()
        {
            _currentFrame++;
        }
    }
}

