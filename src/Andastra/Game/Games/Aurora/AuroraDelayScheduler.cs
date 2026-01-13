using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Aurora engine delay scheduler implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Delay Scheduler:
    /// - Based on nwmain.exe DelayCommand system
    /// - Located via function: ExecuteCommandDelayCommand @ 0x1405159a0 (nwmain.exe: NWScript DelayCommand function)
    /// - Original implementation: Uses CServerAIMaster::AddEventDeltaTime to schedule delayed actions
    /// - Delay timing: Uses calendar day and time of day system (not simple float time)
    /// - Event system: Integrates with CServerAIMaster event queue system
    /// - AddEventDeltaTime: Schedules events based on world time (calendar day + time of day)
    /// - Event processing: Events processed by CServerAIMaster update loop
    /// - DelayCommand(float fSeconds, action aActionToDelay): Schedules action to execute after fSeconds delay
    /// - Time calculation: Converts float seconds to calendar day and time of day using CWorldTimer
    /// - Event storage: Events stored in CServerAIMaster event queue with calendar day/time of day
    /// - Action execution: Actions execute when event time is reached in AI master update
    /// 
    /// Implementation notes:
    /// - This implementation uses float-based time for simplicity, but the original engine
    ///   uses calendar day/time of day. The conversion is handled internally.
    /// - For full fidelity, this could be extended to use calendar day/time of day system.
    /// </remarks>
    public class AuroraDelayScheduler : BaseDelayScheduler
    {
        private float _currentTime;

        /// <summary>
        /// Initializes a new instance of the AuroraDelayScheduler.
        /// </summary>
        public AuroraDelayScheduler()
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

            // Based on nwmain.exe: ExecuteCommandDelayCommand @ 0x1405159a0
            // Original implementation: Converts float seconds to calendar day and time of day
            // Uses CWorldTimer::GetCalendarDayFromSeconds and GetTimeOfDayFromSeconds
            // Then calls CServerAIMaster::AddEventDeltaTime with calendar day/time of day
            // For this implementation, we use float-based time for simplicity
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
            // Based on nwmain.exe: CServerAIMaster processes events in update loop
            // Original implementation: Events processed when calendar day/time of day is reached
            // For this implementation, we use float-based time for simplicity
            _currentTime += deltaTime;

            // Process all actions that are due
            // Original engine: Actions execute when event time is reached in AI master update
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

