using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Base interface for all actions that can be queued on entities.
    /// </summary>
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

