using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Legacy delay scheduler implementation for backward compatibility.
    /// </summary>
    /// <remarks>
    /// This class is maintained for backward compatibility with existing code.
    /// New code should use engine-specific implementations:
    /// - BaseDelayScheduler (Andastra.Game.Games.Common) - Single implementation for all engines
    ///   - Engine-specific delay scheduler classes have been merged into BaseDelayScheduler
    /// - InfinityDelayScheduler for  (Runtime.Games.Infinity)
    /// 
    /// This implementation uses Odyssey-style delay scheduling (float-based time).
    /// Based on swkotor.exe and swkotor2.exe DelayCommand system.
    /// </remarks>
    public class DelayScheduler : IDelayScheduler
    {
        private readonly List<DelayedAction> _delayedActions;
        private float _currentTime;

        /// <summary>
        /// Initializes a new instance of the DelayScheduler.
        /// </summary>
        public DelayScheduler()
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

        /// <summary>
        /// Updates the scheduler and fires any due actions.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
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

        /// <summary>
        /// Clears all delayed actions for a specific entity.
        /// </summary>
        /// <param name="entity">Entity to clear delayed actions for.</param>
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

        /// <summary>
        /// Clears all delayed actions.
        /// </summary>
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

