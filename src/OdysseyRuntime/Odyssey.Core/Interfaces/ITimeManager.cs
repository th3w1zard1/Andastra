namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Manages simulation and render time for deterministic gameplay.
    /// </summary>
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
    }
}

