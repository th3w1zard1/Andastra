using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// Performance profiler that tracks method execution times and generates cProfile-like output.
    /// Similar to Python's cProfile module, providing call statistics and timing information.
    /// </summary>
    public class PerformanceProfiler : IDisposable
    {
        private readonly Dictionary<string, CallStats> _callStats = new Dictionary<string, CallStats>();
        private readonly Stack<CallContext> _callStack = new Stack<CallContext>();
        private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
        private readonly string _testName;

        public PerformanceProfiler(string testName)
        {
            _testName = testName ?? "UnknownTest";
        }

        /// <summary>
        /// Start profiling a method call.
        /// </summary>
        public IDisposable ProfileCall(string methodName, string className = null)
        {
            string fullName = string.IsNullOrEmpty(className) ? methodName : $"{className}.{methodName}";
            var context = new CallContext(fullName, Stopwatch.StartNew());
            _callStack.Push(context);

            if (!_callStats.TryGetValue(fullName, out CallStats stats))
            {
                stats = new CallStats(fullName);
                _callStats[fullName] = stats;
            }

            stats.CallCount++;
            return new ProfilerScope(this, context);
        }

        private void EndCall(CallContext context)
        {
            context.Stopwatch.Stop();
            long elapsedMs = context.Stopwatch.ElapsedMilliseconds;

            if (_callStats.TryGetValue(context.MethodName, out CallStats stats))
            {
                stats.TotalTime += elapsedMs;
                stats.CumulativeTime += elapsedMs;

                // Subtract time from parent if exists
                if (_callStack.Count > 0)
                {
                    var parent = _callStack.Peek();
                    if (_callStats.TryGetValue(parent.MethodName, out CallStats parentStats))
                    {
                        parentStats.CumulativeTime -= elapsedMs;
                    }
                }
            }

            _callStack.Pop();
        }

        /// <summary>
        /// Generate a cProfile-like report.
        /// </summary>
        public string GenerateReport()
        {
            _totalStopwatch.Stop();
            long totalMs = _totalStopwatch.ElapsedMilliseconds;
            double totalSeconds = totalMs / 1000.0;

            var report = new StringBuilder();
            report.AppendLine($"=== Performance Profile Report: {_testName} ===");
            report.AppendLine($"Total execution time: {totalSeconds:F3} seconds ({totalMs} ms)");
            report.AppendLine();
            report.AppendLine("Call Statistics (sorted by cumulative time):");
            report.AppendLine();
            report.AppendLine(string.Format("{0,-60} {1,10} {2,12} {3,12} {4,12} {5,10}",
                "Method", "Calls", "Total (ms)", "Per Call (ms)", "Cumulative (ms)", "% Time"));
            report.AppendLine(new string('-', 120));

            var sortedStats = _callStats.Values
                .OrderByDescending(s => s.CumulativeTime)
                .ThenByDescending(s => s.TotalTime)
                .ToList();

            foreach (var stats in sortedStats)
            {
                double totalSecondsMethod = stats.TotalTime / 1000.0;
                double perCallMs = stats.CallCount > 0 ? (double)stats.TotalTime / stats.CallCount : 0;
                double cumulativeSeconds = stats.CumulativeTime / 1000.0;
                double percentage = totalMs > 0 ? (stats.CumulativeTime * 100.0 / totalMs) : 0;

                report.AppendLine(string.Format("{0,-60} {1,10} {2,12:F3} {3,12:F3} {4,12:F3} {5,10:F2}%",
                    TruncateString(stats.MethodName, 60),
                    stats.CallCount,
                    totalSecondsMethod,
                    perCallMs,
                    cumulativeSeconds,
                    percentage));
            }

            report.AppendLine();
            report.AppendLine("Top 10 Bottlenecks (by cumulative time):");
            report.AppendLine();
            int count = 0;
            foreach (var stats in sortedStats.Take(10))
            {
                count++;
                double cumulativeSeconds = stats.CumulativeTime / 1000.0;
                double percentage = totalMs > 0 ? (stats.CumulativeTime * 100.0 / totalMs) : 0;
                report.AppendLine($"{count}. {stats.MethodName}: {cumulativeSeconds:F3}s ({percentage:F2}%) - {stats.CallCount} calls");
            }

            return report.ToString();
        }

        /// <summary>
        /// Save the profiling report to a file.
        /// </summary>
        public void SaveReport(string outputDirectory = null)
        {
            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "profiles");
            }

            Directory.CreateDirectory(outputDirectory);
            string reportFile = Path.Combine(outputDirectory, $"{_testName}.profile.txt");
            File.WriteAllText(reportFile, GenerateReport());
        }

        private static string TruncateString(string str, int maxLength)
        {
            if (str.Length <= maxLength)
            {
                return str;
            }
            return str.Substring(0, maxLength - 3) + "...";
        }

        public void Dispose()
        {
            _totalStopwatch.Stop();
        }

        private class CallStats
        {
            public string MethodName { get; }
            public int CallCount { get; set; }
            public long TotalTime { get; set; } // Time spent in this method (excluding sub-calls)
            public long CumulativeTime { get; set; } // Time including sub-calls

            public CallStats(string methodName)
            {
                MethodName = methodName;
            }
        }

        private class CallContext
        {
            public string MethodName { get; }
            public Stopwatch Stopwatch { get; }

            public CallContext(string methodName, Stopwatch stopwatch)
            {
                MethodName = methodName;
                Stopwatch = stopwatch;
            }
        }

        private class ProfilerScope : IDisposable
        {
            private readonly PerformanceProfiler _profiler;
            private readonly CallContext _context;

            public ProfilerScope(PerformanceProfiler profiler, CallContext context)
            {
                _profiler = profiler;
                _context = context;
            }

            public void Dispose()
            {
                _profiler.EndCall(_context);
            }
        }
    }
}
