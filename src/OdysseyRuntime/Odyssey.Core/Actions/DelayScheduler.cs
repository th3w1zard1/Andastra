using System.Collections.Generic;
using BioWareEngines.Core.Interfaces;

namespace BioWareEngines.Core.Actions
{
    /// <summary>
    /// Scheduler for delayed actions using a priority queue.
    /// </summary>
    /// <remarks>
    /// Delay Scheduler:
    /// - Based on swkotor2.exe DelayCommand system
    /// - Located via string references: "DelayCommand" @ 0x007be900 (NWScript DelayCommand function)
    /// - Delay-related fields: "Delay" @ 0x007c35b0 (delay field), "DelayReply" @ 0x007c38f0 (delay reply field)
    /// - "DelayEntry" @ 0x007c38fc (delay entry field), "FadeDelay" @ 0x007c358c (fade delay field)
    /// - "DestroyObjectDelay" @ 0x007c0248 (destroy object delay field), "FadeDelayOnDeath" @ 0x007bf55c (fade delay on death)
    /// - "ReaxnDelay" @ 0x007bf94c (reaction delay field), "MusicDelay" @ 0x007c14b4 (music delay field)
    /// - "ShakeDelay" @ 0x007c49ec (shake delay field), "TooltipDelay Sec" @ 0x007c71dc (tooltip delay)
    /// - Animation delays: "controlptdelay" @ 0x007ba218, "controlptdelaykey" @ 0x007ba204, "controlptdelaybezierkey" @ 0x007ba1ec
    /// - "lightningDelay" @ 0x007ba508, "lightningDelaykey" @ 0x007ba4f4, "lightningDelaybezierkey" @ 0x007ba4dc
    /// - "=Lip Delay" @ 0x007c7fb7 (lip sync delay), "EAX2 reverb delay" @ 0x007c5fc4, "EAX2 reflections delay" @ 0x007c5fe4 (audio delays)
    /// - Original implementation: DelayCommand NWScript function schedules actions to execute after specified delay
    /// - Delay timing: Uses game simulation time (_currentTime) to track when actions should execute
    /// - Priority queue: Uses sorted list by execution time to efficiently process delayed actions in order
    /// - Delayed actions: Execute in order based on schedule time (ascending by executeTime)
    /// - STORE_STATE opcode: In NCS VM stores stack/local state for DelayCommand semantics (restores state when action executes)
    /// - Action execution: Actions are queued to target entity's action queue when delay expires
    /// - Entity validation: Checks if target entity is still valid before executing delayed action
    /// - DelayCommand(float fSeconds, action aActionToDelay): Schedules action to execute after fSeconds delay
    /// - AssignCommand(object oTarget, action aAction): Executes action immediately on target (different from DelayCommand)
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
            // Based on swkotor2.exe: DelayCommand scheduler implementation
            // Located via string references: "DelayCommand" @ 0x007be900
            // Original implementation: Processes delayed actions in order based on execution time
            // Uses game simulation time to track when actions should execute
            // STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
            _currentTime += deltaTime;

            // Process all actions that are due
            // Original engine: Actions execute in order (sorted by executeTime)
            // Actions are queued to target entity's action queue when delay expires
            while (_delayedActions.Count > 0 && _delayedActions[0].ExecuteTime <= _currentTime)
            {
                DelayedAction delayed = _delayedActions[0];
                _delayedActions.RemoveAt(0);

                if (delayed.Target.IsValid)
                {
                    IActionQueue actionQueue = delayed.Target.GetComponent<IActionQueue>();
                    if (actionQueue != null)
                    {
                        // Original engine: Action added to entity's action queue
                        actionQueue.Add(delayed.Action);
                    }
                    else
                    {
                        // Execute immediately if no action queue
                        // Original engine: If entity has no action queue, execute action directly
                        delayed.Action.Owner = delayed.Target;
                        delayed.Action.Update(delayed.Target, 0);
                        delayed.Action.Dispose();
                    }
                }
                else
                {
                    // Target entity invalid - dispose action
                    // Original engine: Delayed actions for invalid entities are discarded
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

