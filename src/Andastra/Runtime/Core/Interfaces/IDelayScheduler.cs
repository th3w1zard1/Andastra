namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Scheduler for delayed actions (DelayCommand).
    /// </summary>
    /// <remarks>
    /// Delay Scheduler Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe DelayCommand system
    /// - Located via string references: "DelayCommand" @ 0x007be900 (NWScript DelayCommand function)
    /// - Delay-related fields: "Delay" @ 0x007c35b0 (delay field), "DelayReply" @ 0x007c38f0 (delay reply field)
    /// - "DelayEntry" @ 0x007c38fc (delay entry field), "FadeDelay" @ 0x007c358c (fade delay field)
    /// - "DestroyObjectDelay" @ 0x007c0248 (destroy object delay field), "FadeDelayOnDeath" @ 0x007bf55c (fade delay on death)
    /// - "ReaxnDelay" @ 0x007bf94c (reaction delay field), "MusicDelay" @ 0x007c14b4 (music delay field)
    /// - "ShakeDelay" @ 0x007c49ec (shake delay field), "TooltipDelay Sec" @ 0x007c71dc (tooltip delay)
    /// - Original implementation: DelayCommand NWScript function schedules actions to execute after specified delay
    /// - Uses priority queue sorted by execution time to efficiently process delayed actions
    /// - Delayed actions execute in order based on schedule time
    /// - STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
    /// - Actions are queued to target entity's action queue when delay expires
    /// - NCS VM: STORE_STATE opcode @ offset 0x0D+ in NCS file stores execution context for delayed execution
    /// - DelayCommand implementation: Stores action + delay time, executes when delay expires
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

