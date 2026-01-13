using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Common.Actions
{
    /// <summary>
    /// Base class for all actions shared across BioWare engines that use GFF-based action serialization.
    /// </summary>
    /// <remarks>
    /// Base Action Implementation:
    /// Common action system shared across Odyssey (swkotor.exe, swkotor2.exe) and Aurora (nwmain.exe, nwn2main.exe).
    /// 
    /// Common structure across engines:
    /// - ActionId (uint32): Action type identifier stored in GFF ActionList
    /// - GroupActionId (int16): Group ID for batching/clearing related actions together
    /// - NumParams (int16): Number of parameters (0-13 max parameters)
    /// - Paramaters array: Type/Value pairs stored in GFF structure
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - ActionList GFF field: List of actions stored in entity GFF structures
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action status: InProgress (continue), Complete (done), Failed (abort)
    /// 
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): OdysseyAction - specific function addresses for GFF loading/saving
    /// - Aurora (nwmain.exe, nwn2main.exe): AuroraAction - CNWSObject::LoadActionQueue/SaveActionQueue methods
    /// - Eclipse (daorigins.exe, DragonAge2.exe, ): Uses ActionFramework (different architecture)
    /// - Infinity (, ): May use different system (needs investigation)
    /// 
    /// All engine-specific details (function addresses, GFF field offsets, implementation specifics) are in subclasses.
    /// This base class contains only functionality that is identical across engines using GFF-based action serialization.
    /// </remarks>
    public abstract class BaseAction : IAction
    {
        protected float ElapsedTime;

        protected BaseAction(ActionType type)
        {
            Type = type;
            GroupId = -1;
        }

        /// <summary>
        /// The type of this action.
        /// </summary>
        public ActionType Type { get; }

        /// <summary>
        /// Group ID for clearing related actions.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// The entity that owns this action.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Updates the action and returns its status.
        /// </summary>
        public ActionStatus Update(IEntity actor, float deltaTime)
        {
            ElapsedTime += deltaTime;
            return ExecuteInternal(actor, deltaTime);
        }

        /// <summary>
        /// Executes the action logic. Override in derived classes.
        /// </summary>
        protected abstract ActionStatus ExecuteInternal(IEntity actor, float deltaTime);

        /// <summary>
        /// Called when the action is cancelled or completed.
        /// </summary>
        public virtual void Dispose()
        {
            // Override in derived classes if cleanup is needed
        }
    }
}

