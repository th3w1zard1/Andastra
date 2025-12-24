using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// xUnit attribute that enforces a maximum test execution time and generates profiling output.
    /// Tests exceeding the timeout will fail automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class PerformanceTestAttribute : Attribute, IDisposable
    {
        private readonly int _maxSeconds;
        private readonly bool _enableProfiling;
        private readonly Stopwatch _stopwatch;
        private readonly Process _process;
        private readonly long _initialMemory;
        private readonly StringBuilder _profileReport = new StringBuilder();
        private string _testName;
        private ITestOutputHelper _output;

        /// <summary>
        /// Creates a performance test attribute with a maximum execution time.
        /// </summary>
        /// <param name="maxSeconds">Maximum allowed execution time in seconds (default: 120 = 2 minutes)</param>
        /// <param name="enableProfiling">Whether to generate detailed profiling output (default: true)</param>
        public PerformanceTestAttribute(int maxSeconds = 120, bool enableProfiling = true)
        {
            _maxSeconds = maxSeconds;
            _enableProfiling = enableProfiling;
            _stopwatch = Stopwatch.StartNew();
            _process = Process.GetCurrentProcess();
            _initialMemory = _process.WorkingSet64;
            _testName = "UnknownTest";

            if (_enableProfiling)
            {
                _profileReport.Clear();
                _profileReport.AppendLine($"=== Performance Profile: {_testName} ===");
                _profileReport.AppendLine($"Start Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                _profileReport.AppendLine($"Initial Memory: {_initialMemory / 1024 / 1024} MB");
                _profileReport.AppendLine();
            }
        }

        /// <summary>
        /// Initialize with test name and output helper (called by test framework).
        /// </summary>
        public void Initialize(string testName, ITestOutputHelper output = null)
        {
            _testName = testName;
            _output = output;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            long elapsedMs = _stopwatch.ElapsedMilliseconds;
            double elapsedSeconds = elapsedMs / 1000.0;

            if (_enableProfiling)
            {
                _process.Refresh();
                long finalMemory = _process.WorkingSet64;
                long memoryDelta = finalMemory - _initialMemory;

                _profileReport.Clear();
                _profileReport.AppendLine($"=== Performance Profile: {_testName} ===");
                _profileReport.AppendLine($"Start Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                _profileReport.AppendLine($"Elapsed Time: {elapsedSeconds:F3} seconds ({elapsedMs} ms)");
                _profileReport.AppendLine($"Initial Memory: {_initialMemory / 1024 / 1024} MB");
                _profileReport.AppendLine($"Final Memory: {finalMemory / 1024 / 1024} MB");
                _profileReport.AppendLine($"Memory Delta: {memoryDelta / 1024 / 1024} MB");
                _profileReport.AppendLine($"CPU Time (User): {_process.UserProcessorTime.TotalSeconds:F3} seconds");
                _profileReport.AppendLine($"CPU Time (Total): {_process.TotalProcessorTime.TotalSeconds:F3} seconds");
                _profileReport.AppendLine($"Threads: {_process.Threads.Count}");
                _profileReport.AppendLine();

                // Write profiling report to file
                string profileDir = Path.Combine(Directory.GetCurrentDirectory(), "profiles");
                Directory.CreateDirectory(profileDir);
                string profileFile = Path.Combine(profileDir, $"{_testName.Replace(" ", "_").Replace("::", "_")}.profile.txt");
                File.WriteAllText(profileFile, _profileReport.ToString());

                if (_output != null)
                {
                    _output.WriteLine($"Performance profile saved to: {profileFile}");
                }
            }

            // Fail test if it exceeds the maximum time
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

    /// <summary>
    /// xUnit test base class that provides performance profiling and timeout enforcement.
    /// </summary>
    public abstract class PerformanceTestAttributeBase : IDisposable
    {
        private readonly PerformanceTestAttribute _performanceAttribute;
        private readonly Stopwatch _stopwatch;
        private readonly Process _process;
        private readonly long _initialMemory;
        private readonly StringBuilder _profileReport = new StringBuilder();
        private readonly string _testName;

        protected PerformanceTestAttributeBase(int maxSeconds = 120, bool enableProfiling = true)
        {
            _performanceAttribute = new PerformanceTestAttribute(maxSeconds, enableProfiling);
            _stopwatch = Stopwatch.StartNew();
            _process = Process.GetCurrentProcess();
            _initialMemory = _process.WorkingSet64;
            _testName = GetType().Name;

            if (enableProfiling)
            {
                _profileReport.AppendLine($"=== Performance Profile: {_testName} ===");
                _profileReport.AppendLine($"Start Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
                _profileReport.AppendLine($"Initial Memory: {_initialMemory / 1024 / 1024} MB");
                _profileReport.AppendLine();
            }
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            long elapsedMs = _stopwatch.ElapsedMilliseconds;
            double elapsedSeconds = elapsedMs / 1000.0;

            if (_performanceAttribute != null)
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
                string profileDir = Path.Combine(Directory.GetCurrentDirectory(), "profiles");
                Directory.CreateDirectory(profileDir);
                string profileFile = Path.Combine(profileDir, $"{_testName}.profile.txt");
                File.WriteAllText(profileFile, _profileReport.ToString());
            }

            // Fail test if it exceeds the maximum time
            int maxSeconds = 120; // Default
            if (elapsedSeconds > maxSeconds)
            {
                string message = $"Test exceeded maximum execution time of {maxSeconds} seconds. Actual time: {elapsedSeconds:F3} seconds.";
                throw new TimeoutException(message);
            }
        }
    }
}

