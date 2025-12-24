using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// Helper class for performance testing with timeout enforcement and profiling.
    /// Use this in xUnit tests to ensure tests complete within 2 minutes and generate profiling reports.
    /// </summary>
    public class PerformanceTestHelper : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly Process _process;
        private readonly long _initialMemory;
        private readonly int _maxSeconds;
        private readonly bool _enableProfiling;
        private readonly string _testName;
        private readonly ITestOutputHelper _output;
        private readonly StringBuilder _profileReport = new StringBuilder();

        /// <summary>
        /// Creates a performance test helper.
        /// </summary>
        /// <param name="testName">Name of the test (usually from test method name)</param>
        /// <param name="output">xUnit output helper for logging</param>
        /// <param name="maxSeconds">Maximum allowed execution time in seconds (default: 120 = 2 minutes)</param>
        /// <param name="enableProfiling">Whether to generate detailed profiling output (default: true)</param>
        public PerformanceTestHelper(string testName, ITestOutputHelper output = null, int maxSeconds = 120, bool enableProfiling = true)
        {
            _testName = testName ?? "UnknownTest";
            _output = output;
            _maxSeconds = maxSeconds;
            _enableProfiling = enableProfiling;
            _stopwatch = Stopwatch.StartNew();
            _process = Process.GetCurrentProcess();
            _initialMemory = _process.WorkingSet64;

            if (_enableProfiling)
            {
                _profileReport.AppendLine($"=== Performance Profile: {_testName} ===");
                _profileReport.AppendLine($"Start Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                _profileReport.AppendLine($"Initial Memory: {_initialMemory / 1024 / 1024} MB");
                _profileReport.AppendLine($"Process ID: {_process.Id}");
                _profileReport.AppendLine();
            }
        }

        /// <summary>
        /// Check if the test has exceeded the maximum time. Throws TimeoutException if exceeded.
        /// Call this periodically in long-running tests.
        /// </summary>
        public void CheckTimeout()
        {
            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds > _maxSeconds)
            {
                string message = $"Test exceeded maximum execution time of {_maxSeconds} seconds. Actual time: {elapsedSeconds:F3} seconds.";
                if (_enableProfiling)
                {
                    message += $"\nPerformance profile will be saved for analysis.";
                    SaveProfile();
                }
                throw new TimeoutException(message);
            }
        }

        /// <summary>
        /// Get the elapsed time so far.
        /// </summary>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>
        /// Save the profiling report to disk.
        /// </summary>
        private void SaveProfile()
        {
            _stopwatch.Stop();
            _process.Refresh();

            long elapsedMs = _stopwatch.ElapsedMilliseconds;
            double elapsedSeconds = elapsedMs / 1000.0;
            long finalMemory = _process.WorkingSet64;
            long memoryDelta = finalMemory - _initialMemory;

            _profileReport.AppendLine($"End Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
            _profileReport.AppendLine($"Elapsed Time: {elapsedSeconds:F3} seconds ({elapsedMs} ms)");
            _profileReport.AppendLine($"Final Memory: {finalMemory / 1024 / 1024} MB");
            _profileReport.AppendLine($"Memory Delta: {memoryDelta / 1024 / 1024} MB");
            _profileReport.AppendLine($"Peak Memory: {_process.PeakWorkingSet64 / 1024 / 1024} MB");
            _profileReport.AppendLine($"CPU Time (User): {_process.UserProcessorTime.TotalSeconds:F3} seconds");
            _profileReport.AppendLine($"CPU Time (Total): {_process.TotalProcessorTime.TotalSeconds:F3} seconds");
            _profileReport.AppendLine($"Threads: {_process.Threads.Count}");
            _profileReport.AppendLine($"Handles: {_process.HandleCount}");
            _profileReport.AppendLine();

            // Write profiling report to file
            string profileDir = Path.Combine(Directory.GetCurrentDirectory(), "profiles");
            Directory.CreateDirectory(profileDir);
            string profileFile = Path.Combine(profileDir, $"{_testName.Replace(" ", "_").Replace("::", "_").Replace(".", "_")}.profile.txt");
            File.WriteAllText(profileFile, _profileReport.ToString());

            if (_output != null)
            {
                _output.WriteLine($"Performance profile saved to: {profileFile}");
            }
        }

        public void Dispose()
        {
            SaveProfile();

            // Final timeout check
            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            if (elapsedSeconds > _maxSeconds)
            {
                string message = $"Test exceeded maximum execution time of {_maxSeconds} seconds. Actual time: {elapsedSeconds:F3} seconds.";
                if (_enableProfiling)
                {
                    message += $"\nPerformance profile saved for analysis.";
                }
                throw new TimeoutException(message);
            }
        }
    }
}
