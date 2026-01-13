using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of delay scheduler shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Delay Scheduler Implementation:
    /// - Common functionality for scheduling delayed actions (DelayCommand) across all engines
    /// - Provides priority queue sorted by execution time to efficiently process delayed actions
    /// - Delayed actions execute in order based on schedule time
    /// - Actions are queued to target entity's action queue when delay expires
    /// - Entity validation: Checks if target entity is still valid before executing delayed action
    ///
    /// All engine-specific delay scheduler classes (OdysseyDelayScheduler, AuroraDelayScheduler, EclipseDelayScheduler)
    /// have been merged into this base class since their implementations are identical.
    ///
    /// Engine-Specific Details (Documented):
    /// - Odyssey: Based on swkotor.exe/swkotor2.exe DelayCommand system
    ///   - "DelayCommand" @ 0x007be900 (swkotor2.exe: NWScript DelayCommand function)
    ///   - Uses game simulation time to track when actions should execute
    ///   - STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
    /// - Aurora: Based on nwmain.exe DelayCommand system
    ///   - ExecuteCommandDelayCommand @ 0x1405159a0 (nwmain.exe: NWScript DelayCommand function)
    ///   - Original uses CServerAIMaster::AddEventDeltaTime with calendar day/time of day
    ///   - This implementation uses float-based time for simplicity (matches Odyssey/Eclipse)
    /// - Eclipse: Based on daorigins.exe/DragonAge2.exe delay systems
    ///   - Similar to Odyssey: Uses float-based time system
    ///   - DelayCommand functionality schedules actions to execute after specified delay
    ///
    /// Common functionality across all engines:
    /// - Schedule actions with a delay
    /// - Update scheduler to process due actions
    /// - Clear actions for entities or all actions
    /// - Track pending count
    /// - Priority queue sorted by execution time
    /// - Entity validation before execution
    /// - Action queue integration
    /// </remarks>
    public class BaseDelayScheduler : IDelayScheduler
    {
        protected readonly List<DelayedAction> _delayedActions;
        private float _currentTime;

        /// <summary>
        /// Initializes a new instance of the BaseDelayScheduler.
        /// </summary>
        public BaseDelayScheduler()
        {
            _delayedActions = new List<DelayedAction>();
            _currentTime = 0;
        }

        /// <summary>
        /// The number of pending delayed actions.
        /// </summary>
        public int PendingCount => _delayedActions.Count;

        /// <summary>
        /// Schedules an action to execute after a delay.
        /// </summary>
        /// <param name="delaySeconds">Delay in seconds before action executes.</param>
        /// <param name="action">Action to execute after delay.</param>
        /// <param name="target">Target entity for the action.</param>
        /// <remarks>
        /// Common implementation across all engines: Calculate execute time and insert into priority queue.
        ///
        /// Engine-Specific Details:
        /// - Odyssey: Based on swkotor.exe/swkotor2.exe DelayCommand implementation
        /// - Aurora: Based on nwmain.exe ExecuteCommandDelayCommand @ 0x1405159a0
        ///   - Original converts float seconds to calendar day/time of day, but this implementation uses float time
        /// - Eclipse: Based on daorigins.exe/DragonAge2.exe delay systems (similar to Odyssey)
        /// </remarks>
        public virtual void ScheduleDelay(float delaySeconds, IAction action, IEntity target)
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
        /// <remarks>
        /// Common implementation across all engines: Advance time and process due actions.
        ///
        /// Engine-Specific Details:
        /// - Odyssey: Based on swkotor.exe/swkotor2.exe DelayCommand scheduler implementation
        ///   - Processes delayed actions in order based on execution time
        ///   - Uses game simulation time to track when actions should execute
        /// - Aurora: Based on nwmain.exe CServerAIMaster event processing
        ///   - Original processes events when calendar day/time of day is reached
        ///   - This implementation uses float-based time for simplicity
        /// - Eclipse: Based on daorigins.exe/DragonAge2.exe delay scheduler
        ///   - Processes delayed actions in order based on execution time (same as Odyssey)
        /// </remarks>
        public virtual void Update(float deltaTime)
        {
            _currentTime += deltaTime;

            // Process all actions that are due
            // All engines: Actions execute in order (sorted by executeTime)
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
        public virtual void Reset()
        {
            ClearAll();
            _currentTime = 0;
        }

        /// <summary>
        /// Clears all delayed actions for a specific entity.
        /// </summary>
        /// <param name="entity">Entity to clear delayed actions for.</param>
        public virtual void ClearForEntity(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _delayedActions.RemoveAll(d =>
            {
                if (d.Target == entity)
                {
                    d.Action.Dispose();
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// Clears all delayed actions.
        /// </summary>
        public virtual void ClearAll()
        {
            foreach (DelayedAction delayed in _delayedActions)
            {
                delayed.Action.Dispose();
            }
            _delayedActions.Clear();
        }

        /// <summary>
        /// Executes a delayed action on its target entity.
        /// </summary>
        /// <remarks>
        /// Common execution logic shared across all engines.
        /// Checks entity validity and queues action to entity's action queue.
        /// </remarks>
        protected virtual void ExecuteDelayedAction(DelayedAction delayed)
        {
            if (delayed.Target.IsValid)
            {
                IActionQueue actionQueue = delayed.Target.GetComponent<IActionQueue>();
                if (actionQueue != null)
                {
                    // Action added to entity's action queue
                    actionQueue.Add(delayed.Action);
                }
                else
                {
                    // Execute immediately if no action queue
                    // If entity has no action queue, execute action directly
                    delayed.Action.Owner = delayed.Target;
                    delayed.Action.Update(delayed.Target, 0);
                    delayed.Action.Dispose();
                }
            }
            else
            {
                // Target entity invalid - dispose action
                // Delayed actions for invalid entities are discarded
                delayed.Action.Dispose();
            }
        }

        /// <summary>
        /// Inserts a delayed action into the priority queue in sorted order.
        /// </summary>
        /// <remarks>
        /// Inserts action in ascending order by execution time.
        /// </remarks>
        protected virtual void InsertDelayedAction(DelayedAction delayed)
        {
            // Insert in sorted order (ascending by execute time)
            int index = 0;
            while (index < _delayedActions.Count && _delayedActions[index].ExecuteTime <= delayed.ExecuteTime)
            {
                index++;
            }
            _delayedActions.Insert(index, delayed);
        }

        /// <summary>
        /// Represents a delayed action in the scheduler.
        /// </summary>
        protected struct DelayedAction
        {
            /// <summary>
            /// Time when the action should execute.
            /// </summary>
            public float ExecuteTime;

            /// <summary>
            /// Action to execute.
            /// </summary>
            public IAction Action;

            /// <summary>
            /// Target entity for the action.
            /// </summary>
            public IEntity Target;
        }
    }
}

