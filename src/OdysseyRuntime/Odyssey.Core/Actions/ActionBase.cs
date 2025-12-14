using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Base class for all actions.
    /// </summary>
    /// <remarks>
    /// Action Base:
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0, "ActionType" @ 0x007bf7f8
    /// - Original implementation: Actions are executed by entities, return status (Complete, InProgress, Failed)
    /// - Actions update each frame until they complete or fail
    /// - Action types defined in ActionType enum (Move, Attack, UseObject, SpeakString, etc.)
    /// - Group IDs allow batching/clearing related actions together
    /// </remarks>
    public abstract class ActionBase : IAction
    {
        protected float ElapsedTime;

        protected ActionBase(ActionType type)
        {
            Type = type;
            GroupId = -1;
        }

        public ActionType Type { get; }
        public int GroupId { get; set; }
        public IEntity Owner { get; set; }

        public ActionStatus Update(IEntity actor, float deltaTime)
        {
            ElapsedTime += deltaTime;
            return ExecuteInternal(actor, deltaTime);
        }

        protected abstract ActionStatus ExecuteInternal(IEntity actor, float deltaTime);

        public virtual void Dispose()
        {
            // Override in derived classes if cleanup is needed
        }
    }
}

