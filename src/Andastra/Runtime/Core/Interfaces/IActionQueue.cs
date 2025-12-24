using System.Collections.Generic;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Action queue for an entity.
    /// </summary>
    /// <remarks>
    /// Action Queue Interface:
    /// Common action queue system shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity).
    /// 
    /// Common structure across all engines:
    /// - FIFO queue: Actions processed sequentially, current action executes until complete
    /// - Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// 
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Uses GFF-based action serialization (ActionList field)
    /// - Aurora (nwmain.exe, nwn2main.exe): Uses GFF-based action serialization (ActionList field, CNWSObject methods)
    /// - Eclipse (daorigins.exe, DragonAge2.exe, ): Uses ActionFramework (different architecture)
    /// - Infinity (, ): May use different system (needs investigation)
    /// 
    /// All engine-specific details (function addresses, serialization formats, implementation specifics) are in engine-specific base classes.
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

