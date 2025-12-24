using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// Base class for performance-tested classes in xUnit.
    /// Automatically enforces 2-minute timeout and generates profiling reports.
    /// </summary>
    public abstract class PerformanceTestBase : IDisposable
    {
        protected readonly PerformanceTestHelper PerformanceHelper;
        protected readonly ITestOutputHelper Output;

        /// <summary>
        /// Creates a performance test base with timeout enforcement.
        /// </summary>
        /// <param name="output">xUnit output helper</param>
        /// <param name="maxSeconds">Maximum allowed execution time in seconds (default: 120 = 2 minutes)</param>
        /// <param name="enableProfiling">Whether to generate detailed profiling output (default: true)</param>
        protected PerformanceTestBase(ITestOutputHelper output = null, int maxSeconds = 120, bool enableProfiling = true)
        {
            Output = output;
            string testName = GetType().Name;
            PerformanceHelper = new PerformanceTestHelper(testName, output, maxSeconds, enableProfiling);
        }

        /// <summary>
        /// Check if the test has exceeded the maximum time. Call this periodically in long-running tests.
        /// </summary>
        protected void CheckTimeout()
        {
            PerformanceHelper?.CheckTimeout();
        }

        public virtual void Dispose()
        {
            PerformanceHelper?.Dispose();
        }
    }
}
