using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Draw call sorting system for optimal rendering order.
    /// 
    /// Draw call sorting minimizes state changes and improves GPU utilization
    /// by grouping draw calls with similar state together.
    /// 
    /// Features:
    /// - State-based sorting
    /// - Material-based sorting
    /// - Distance-based sorting (front-to-back, back-to-front)
    /// - Custom sort keys
    /// </summary>
    public class DrawCallSorter
    {
        /// <summary>
        /// Sort mode for draw calls.
        /// </summary>
        public enum SortMode
        {
            /// <summary>
            /// Sort by state to minimize state changes.
            /// </summary>
            State,

            /// <summary>
            /// Sort by material ID.
            /// </summary>
            Material,

            /// <summary>
            /// Sort front-to-back for early-Z optimization.
            /// </summary>
            FrontToBack,

            /// <summary>
            /// Sort back-to-front for transparency.
            /// </summary>
            BackToFront,

            /// <summary>
            /// Custom sort key.
            /// </summary>
            Custom
        }

        /// <summary>
        /// Draw call entry.
        /// </summary>
        public struct DrawCall
        {
            /// <summary>
            /// Sort key for ordering.
            /// </summary>
            public ulong SortKey;

            /// <summary>
            /// Draw call data.
            /// </summary>
            public object Data;

            /// <summary>
            /// Distance from camera (for depth sorting).
            /// </summary>
            public float Distance;
        }

        private readonly List<DrawCall> _drawCalls;
        private SortMode _sortMode;

        /// <summary>
        /// Gets or sets the sort mode.
        /// </summary>
        public SortMode Mode
        {
            get { return _sortMode; }
            set { _sortMode = value; }
        }

        /// <summary>
        /// Gets the number of draw calls.
        /// </summary>
        public int DrawCallCount
        {
            get { return _drawCalls.Count; }
        }

        /// <summary>
        /// Initializes a new draw call sorter.
        /// </summary>
        public DrawCallSorter(SortMode mode = SortMode.State)
        {
            _drawCalls = new List<DrawCall>();
            _sortMode = mode;
        }

        /// <summary>
        /// Adds a draw call.
        /// </summary>
        public void AddDrawCall(object data, uint materialId, uint shaderId, float distance = 0.0f)
        {
            ulong sortKey = CalculateSortKey(materialId, shaderId, distance);
            _drawCalls.Add(new DrawCall
            {
                SortKey = sortKey,
                Data = data,
                Distance = distance
            });
        }

        /// <summary>
        /// Sorts draw calls based on current sort mode.
        /// </summary>
        public void Sort()
        {
            switch (_sortMode)
            {
                case SortMode.State:
                    _drawCalls.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
                    break;

                case SortMode.Material:
                    _drawCalls.Sort((a, b) => (a.SortKey >> 32).CompareTo(b.SortKey >> 32));
                    break;

                case SortMode.FrontToBack:
                    _drawCalls.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    break;

                case SortMode.BackToFront:
                    _drawCalls.Sort((a, b) => b.Distance.CompareTo(a.Distance));
                    break;

                case SortMode.Custom:
                    // Already sorted by custom key
                    _drawCalls.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
                    break;
            }
        }

        /// <summary>
        /// Gets sorted draw calls.
        /// </summary>
        public IReadOnlyList<DrawCall> GetSortedDrawCalls()
        {
            return _drawCalls;
        }

        /// <summary>
        /// Clears all draw calls.
        /// </summary>
        public void Clear()
        {
            _drawCalls.Clear();
        }

        private ulong CalculateSortKey(uint materialId, uint shaderId, float distance)
        {
            // Pack sort key: [MaterialID:32][ShaderID:16][Distance:16]
            ulong key = ((ulong)materialId << 32) | ((ulong)shaderId << 16);

            // Encode distance in lower 16 bits (normalized to 0-65535)
            ushort distanceEncoded = (ushort)Math.Min(65535, (int)(distance * 100.0f));
            key |= distanceEncoded;

            return key;
        }
    }
}

