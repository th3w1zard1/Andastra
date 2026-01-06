// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:197-202
// Original: def stop_profiler(profiler: cProfile.Profile): ...
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace KotorDiff.AppCore
{
    /// <summary>
    /// Simple profiler that mimics Python's cProfile functionality.
    /// Tracks method execution times and call counts.
    /// </summary>
    public class Profiler
    {
        private readonly Dictionary<string, ProfilerStats> _stats = new Dictionary<string, ProfilerStats>();
        private readonly Stopwatch _totalStopwatch = new Stopwatch();
        private bool _enabled;

        /// <summary>
        /// Enable profiling.
        /// </summary>
        public void Enable()
        {
            _enabled = true;
            _totalStopwatch.Start();
        }

        /// <summary>
        /// Disable profiling.
        /// </summary>
        public void Disable()
        {
            _enabled = false;
            _totalStopwatch.Stop();
        }

        /// <summary>
        /// Check if profiling is enabled.
        /// </summary>
        public bool IsEnabled => _enabled;

        /// <summary>
        /// Profile a method call. Call this at the start and end of methods you want to profile.
        /// </summary>
        public IDisposable Profile(string methodName)
        {
            if (!_enabled)
            {
                return new NullProfilerScope();
            }

            return new ProfilerScope(this, methodName);
        }

        private void RecordCall(string methodName, TimeSpan elapsed)
        {
            if (!_stats.ContainsKey(methodName))
            {
                _stats[methodName] = new ProfilerStats();
            }

            var stats = _stats[methodName];
            stats.CallCount++;
            stats.TotalTime += elapsed;
            if (elapsed > stats.MaxTime)
            {
                stats.MaxTime = elapsed;
            }
            if (stats.MinTime == TimeSpan.Zero || elapsed < stats.MinTime)
            {
                stats.MinTime = elapsed;
            }
        }

        /// <summary>
        /// Dump profiling statistics to a file in a format similar to cProfile's pstat format.
        /// </summary>
        public void DumpStats(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Profiler Statistics");
            sb.AppendLine("==================");
            sb.AppendLine($"Total profiling time: {_totalStopwatch.Elapsed.TotalSeconds:F6} seconds");
            sb.AppendLine();
            sb.AppendLine("Function calls ordered by cumulative time:");
            sb.AppendLine();

            // Sort by total time (descending)
            var sortedStats = _stats.OrderByDescending(kvp => kvp.Value.TotalTime).ToList();

            sb.AppendLine(string.Format("{0,-50} {1,10} {2,15} {3,15} {4,15} {5,15}",
                "Function", "Calls", "Total Time (s)", "Per Call (s)", "Min Time (s)", "Max Time (s)"));
            sb.AppendLine(new string('-', 120));

            foreach (var kvp in sortedStats)
            {
                var stats = kvp.Value;
                var perCall = stats.CallCount > 0 ? stats.TotalTime.TotalSeconds / stats.CallCount : 0;
                sb.AppendLine(string.Format("{0,-50} {1,10} {2,15:F6} {3,15:F6} {4,15:F6} {5,15:F6}",
                    kvp.Key,
                    stats.CallCount,
                    stats.TotalTime.TotalSeconds,
                    perCall,
                    stats.MinTime.TotalSeconds,
                    stats.MaxTime.TotalSeconds));
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        private class ProfilerStats
        {
            public int CallCount { get; set; }
            public TimeSpan TotalTime { get; set; }
            public TimeSpan MinTime { get; set; }
            public TimeSpan MaxTime { get; set; }
        }

        private class ProfilerScope : IDisposable
        {
            private readonly Profiler _profiler;
            private readonly string _methodName;
            private readonly Stopwatch _stopwatch;

            public ProfilerScope(Profiler profiler, string methodName)
            {
                _profiler = profiler;
                _methodName = methodName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _profiler.RecordCall(_methodName, _stopwatch.Elapsed);
            }
        }

        private class NullProfilerScope : IDisposable
        {
            public void Dispose()
            {
                // Do nothing
            }
        }
    }
}

