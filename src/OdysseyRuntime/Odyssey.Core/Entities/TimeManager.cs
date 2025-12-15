using System;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Entities
{
    /// <summary>
    /// Manages simulation and render time for deterministic gameplay.
    /// </summary>
    /// <remarks>
    /// Time Manager:
    /// - Based on swkotor2.exe time management system
    /// - Located via string references: "TIME_PAUSETIME" @ 0x007bdf88, "TIME_SECOND" @ 0x007bdfbc, "TIME_MINUTE" @ 0x007bdfc8,
    ///   "TIME_HOUR" @ 0x007bdfd4, "TIME_DAY" @ 0x007bdfe0, "TIME_MONTH" @ 0x007bdfec, "TIME_YEAR" @ 0x007bdff8,
    ///   "TIMEPLAYED" @ 0x007be1c4, "TimeElapsed" @ 0x007bed5c, "Mod_PauseTime" @ 0x007be89c
    /// - "GameTime" @ 0x007c1a78, "GameTimeScale" @ 0x007c1a80 (game time scaling)
    /// - "frameStart" @ 0x007ba698, "frameEnd" @ 0x007ba668 (frame timing)
    /// - Original implementation: Fixed timestep simulation for deterministic gameplay (60 Hz = 1/60s = 0.01667s per tick)
    /// - Game time tracking: Day, month, year, time of day (hours, minutes, seconds) stored in module (IFO) time fields
    /// - TIMEPLAYED: Total seconds played, stored in save game metadata (NFO.res)
    /// - Fixed timestep ensures consistent simulation regardless of frame rate (physics, combat, scripts)
    /// - Time scale allows for pause (TimeScale = 0), slow-motion (TimeScale < 1), fast-forward (TimeScale > 1) effects
    /// - Original engine uses 60 Hz fixed timestep for game logic, variable timestep for rendering
    /// </remarks>
    public class TimeManager : ITimeManager
    {
        private const float DefaultFixedTimestep = 1f / 60f;
        private const float MaxFrameTime = 0.25f;

        private float _accumulator;
        private float _simulationTime;
        private float _realTime;
        private float _deltaTime;

        public TimeManager()
        {
            FixedTimestep = DefaultFixedTimestep;
            TimeScale = 1.0f;
            IsPaused = false;
        }

        public float FixedTimestep { get; }
        public float SimulationTime { get { return _simulationTime; } }
        public float RealTime { get { return _realTime; } }
        public float TimeScale { get; set; }
        public bool IsPaused { get; set; }
        public float DeltaTime { get { return _deltaTime; } }
        public float InterpolationAlpha { get { return _accumulator / FixedTimestep; } }

        public void Update(float realDeltaTime)
        {
            _realTime += realDeltaTime;
            _deltaTime = Math.Min(realDeltaTime, MaxFrameTime);

            if (!IsPaused)
            {
                _accumulator += _deltaTime * TimeScale;
            }
        }

        public bool HasPendingTicks()
        {
            return _accumulator >= FixedTimestep;
        }

        public void Tick()
        {
            if (_accumulator >= FixedTimestep)
            {
                _simulationTime += FixedTimestep;
                _accumulator -= FixedTimestep;
            }
        }

        /// <summary>
        /// Resets all time values.
        /// </summary>
        public void Reset()
        {
            _accumulator = 0;
            _simulationTime = 0;
            _realTime = 0;
            _deltaTime = 0;
        }
    }
}

