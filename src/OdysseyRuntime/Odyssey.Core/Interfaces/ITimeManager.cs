namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Manages simulation and render time for deterministic gameplay.
    /// </summary>
    /// <remarks>
    /// Time Manager Interface:
    /// - Based on swkotor2.exe time management system
    /// - Fixed timestep: Typically 1/60 second (60 Hz) for deterministic physics and gameplay
    /// - SimulationTime: Accumulated fixed timestep time (advances only during simulation ticks)
    /// - RealTime: Total elapsed real-world time (continuous)
    /// - TimeScale: Multiplier for time flow (1.0 = normal, 0.0 = paused, >1.0 = faster)
    /// - IsPaused: Pauses simulation (TimeScale = 0.0)
    /// - DeltaTime: Time delta for current frame (scaled by TimeScale)
    /// - InterpolationAlpha: Blending factor for smooth rendering between simulation frames (0.0 to 1.0)
    /// - Tick: Advances simulation by one fixed timestep
    /// - Update: Updates accumulator with real frame time (drives fixed timestep ticks)
    /// - HasPendingTicks: Returns true if accumulator has enough time for additional ticks
    /// - Fixed timestep prevents timing-dependent bugs and ensures deterministic gameplay
    /// - Time tracking: TIMEPLAYED @ 0x007be1c4 (time played field in save GFF), frameStart @ 0x007ba698, frameEnd @ 0x007ba668 (frame timing)
    /// </remarks>
    public interface ITimeManager
    {
        /// <summary>
        /// The fixed timestep for simulation updates (typically 1/60 second).
        /// </summary>
        float FixedTimestep { get; }

        /// <summary>
        /// The current simulation time in seconds.
        /// </summary>
        float SimulationTime { get; }

        /// <summary>
        /// The total elapsed real time in seconds.
        /// </summary>
        float RealTime { get; }

        /// <summary>
        /// The time scale multiplier (1.0 = normal, 0.0 = paused).
        /// </summary>
        float TimeScale { get; set; }

        /// <summary>
        /// Whether the game is currently paused.
        /// </summary>
        bool IsPaused { get; set; }

        /// <summary>
        /// The delta time for the current frame.
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// The interpolation factor for smooth rendering (0.0 to 1.0).
        /// </summary>
        float InterpolationAlpha { get; }

        /// <summary>
        /// Advances the simulation by the fixed timestep.
        /// </summary>
        void Tick();

        /// <summary>
        /// Updates the accumulator with frame time.
        /// </summary>
        void Update(float realDeltaTime);

        /// <summary>
        /// Returns true if there are pending simulation ticks to process.
        /// </summary>
        bool HasPendingTicks();

        /// <summary>
        /// Gets the current game time hour (0-23).
        /// </summary>
        int GameTimeHour { get; }

        /// <summary>
        /// Gets the current game time minute (0-59).
        /// </summary>
        int GameTimeMinute { get; }

        /// <summary>
        /// Gets the current game time second (0-59).
        /// </summary>
        int GameTimeSecond { get; }

        /// <summary>
        /// Gets the current game time millisecond (0-999).
        /// </summary>
        int GameTimeMillisecond { get; }

        /// <summary>
        /// Sets the game time.
        /// </summary>
        /// <param name="hour">Hour (0-23)</param>
        /// <param name="minute">Minute (0-59)</param>
        /// <param name="second">Second (0-59)</param>
        /// <param name="millisecond">Millisecond (0-999)</param>
        void SetGameTime(int hour, int minute, int second, int millisecond);
    }
}

