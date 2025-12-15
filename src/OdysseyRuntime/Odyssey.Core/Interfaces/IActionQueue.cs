using System.Collections.Generic;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Action queue for an entity.
    /// </summary>
    /// <remarks>
    /// Action Queue Interface:
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0, "ActionType" @ 0x007bf7f8
    /// - Original implementation: Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// - Action parameters stored in ActionParam1-5, ActionParamStrA/B fields
    /// </remarks>
    public interface IActionQueue : IComponent
    {
        /// <summary>
        /// Adds an action to the end of the queue.
        /// </summary>
        void Add(IAction action);
        
        /// <summary>
        /// Adds an action to the front of the queue.
        /// </summary>
        void AddFront(IAction action);
        
        /// <summary>
        /// Clears all actions from the queue.
        /// </summary>
        void Clear();
        
        /// <summary>
        /// Clears all actions with the specified group ID.
        /// </summary>
        void ClearByGroupId(int groupId);
        
        /// <summary>
        /// The currently executing action.
        /// </summary>
        IAction Current { get; }
        
        /// <summary>
        /// Whether there are any actions in the queue.
        /// </summary>
        bool HasActions { get; }
        
        /// <summary>
        /// The number of actions in the queue.
        /// </summary>
        int Count { get; }
        
        /// <summary>
        /// Processes the current action.
        /// Returns the number of script instructions executed.
        /// </summary>
        int Process(float deltaTime);
        
        /// <summary>
        /// Gets all actions in the queue.
        /// </summary>
        IEnumerable<IAction> GetAllActions();
    }
}

