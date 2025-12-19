using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Base interface for all actions that can be queued on entities.
    /// </summary>
    /// <remarks>
    /// Action Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0, "ActionType" @ 0x007bf7f8
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF)
    /// - FUN_00505bc0 @ 0x00505bc0 (save ActionList to GFF)
    /// - Actions are executed by entities, return status (Complete, InProgress, Failed)
    /// - Actions update each frame until they complete or fail
    /// - Action types defined in ActionType enum (Move, Attack, UseObject, SpeakString, etc.)
    /// - Group IDs allow batching/clearing related actions together
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

