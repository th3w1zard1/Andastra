using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Base interface for all actions that can be queued on entities.
    /// </summary>
    /// <remarks>
    /// Action Interface:
    /// Common action system shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity).
    /// 
    /// Common structure across all engines:
    /// - ActionId (uint32): Action type identifier
    /// - GroupActionId (int16): Group ID for batching/clearing related actions together
    /// - Actions are executed by entities, return status (Complete, InProgress, Failed)
    /// - Actions update each frame until they complete or fail
    /// - Action types defined in ActionType enum (Move, Attack, UseObject, SpeakString, etc.)
    /// 
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Uses GFF-based action serialization (ActionList field)
    /// - Aurora (nwmain.exe, nwn2main.exe): Uses GFF-based action serialization (ActionList field, CNWSObject methods)
    /// - Eclipse (daorigins.exe, DragonAge2.exe, MassEffect.exe): Uses ActionFramework (different architecture)
    /// - Infinity (MassEffect.exe, MassEffect2.exe): May use different system (needs investigation)
    /// 
    /// All engine-specific details (function addresses, serialization formats, implementation specifics) are in engine-specific base classes.
    /// </remarks>
    public interface IAction
    {
        /// <summary>
        /// The type of this action.
        /// </summary>
        ActionType Type { get; }

        /// <summary>
        /// Group ID for clearing related actions.
        /// </summary>
        int GroupId { get; set; }

        /// <summary>
        /// The entity that owns this action.
        /// </summary>
        IEntity Owner { get; set; }

        /// <summary>
        /// Updates the action and returns its status.
        /// </summary>
        ActionStatus Update(IEntity actor, float deltaTime);

        /// <summary>
        /// Called when the action is cancelled or completed.
        /// </summary>
        void Dispose();
    }
}

