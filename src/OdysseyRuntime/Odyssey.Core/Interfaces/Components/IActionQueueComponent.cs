using Odyssey.Core.Actions;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that have an action queue.
    /// </summary>
    /// <remarks>
    /// Action Queue Component Interface:
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0
    /// - Original implementation: Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Update processes current action, returns number of script instructions executed
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// </remarks>
    public interface IActionQueueComponent : IComponent
    {
        /// <summary>
        /// Gets the current action being executed.
        /// </summary>
        IAction CurrentAction { get; }

        /// <summary>
        /// Gets the number of queued actions.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Adds an action to the queue.
        /// </summary>
        void Add(IAction action);

        /// <summary>
        /// Clears all queued actions.
        /// </summary>
        void Clear();

        /// <summary>
        /// Updates the action queue.
        /// </summary>
        void Update(IEntity entity, float deltaTime);
    }
}

