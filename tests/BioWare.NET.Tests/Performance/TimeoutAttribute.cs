using System;
using NUnit.Framework;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// NUnit attribute that enforces a maximum test execution time.
    /// Tests exceeding the timeout will fail automatically.
    /// This is a simpler alternative to PerformanceTestAttribute when profiling is not needed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class TimeoutAttribute : Attribute
    {
        /// <summary>
        /// Maximum allowed execution time in milliseconds.
        /// Default: 120000 (2 minutes).
        /// </summary>
        public int Milliseconds { get; set; } = 120000; // 2 minutes default

        public TimeoutAttribute()
        {
        }

        public TimeoutAttribute(int milliseconds)
        {
            Milliseconds = milliseconds;
        }
    }
}
