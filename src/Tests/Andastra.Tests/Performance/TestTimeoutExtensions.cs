using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Andastra.Parsing.Tests.Performance
{
    /// <summary>
    /// Extension methods for enforcing test timeouts in xUnit tests.
    /// </summary>
    public static class TestTimeoutExtensions
    {
        /// <summary>
        /// Executes an action with a timeout, throwing TimeoutException if exceeded.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="timeoutSeconds">Maximum time in seconds (default: 120 = 2 minutes)</param>
        public static void WithTimeout(Action action, int timeoutSeconds = 120)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        throw new AggregateException(ex);
                    }
                }, cts.Token);

                try
                {
                    if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
                    {
                        throw new TimeoutException($"Operation exceeded maximum execution time of {timeoutSeconds} seconds.");
                    }
                    task.GetAwaiter().GetResult();
                }
                catch (AggregateException ae)
                {
                    if (ae.InnerException != null)
                    {
                        throw ae.InnerException;
                    }
                    throw;
                }
            }
        }

        /// <summary>
        /// Executes an async action with a timeout, throwing TimeoutException if exceeded.
        /// </summary>
        /// <param name="action">The async action to execute</param>
        /// <param name="timeoutSeconds">Maximum time in seconds (default: 120 = 2 minutes)</param>
        public static async Task WithTimeoutAsync(Func<Task> action, int timeoutSeconds = 120)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                try
                {
                    await Task.Run(async () => await action(), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"Operation exceeded maximum execution time of {timeoutSeconds} seconds.");
                }
            }
        }
    }
}
