using Andastra.Runtime.Core.Actions;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for entities that have an action queue.
    /// </summary>
    /// <remarks>
    /// Action Queue Component Interface:
    /// Common action queue component system shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity).
    /// 
    /// Common structure across all engines:
    /// - Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Update processes current action, returns number of script instructions executed
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// 
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): Uses GFF-based action serialization (ActionList field)
    /// - Aurora (nwmain.exe, nwn2main.exe): Uses GFF-based action serialization (ActionList field, CNWSObject methods)
    /// - Eclipse (daorigins.exe, DragonAge2.exe, ): Uses ActionFramework (different architecture)
    /// - Infinity (, ): May use different system (needs investigation)
    /// 
    /// All engine-specific details (function addresses, serialization formats, implementation specifics) are in engine-specific implementations.
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

        /// <summary>
        /// Gets the number of script instructions executed during the last update.
        /// </summary>
        /// <returns>The instruction count from the last update, or 0 if no scripts were executed.</returns>
        /// <remarks>
        /// Instruction Count Tracking:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script budget system
        /// - Located via string references: Script execution budget limits per frame
        /// - Original implementation: Tracks instruction count to enforce per-frame script budget limits
        /// - Used by game loop to prevent script lockups (MaxScriptBudget constant)
        /// - Instruction count accumulates from all script executions (ExecuteScript, heartbeats, etc.)
        /// - Reset each frame before processing action queues
        /// </remarks>
        int GetLastInstructionCount();

        /// <summary>
        /// Resets the instruction count accumulator for the current frame.
        /// </summary>
        /// <remarks>
        /// Called by game loop at the start of each frame to reset instruction tracking.
        /// </remarks>
        void ResetInstructionCount();

        /// <summary>
        /// Adds instruction count from script execution to the accumulator.
        /// </summary>
        /// <param name="count">The number of instructions executed.</param>
        /// <remarks>
        /// Instruction Count Accumulation:
        /// - Called when scripts execute (ExecuteScript, heartbeats, etc.) to track instruction usage
        /// - Accumulates instruction count per frame for budget enforcement
        /// </remarks>
        void AddInstructionCount(int count);
    }
}

