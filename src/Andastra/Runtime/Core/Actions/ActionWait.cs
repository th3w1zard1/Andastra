using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to wait for a duration.
    /// </summary>
    /// <remarks>
    /// Wait Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) ActionWait NWScript function
    /// - Located via string references: "ActionWait" @ 0x007be8e4, "Wait" action type
    /// - Action timing: Uses game simulation time (ITimeManager) for duration tracking
    /// - Original implementation: Entity waits (does nothing) for specified duration
    /// - Used for scripted delays, timing sequences, animation synchronization
    /// - Action completes after duration expires, allowing next action in queue to execute
    /// - Duration measured in game seconds (not real-time), respects game time scale/pause
    /// </remarks>
    public class ActionWait : ActionBase
    {
        private readonly float _duration;

        public ActionWait(float duration)
            : base(ActionType.Wait)
        {
            _duration = duration;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (ElapsedTime >= _duration)
            {
                return ActionStatus.Complete;
            }
            return ActionStatus.InProgress;
        }
    }
}

