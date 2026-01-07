using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Render queue system for priority-based rendering order.
    /// 
    /// Render queues organize rendering by priority and type, ensuring
    /// correct rendering order (opaque before transparent, etc.).
    /// 
    /// Features:
    /// - Multiple render queues (opaque, transparent, UI, etc.)
    /// - Priority-based sorting
    /// - Automatic queue management
    /// - Render order optimization
    /// </summary>
    public class RenderQueue
    {
        /// <summary>
        /// Render queue types.
        /// </summary>
        public enum QueueType
        {
            /// <summary>
            /// Background/sky rendering (first).
            /// </summary>
            Background = 0,

            /// <summary>
            /// Opaque geometry (depth tested).
            /// </summary>
            Opaque = 1000,

            /// <summary>
            /// Alpha tested geometry.
            /// </summary>
            AlphaTest = 2000,

            /// <summary>
            /// Transparent geometry (back-to-front).
            /// </summary>
            Transparent = 3000,

            /// <summary>
            /// Overlay/UI rendering (last).
            /// </summary>
            Overlay = 4000
        }

        /// <summary>
        /// Render queue entry.
        /// </summary>
        public struct QueueEntry
        {
            /// <summary>
            /// Queue type.
            /// </summary>
            public QueueType Type;

            /// <summary>
            /// Priority within queue (lower = earlier).
            /// </summary>
            public int Priority;

            /// <summary>
            /// Distance from camera (for sorting).
            /// </summary>
            public float Distance;

            /// <summary>
            /// Render data.
            /// </summary>
            public object RenderData;
        }

        private readonly Dictionary<QueueType, List<QueueEntry>> _queues;

        /// <summary>
        /// Initializes a new render queue.
        /// </summary>
        public RenderQueue()
        {
            _queues = new Dictionary<QueueType, List<QueueEntry>>();
            foreach (QueueType type in Enum.GetValues(typeof(QueueType)))
            {
                _queues[type] = new List<QueueEntry>();
            }
        }

        /// <summary>
        /// Adds a render entry to the queue.
        /// </summary>
        public void Add(QueueType type, object renderData, int priority = 0, float distance = 0.0f)
        {
            QueueEntry entry = new QueueEntry
            {
                Type = type,
                Priority = priority,
                Distance = distance,
                RenderData = renderData
            };

            _queues[type].Add(entry);
        }

        /// <summary>
        /// Sorts all queues.
        /// </summary>
        public void Sort()
        {
            foreach (var kvp in _queues)
            {
                List<QueueEntry> queue = kvp.Value;

                if (kvp.Key == QueueType.Transparent)
                {
                    // Sort transparent back-to-front
                    queue.Sort((a, b) => b.Distance.CompareTo(a.Distance));
                }
                else
                {
                    // Sort by priority, then distance (front-to-back for early-Z)
                    queue.Sort((a, b) =>
                    {
                        int priorityCmp = a.Priority.CompareTo(b.Priority);
                        if (priorityCmp != 0)
                        {
                            return priorityCmp;
                        }
                        return a.Distance.CompareTo(b.Distance);
                    });
                }
            }
        }

        /// <summary>
        /// Gets all entries in render order.
        /// </summary>
        public IEnumerable<QueueEntry> GetEntries()
        {
            // Return entries in queue type order
            foreach (QueueType type in Enum.GetValues(typeof(QueueType)))
            {
                foreach (QueueEntry entry in _queues[type])
                {
                    yield return entry;
                }
            }
        }

        /// <summary>
        /// Clears all queues.
        /// </summary>
        public void Clear()
        {
            foreach (List<QueueEntry> queue in _queues.Values)
            {
                queue.Clear();
            }
        }
    }
}

