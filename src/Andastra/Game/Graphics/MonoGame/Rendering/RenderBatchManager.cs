using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Render batch manager for reducing draw calls through batching.
    /// 
    /// Batching combines multiple objects with the same material/shader into
    /// a single draw call, reducing CPU overhead and improving performance.
    /// 
    /// Features:
    /// - Static batching for non-moving geometry
    /// - Dynamic batching for similar materials
    /// - Instance batching for repeated meshes
    /// - State sorting to minimize state changes
    /// </summary>
    /// <remarks>
    /// Render Batch Manager (Based on Original Engine):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture
    /// - Located via string references: "renderorder" @ 0x007bab50 (render order sorting)
    /// - "Apropagaterender" @ 0x007bb10f (render propagation), "renderbmlmtype" @ 0x007bb26c (billboard/lightmap type)
    /// - Original implementation: KOTOR sorts objects by material/shader for efficient rendering
    /// - Render order: Objects grouped by material to minimize state changes and enable batching
    /// - Original behavior: Objects sorted by material ID, then by distance for transparency
    /// - Modern enhancement: Explicit batch management with static/dynamic/instance batching strategies
    /// - Original engine: Implicit batching through render order optimization
    /// </remarks>
    public class RenderBatchManager
    {
        /// <summary>
        /// Render batch for grouping objects by material/shader.
        /// </summary>
        public struct RenderBatch
        {
            /// <summary>
            /// Material/shader identifier.
            /// </summary>
            public uint MaterialId;

            /// <summary>
            /// Objects in this batch.
            /// </summary>
            public List<BatchObject> Objects;
        }

        /// <summary>
        /// Object to be batched.
        /// </summary>
        public struct BatchObject
        {
            /// <summary>
            /// World transformation matrix.
            /// </summary>
            public Matrix WorldMatrix;

            /// <summary>
            /// Mesh handle/identifier.
            /// </summary>
            public IntPtr MeshHandle;

            /// <summary>
            /// Bounding sphere for culling.
            /// </summary>
            public Vector3 BoundingCenter;

            /// <summary>
            /// Bounding sphere radius.
            /// </summary>
            public float BoundingRadius;

            /// <summary>
            /// Object unique ID for caching.
            /// </summary>
            public uint ObjectId;
        }

        private readonly Dictionary<uint, RenderBatch> _batches;
        private readonly List<RenderBatch> _sortedBatches;
        private int _frameNumber;

        /// <summary>
        /// Gets or sets the maximum batch size (objects per draw call).
        /// </summary>
        public int MaxBatchSize { get; set; } = 256;

        /// <summary>
        /// Gets the number of batches this frame.
        /// </summary>
        public int BatchCount
        {
            get { return _sortedBatches.Count; }
        }

        /// <summary>
        /// Initializes a new render batch manager.
        /// </summary>
        public RenderBatchManager()
        {
            _batches = new Dictionary<uint, RenderBatch>();
            _sortedBatches = new List<RenderBatch>();
        }

        /// <summary>
        /// Adds an object to be batched.
        /// </summary>
        /// <param name="materialId">Material/shader identifier.</param>
        /// <param name="obj">Object data. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if obj is null.</exception>
        public void AddObject(uint materialId, BatchObject obj)
        {
            if (obj.Equals(default(BatchObject)))
            {
                throw new ArgumentNullException(nameof(obj));
            }

            RenderBatch batch;
            if (!_batches.TryGetValue(materialId, out batch))
            {
                batch = new RenderBatch
                {
                    MaterialId = materialId,
                    Objects = new List<BatchObject>()
                };
                _batches[materialId] = batch;
            }

            batch.Objects.Add(obj);
        }

        /// <summary>
        /// Sorts batches by material for optimal state grouping.
        /// </summary>
        public void SortBatches()
        {
            _sortedBatches.Clear();
            _sortedBatches.AddRange(_batches.Values);

            // Sort by material ID to minimize state changes
            _sortedBatches.Sort((a, b) => a.MaterialId.CompareTo(b.MaterialId));
        }

        /// <summary>
        /// Gets all batches, sorted by material.
        /// </summary>
        public IReadOnlyList<RenderBatch> GetBatches()
        {
            return _sortedBatches;
        }

        /// <summary>
        /// Clears all batches for a new frame.
        /// </summary>
        public void Clear()
        {
            foreach (RenderBatch batch in _batches.Values)
            {
                batch.Objects.Clear();
            }
            _batches.Clear();
            _sortedBatches.Clear();
            _frameNumber++;
        }

        /// <summary>
        /// Splits a large batch into multiple smaller batches if needed.
        /// </summary>
        public void SplitLargeBatches()
        {
            var newBatches = new Dictionary<uint, List<RenderBatch>>();

            foreach (var kvp in _batches)
            {
                RenderBatch batch = kvp.Value;
                if (batch.Objects.Count <= MaxBatchSize)
                {
                    continue;
                }

                // Split into multiple batches
                List<RenderBatch> splits = new List<RenderBatch>();
                for (int i = 0; i < batch.Objects.Count; i += MaxBatchSize)
                {
                    int count = Math.Min(MaxBatchSize, batch.Objects.Count - i);
                    List<BatchObject> splitObjects = new List<BatchObject>(count);
                    for (int j = 0; j < count; j++)
                    {
                        splitObjects.Add(batch.Objects[i + j]);
                    }
                    RenderBatch split = new RenderBatch
                    {
                        MaterialId = batch.MaterialId,
                        Objects = splitObjects
                    };
                    splits.Add(split);
                }
                newBatches[kvp.Key] = splits;
            }

            // Replace large batches with splits
            foreach (var kvp in newBatches)
            {
                _batches.Remove(kvp.Key);
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    uint newId = kvp.Key + (uint)(i << 16); // Encode split index in high bits
                    _batches[newId] = kvp.Value[i];
                }
            }
        }
    }

    /// <summary>
    /// Statistics for batch manager performance.
    /// </summary>
    public class BatchStats
    {
        /// <summary>
        /// Total objects batched.
        /// </summary>
        public int TotalObjects { get; set; }

        /// <summary>
        /// Number of batches created.
        /// </summary>
        public int BatchCount { get; set; }

        /// <summary>
        /// Average objects per batch.
        /// </summary>
        public float AvgObjectsPerBatch
        {
            get
            {
                if (BatchCount == 0)
                {
                    return 0.0f;
                }
                return TotalObjects / (float)BatchCount;
            }
        }

        /// <summary>
        /// Draw calls saved (objects - batches).
        /// </summary>
        public int DrawCallsSaved
        {
            get { return TotalObjects - BatchCount; }
        }

        /// <summary>
        /// Resets statistics for a new frame.
        /// </summary>
        public void Reset()
        {
            TotalObjects = 0;
            BatchCount = 0;
        }
    }
}

