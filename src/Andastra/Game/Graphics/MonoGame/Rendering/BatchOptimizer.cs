using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Batch optimizer for automatic batch size tuning.
    /// 
    /// Automatically optimizes batch sizes based on performance metrics,
    /// finding the optimal balance between draw calls and batch overhead.
    /// 
    /// Features:
    /// - Performance-based tuning
    /// - Automatic batch size adjustment
    /// - Per-material optimization
    /// - Statistics tracking
    /// </summary>
    public class BatchOptimizer
    {
        /// <summary>
        /// Batch performance metrics.
        /// </summary>
        public struct BatchMetrics
        {
            public int BatchSize;
            public float AverageTime;
            public int SampleCount;
        }

        private readonly Dictionary<uint, BatchMetrics> _materialMetrics;
        private readonly Dictionary<uint, int> _optimalBatchSizes;
        private int _defaultBatchSize;

        /// <summary>
        /// Gets or sets the default batch size.
        /// </summary>
        public int DefaultBatchSize
        {
            get { return _defaultBatchSize; }
            set { _defaultBatchSize = Math.Max(1, value); }
        }

        /// <summary>
        /// Initializes a new batch optimizer.
        /// </summary>
        public BatchOptimizer(int defaultBatchSize = 256)
        {
            _materialMetrics = new Dictionary<uint, BatchMetrics>();
            _optimalBatchSizes = new Dictionary<uint, int>();
            _defaultBatchSize = defaultBatchSize;
        }

        /// <summary>
        /// Records batch performance metrics.
        /// </summary>
        public void RecordBatch(uint materialId, int batchSize, float executionTime)
        {
            BatchMetrics metrics;
            if (!_materialMetrics.TryGetValue(materialId, out metrics))
            {
                metrics = new BatchMetrics
                {
                    BatchSize = batchSize,
                    AverageTime = executionTime,
                    SampleCount = 1
                };
            }
            else
            {
                // Update running average
                float total = metrics.AverageTime * metrics.SampleCount + executionTime;
                metrics.SampleCount++;
                metrics.AverageTime = total / metrics.SampleCount;
            }

            _materialMetrics[materialId] = metrics;

            // Optimize batch size periodically
            if (metrics.SampleCount % 100 == 0)
            {
                OptimizeBatchSize(materialId);
            }
        }

        /// <summary>
        /// Gets optimal batch size for a material.
        /// </summary>
        public int GetOptimalBatchSize(uint materialId)
        {
            int optimal;
            if (_optimalBatchSizes.TryGetValue(materialId, out optimal))
            {
                return optimal;
            }
            return _defaultBatchSize;
        }

        /// <summary>
        /// Optimizes batch size for a material.
        /// </summary>
        private void OptimizeBatchSize(uint materialId)
        {
            BatchMetrics metrics;
            if (!_materialMetrics.TryGetValue(materialId, out metrics))
            {
                return;
            }

            // Simple optimization: if time per object is decreasing with larger batches, increase size
            // If time per object is increasing, decrease size
            // Placeholder - would implement more sophisticated algorithm

            int currentOptimal = GetOptimalBatchSize(materialId);
            float timePerObject = metrics.AverageTime / metrics.BatchSize;

            // Adjust based on performance
            if (timePerObject < 0.001f && currentOptimal < 1024)
            {
                _optimalBatchSizes[materialId] = currentOptimal * 2;
            }
            else if (timePerObject > 0.01f && currentOptimal > 64)
            {
                _optimalBatchSizes[materialId] = currentOptimal / 2;
            }
        }

        /// <summary>
        /// Clears all metrics.
        /// </summary>
        public void Clear()
        {
            _materialMetrics.Clear();
            _optimalBatchSizes.Clear();
        }
    }
}

