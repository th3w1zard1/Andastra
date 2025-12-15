using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Scheduler for delayed actions using a priority queue.
    /// </summary>
    /// <remarks>
    /// Delay Scheduler:
    /// - Based on swkotor2.exe DelayCommand system
    /// - Located via string references: "DelayCommand" @ 0x007be900
    /// - Original implementation: DelayCommand NWScript function schedules actions to execute after specified delay
    /// - Delay timing: Uses game simulation time (_currentTime) to track when actions should execute
    /// - Priority queue: Uses sorted list by execution time to efficiently process delayed actions in order
    /// - Delayed actions: Execute in order based on schedule time (ascending by executeTime)
    /// - STORE_STATE opcode: In NCS VM stores stack/local state for DelayCommand semantics (restores state when action executes)
    /// - Action execution: Actions are queued to target entity's action queue when delay expires
    /// - Entity validation: Checks if target entity is still valid before executing delayed action
    /// </remarks>
    public class DelayScheduler : IDelayScheduler
    {
        private readonly List<DelayedAction> _delayedActions;
        private float _currentTime;

        public DelayScheduler()
        {
            _delayedActions = new List<DelayedAction>();
        }

        public int PendingCount { get { return _delayedActions.Count; } }

        public void ScheduleDelay(float delaySeconds, IAction action, IEntity target)
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

            // Insert in sorted order (ascending by execute time)
            int index = 0;
            while (index < _delayedActions.Count && _delayedActions[index].ExecuteTime <= executeTime)
            {
                index++;
            }
            _delayedActions.Insert(index, delayed);
        }

        public void Update(float deltaTime)
        {
            _currentTime += deltaTime;

            // Process all actions that are due
            while (_delayedActions.Count > 0 && _delayedActions[0].ExecuteTime <= _currentTime)
            {
                DelayedAction delayed = _delayedActions[0];
                _delayedActions.RemoveAt(0);

                if (delayed.Target.IsValid)
                {
                    IActionQueue actionQueue = delayed.Target.GetComponent<IActionQueue>();
                    if (actionQueue != null)
                    {
                        actionQueue.Add(delayed.Action);
                    }
                    else
                    {
                        // Execute immediately if no action queue
                        delayed.Action.Owner = delayed.Target;
                        delayed.Action.Update(delayed.Target, 0);
                        delayed.Action.Dispose();
                    }
                }
                else
                {
                    delayed.Action.Dispose();
                }
            }
        }

        public void ClearForEntity(IEntity entity)
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

        public void ClearAll()
        {
            foreach (DelayedAction delayed in _delayedActions)
            {
                delayed.Action.Dispose();
            }
            _delayedActions.Clear();
        }

        /// <summary>
        /// Resets the current time (for testing).
        /// </summary>
        public void Reset()
        {
            ClearAll();
            _currentTime = 0;
        }

        private struct DelayedAction
        {
            public float ExecuteTime;
            public IAction Action;
            public IEntity Target;
        }
    }
}

