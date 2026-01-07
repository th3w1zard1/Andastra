using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Odyssey
{
    /// <summary>
    /// Odyssey engine delay scheduler implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Delay Scheduler:
    /// - Based on swkotor.exe and swkotor2.exe DelayCommand system
    /// - Located via string references: "DelayCommand" @ 0x007be900 (swkotor2.exe: NWScript DelayCommand function)
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
    public class OdysseyDelayScheduler : BaseDelayScheduler
    {
        private float _currentTime;

        /// <summary>
        /// Initializes a new instance of the OdysseyDelayScheduler.
        /// </summary>
        public OdysseyDelayScheduler()
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
            // Based on swkotor.exe and swkotor2.exe: DelayCommand scheduler implementation
            // Located via string references: "DelayCommand" @ 0x007be900 (swkotor2.exe)
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

