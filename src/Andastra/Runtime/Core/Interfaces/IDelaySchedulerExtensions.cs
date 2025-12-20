using System;
using Andastra.Runtime.Core.Actions;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Extension methods for IDelayScheduler to support lambda-based scheduling.
    /// </summary>
    public static class IDelaySchedulerExtensions
    {
        /// <summary>
        /// Schedules an action (lambda) to execute after a delay.
        /// </summary>
        /// <param name="scheduler">The delay scheduler.</param>
        /// <param name="delaySeconds">Delay in seconds before action executes.</param>
        /// <param name="action">Action to execute after delay.</param>
        /// <param name="target">Target entity for the action (optional, defaults to null if not provided).</param>
        public static void ScheduleAction(this IDelayScheduler scheduler, float delaySeconds, System.Action action, IEntity target = null)
        {
            if (scheduler == null || action == null)
            {
                return;
            }

            var lambdaAction = new LambdaAction(action);
            scheduler.ScheduleDelay(delaySeconds, lambdaAction, target);
        }
    }
}

