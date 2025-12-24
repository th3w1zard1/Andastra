namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Scheduler for delayed actions (DelayCommand).
    /// </summary>
    /// <remarks>
    /// Delay Scheduler Interface:
    /// - Common interface for scheduling delayed actions across all BioWare engines
    /// - DelayCommand NWScript function schedules actions to execute after specified delay
    /// - Uses priority queue sorted by execution time to efficiently process delayed actions
    /// - Delayed actions execute in order based on schedule time
    /// - STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
    /// - Actions are queued to target entity's action queue when delay expires
    /// - NCS VM: STORE_STATE opcode stores execution context for delayed execution
    /// - DelayCommand implementation: Stores action + delay time, executes when delay expires
    /// 
    /// Engine-specific implementations:
    /// - Odyssey: OdysseyDelayScheduler (swkotor.exe, swkotor2.exe) - float-based time
    /// - Aurora: AuroraDelayScheduler (nwmain.exe) - calendar day/time of day system
    /// - Eclipse: EclipseDelayScheduler (daorigins.exe, DragonAge2.exe) - float-based time
    /// - Infinity: InfinityDelayScheduler (, ) - float-based time
    /// 
    /// Base implementation: BaseDelayScheduler (Runtime.Games.Common) provides common functionality.
    /// </remarks>
    public interface IDelayScheduler
    {
        /// <summary>
        /// Schedules an action to execute after a delay.
        /// </summary>
        void ScheduleDelay(float delaySeconds, IAction action, IEntity target);

        /// <summary>
        /// Updates the scheduler and fires any due actions.
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Clears all delayed actions for a specific entity.
        /// </summary>
        void ClearForEntity(IEntity entity);

        /// <summary>
        /// Clears all delayed actions.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// The number of pending delayed actions.
        /// </summary>
        int PendingCount { get; }
    }
}

