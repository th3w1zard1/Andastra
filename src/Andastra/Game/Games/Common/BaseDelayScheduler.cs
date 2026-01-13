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
    /// Common functionality across all engines:
    /// - Schedule actions with a delay
    /// - Update scheduler to process due actions
    /// - Clear actions for entities or all actions
    /// - Track pending count
    /// - Priority queue sorted by execution time
    /// - Entity validation before execution
    /// - Action queue integration
    /// 
    /// Engine-specific implementations handle:
    /// - Time calculation (float-based vs calendar day/time of day)
    /// - Event storage structure (simple queue vs AI master event system)
    /// - Integration with engine-specific systems
    /// </remarks>
    public abstract class BaseDelayScheduler : IDelayScheduler
    {
        protected readonly List<DelayedAction> _delayedActions;

        protected BaseDelayScheduler()
        {
            _delayedActions = new List<DelayedAction>();
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
        public abstract void ScheduleDelay(float delaySeconds, IAction action, IEntity target);

        /// <summary>
        /// Updates the scheduler and fires any due actions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        public abstract void Update(float deltaTime);

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

