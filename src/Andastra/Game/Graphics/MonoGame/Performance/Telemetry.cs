using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Andastra.Runtime.MonoGame.Performance
{
    /// <summary>
    /// Telemetry system for performance data collection and analysis.
    /// 
    /// Telemetry collects performance metrics over time, enabling:
    /// - Performance trend analysis
    /// - Bottleneck identification
    /// - Regression detection
    /// - User experience monitoring
    /// 
    /// Features:
    /// - Metric collection
    /// - Time-series data
    /// - Statistical analysis
    /// - Export capabilities
    /// </summary>
    public class Telemetry
    {
        /// <summary>
        /// Telemetry metric.
        /// </summary>
        public struct Metric
        {
            public string Name;
            public float Value;
            public DateTime Timestamp;
            public Dictionary<string, string> Tags;
        }

        private readonly List<Metric> _metrics;
        private readonly Dictionary<string, List<float>> _metricHistory;
        private readonly object _lock;
        private bool _enabled;

        /// <summary>
        /// Gets or sets whether telemetry is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets the number of collected metrics.
        /// </summary>
        public int MetricCount
        {
            get
            {
                lock (_lock)
                {
                    return _metrics.Count;
                }
            }
        }

        /// <summary>
        /// Initializes a new telemetry system.
        /// </summary>
        public Telemetry()
        {
            _metrics = new List<Metric>();
            _metricHistory = new Dictionary<string, List<float>>();
            _lock = new object();
            _enabled = true;
        }

        /// <summary>
        /// Records a metric.
        /// </summary>
        public void RecordMetric(string name, float value, Dictionary<string, string> tags = null)
        {
            if (!_enabled || string.IsNullOrEmpty(name))
            {
                return;
            }

            Metric metric = new Metric
            {
                Name = name,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Tags = tags ?? new Dictionary<string, string>()
            };

            lock (_lock)
            {
                _metrics.Add(metric);

                // Update history
                List<float> history;
                if (!_metricHistory.TryGetValue(name, out history))
                {
                    history = new List<float>();
                    _metricHistory[name] = history;
                }
                history.Add(value);

                // Limit history size
                if (history.Count > 10000)
                {
                    history.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Gets average value for a metric.
        /// </summary>
        public float GetAverage(string metricName)
        {
            lock (_lock)
            {
                List<float> history;
                if (!_metricHistory.TryGetValue(metricName, out history) || history.Count == 0)
                {
                    return 0.0f;
                }

                float sum = 0.0f;
                foreach (float value in history)
                {
                    sum += value;
                }
                return sum / history.Count;
            }
        }

        /// <summary>
        /// Gets minimum value for a metric.
        /// </summary>
        public float GetMin(string metricName)
        {
            lock (_lock)
            {
                List<float> history;
                if (!_metricHistory.TryGetValue(metricName, out history) || history.Count == 0)
                {
                    return 0.0f;
                }

                float min = float.MaxValue;
                foreach (float value in history)
                {
                    if (value < min)
                    {
                        min = value;
                    }
                }
                return min;
            }
        }

        /// <summary>
        /// Gets maximum value for a metric.
        /// </summary>
        public float GetMax(string metricName)
        {
            lock (_lock)
            {
                List<float> history;
                if (!_metricHistory.TryGetValue(metricName, out history) || history.Count == 0)
                {
                    return 0.0f;
                }

                float max = float.MinValue;
                foreach (float value in history)
                {
                    if (value > max)
                    {
                        max = value;
                    }
                }
                return max;
            }
        }

        /// <summary>
        /// Exports metrics to file.
        /// </summary>
        public void ExportMetrics(string filePath)
        {
            lock (_lock)
            {
                // Export metrics to CSV or JSON
                // Placeholder - would implement actual export
            }
        }

        /// <summary>
        /// Clears all metrics.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _metrics.Clear();
                _metricHistory.Clear();
            }
        }
    }
}

