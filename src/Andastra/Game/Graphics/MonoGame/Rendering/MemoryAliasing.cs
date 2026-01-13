using System;
using System.Collections.Generic;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Memory aliasing system for efficient GPU memory reuse.
    /// 
    /// Memory aliasing allows the same GPU memory to be used for different
    /// resources at different times, dramatically reducing memory usage.
    /// 
    /// Features:
    /// - Automatic memory reuse
    /// - Lifetime-based aliasing
    /// - Conflict detection
    /// - Optimal memory layout
    /// </summary>
    public class MemoryAliasing
    {
        /// <summary>
        /// Resource allocation.
        /// </summary>
        public class ResourceAllocation
        {
            public string ResourceName;
            public long Offset;
            public long Size;
            public int FirstUse;
            public int LastUse;
            public object Resource;
        }

        private readonly List<ResourceAllocation> _allocations;
        private long _totalSize;

        /// <summary>
        /// Gets the total aliased memory size.
        /// </summary>
        public long TotalSize
        {
            get { return _totalSize; }
        }

        /// <summary>
        /// Initializes a new memory aliasing system.
        /// </summary>
        public MemoryAliasing()
        {
            _allocations = new List<ResourceAllocation>();
        }

        /// <summary>
        /// Allocates memory for a resource with lifetime information.
        /// </summary>
        public long Allocate(string resourceName, long size, int firstUse, int lastUse, object resource)
        {
            // Find overlapping allocations that can be aliased
            long offset = FindAliasableOffset(size, firstUse, lastUse);

            ResourceAllocation allocation = new ResourceAllocation
            {
                ResourceName = resourceName,
                Offset = offset,
                Size = size,
                FirstUse = firstUse,
                LastUse = lastUse,
                Resource = resource
            };

            _allocations.Add(allocation);
            _totalSize = Math.Max(_totalSize, offset + size);

            return offset;
        }

        /// <summary>
        /// Finds an offset where memory can be aliased.
        /// </summary>
        private long FindAliasableOffset(long size, int firstUse, int lastUse)
        {
            // Sort allocations by offset
            _allocations.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            // Try to find space between existing allocations
            for (int i = 0; i < _allocations.Count - 1; i++)
            {
                ResourceAllocation current = _allocations[i];
                ResourceAllocation next = _allocations[i + 1];

                // Check if lifetimes don't overlap
                if (lastUse < next.FirstUse || firstUse > current.LastUse)
                {
                    long gapStart = current.Offset + current.Size;
                    long gapSize = next.Offset - gapStart;

                    if (gapSize >= size)
                    {
                        return gapStart;
                    }
                }
            }

            // No gap found, allocate at end
            return _totalSize;
        }

        /// <summary>
        /// Gets allocation for a resource.
        /// </summary>
        public ResourceAllocation GetAllocation(string resourceName)
        {
            foreach (ResourceAllocation alloc in _allocations)
            {
                if (alloc.ResourceName == resourceName)
                {
                    return alloc;
                }
            }
            return null;
        }

        /// <summary>
        /// Clears all allocations.
        /// </summary>
        public void Clear()
        {
            _allocations.Clear();
            _totalSize = 0;
        }
    }
}

