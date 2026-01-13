using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// NUnit attribute that enforces a maximum test execution time and generates profiling output.
    /// Tests exceeding the timeout will fail automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class PerformanceTestAttribute : Attribute, ITestAction
    {
        private readonly int _maxSeconds;
        private readonly bool _enableProfiling;
        private Stopwatch _stopwatch;
        private Process _process;
        private long _initialMemory;
        private readonly StringBuilder _profileReport = new StringBuilder();

        /// <summary>
        /// Creates a performance test attribute with a maximum execution time.
        /// </summary>
        /// <param name="maxSeconds">Maximum allowed execution time in seconds (default: 120 = 2 minutes)</param>
        /// <param name="enableProfiling">Whether to generate detailed profiling output (default: true)</param>
        public PerformanceTestAttribute(int maxSeconds = 120, bool enableProfiling = true)
        {
            _maxSeconds = maxSeconds;
            _enableProfiling = enableProfiling;
        }

        public void BeforeTest(ITest test)
        {
            _stopwatch = Stopwatch.StartNew();
            _process = Process.GetCurrentProcess();
            _initialMemory = _process.WorkingSet64;

            if (_enableProfiling)
            {
                _profileReport.Clear();
                _profileReport.AppendLine($"=== Performance Profile: {test.FullName} ===");
                _profileReport.AppendLine($"Start Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                _profileReport.AppendLine($"Initial Memory: {_initialMemory / 1024 / 1024} MB");
                _profileReport.AppendLine();
            }
        }

        public void AfterTest(ITest test)
        {
            _stopwatch.Stop();
            long elapsedMs = _stopwatch.ElapsedMilliseconds;
            double elapsedSeconds = elapsedMs / 1000.0;

            if (_enableProfiling)
            {
                _process.Refresh();
                long finalMemory = _process.WorkingSet64;
                long memoryDelta = finalMemory - _initialMemory;

                _profileReport.AppendLine($"End Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                _profileReport.AppendLine($"Elapsed Time: {elapsedSeconds:F3} seconds ({elapsedMs} ms)");
                _profileReport.AppendLine($"Final Memory: {finalMemory / 1024 / 1024} MB");
                _profileReport.AppendLine($"Memory Delta: {memoryDelta / 1024 / 1024} MB");
                _profileReport.AppendLine($"CPU Time (User): {_process.UserProcessorTime.TotalSeconds:F3} seconds");
                _profileReport.AppendLine($"CPU Time (Total): {_process.TotalProcessorTime.TotalSeconds:F3} seconds");
                _profileReport.AppendLine($"Threads: {_process.Threads.Count}");
                _profileReport.AppendLine();

                // Write profiling report to file
                string profileDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "profiles");
                Directory.CreateDirectory(profileDir);
                string profileFile = Path.Combine(profileDir, $"{test.FullName.Replace(" ", "_").Replace("::", "_")}.profile.txt");
                File.WriteAllText(profileFile, _profileReport.ToString());
                TestContext.Progress.WriteLine($"Performance profile saved to: {profileFile}");
            }

            // Fail test if it exceeds the maximum time
            if (elapsedSeconds > _maxSeconds)
            {
                string message = $"Test exceeded maximum execution time of {_maxSeconds} seconds. Actual time: {elapsedSeconds:F3} seconds.";
                if (_enableProfiling)
                {
                    message += $"\nPerformance profile saved for analysis.";
                }
                Assert.Fail(message);
            }
        }

        public ActionTargets Targets => ActionTargets.Test;
    }
}
