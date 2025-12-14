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
    /// - Located via string references: Time functions handle game time, day/night cycles, time-based events
    /// - Original implementation: Fixed timestep simulation for deterministic gameplay (60 Hz)
    /// - Game time tracking: Day, month, year, time of day (hours, minutes, seconds)
    /// - Fixed timestep ensures consistent simulation regardless of frame rate
    /// - Time scale allows for pause, slow-motion, fast-forward effects
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

