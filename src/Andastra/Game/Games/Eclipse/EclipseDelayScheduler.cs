using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse engine delay scheduler implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Delay Scheduler:
    /// - Based on daorigins.exe and DragonAge2.exe delay systems
    /// - Similar to Odyssey engine: Uses float-based time system
    /// - DelayCommand functionality: Schedules actions to execute after specified delay
    /// - Priority queue: Uses sorted list by execution time to efficiently process delayed actions
    /// - Delayed actions: Execute in order based on schedule time (ascending by executeTime)
    /// - Action execution: Actions are queued to target entity's action queue when delay expires
    /// - Entity validation: Checks if target entity is still valid before executing delayed action
    /// 
    /// Note: Eclipse engine delay system is similar to Odyssey, using float-based time.
    /// Engine-specific differences may exist in integration with other systems.
    /// </remarks>
    public class EclipseDelayScheduler : BaseDelayScheduler
    {
        private float _currentTime;

        /// <summary>
        /// Initializes a new instance of the EclipseDelayScheduler.
        /// </summary>
        public EclipseDelayScheduler()
        {
            _currentTime = 0;
        }

        /// <summary>
        /// Schedules an action to execute after a delay.
        /// </summary>
        /// <param name="delaySeconds">Delay in seconds before action executes.</param>
        /// <param name="action">Action to execute after delay.</param>
        /// <param name="target">Target entity for the action.</param>
        public override void ScheduleDelay(float delaySeconds, IAction action, IEntity target)
        {
            if (action == null || target == null)
            {
                return;
            }

            float executeTime = _currentTime + delaySeconds;

            var delayed = new DelayedAction
            {
                ExecuteTime = executeTime,
                Action = action,
                Target = target
            };

            InsertDelayedAction(delayed);
        }

        /// <summary>
        /// Updates the scheduler and fires any due actions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        public override void Update(float deltaTime)
        {
            // Based on daorigins.exe and DragonAge2.exe: Delay scheduler implementation
            // Original implementation: Processes delayed actions in order based on execution time
            // Uses game simulation time to track when actions should execute
            _currentTime += deltaTime;

            // Process all actions that are due
            // Original engine: Actions execute in order (sorted by executeTime)
            // Actions are queued to target entity's action queue when delay expires
            while (_delayedActions.Count > 0 && _delayedActions[0].ExecuteTime <= _currentTime)
            {
                DelayedAction delayed = _delayedActions[0];
                _delayedActions.RemoveAt(0);

                ExecuteDelayedAction(delayed);
            }
        }

        /// <summary>
        /// Resets the current time (for testing).
        /// </summary>
        public void Reset()
        {
            ClearAll();
            _currentTime = 0;
        }
    }
}

