using System;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of time management functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Time Manager Implementation:
    /// - Common time management functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyTimeManager, AuroraTimeManager, EclipseTimeManager, InfinityTimeManager)
    /// 
    /// Common Functionality (all engines):
    /// - Fixed timestep simulation: All engines use fixed timestep for deterministic gameplay (typically 60 Hz = 1/60s = 0.01667s per tick)
    /// - Simulation time: Accumulated fixed timestep time (advances only during simulation ticks)
    /// - Real time: Total elapsed real-world time (continuous)
    /// - Time scale: Multiplier for time flow (1.0 = normal, 0.0 = paused, >1.0 = faster)
    /// - Pause state: Pauses simulation (TimeScale = 0.0)
    /// - Delta time: Time delta for current frame (scaled by TimeScale)
    /// - Interpolation alpha: Blending factor for smooth rendering between simulation frames (0.0 to 1.0)
    /// - Game time tracking: Hours, minutes, seconds, milliseconds (all engines track game time)
    /// - Fixed timestep accumulator: Accumulates real frame time to drive fixed timestep ticks
    /// - Max frame time clamping: Prevents spiral of death from large frame time spikes
    /// 
    /// Common Patterns (Reverse Engineered):
    /// - All engines use fixed timestep for game logic (physics, combat, scripts) and variable timestep for rendering
    /// - All engines track game time (hours, minutes, seconds, milliseconds) that advances with simulation time
    /// - All engines support time scaling (pause, slow-motion, fast-forward) via TimeScale multiplier
    /// - All engines use accumulator pattern: Accumulate real frame time, then tick fixed timesteps until accumulator is depleted
    /// - All engines clamp maximum frame time to prevent simulation instability from large frame time spikes
    /// 
    /// Engine-Specific (implemented in subclasses):
    /// - Fixed timestep value: May differ slightly between engines (typically 60 Hz, but needs verification)
    /// - Game time storage: Different save game formats store game time differently (NFO for Odyssey, GAM for Aurora, etc.)
    /// - Time constants: Engine-specific string references and memory addresses for time-related constants
    /// - Frame timing markers: Engine-specific frame start/end markers for profiling
    /// - Timer systems: Engine-specific timer implementations (combat timers, effect timers, etc.)
    /// 
    /// Inheritance Structure:
    /// - BaseTimeManager (this class) - Common functionality only
    ///   - OdysseyTimeManager : BaseTimeManager (swkotor.exe, swkotor2.exe)
    ///   - AuroraTimeManager : BaseTimeManager (nwmain.exe, nwn2main.exe)
    ///   - EclipseTimeManager : BaseTimeManager (daorigins.exe, DragonAge2.exe, , )
    ///   - InfinityTimeManager : BaseTimeManager (.exe, .exe, .exe)
    /// 
    /// NOTE: This base class contains ONLY functionality verified as identical across ALL engines.
    /// All engine-specific function addresses, memory offsets, and implementation details are in subclasses.
    /// Cross-engine verified components  is required to verify commonality before moving code to base class.
    /// </remarks>
    public class BaseTimeManager : ITimeManager
    {
        /// <summary>
        /// Default fixed timestep (60 Hz = 1/60 second).
        /// </summary>
        /// <remarks>
        /// Common across all engines: 60 Hz fixed timestep for deterministic gameplay.
        /// Engine-specific subclasses may override if different.
        /// </remarks>
        protected const float DefaultFixedTimestep = 1f / 60f;

        /// <summary>
        /// Maximum frame time to prevent simulation instability.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Clamp frame time to prevent spiral of death.
        /// Engine-specific subclasses may override if different.
        /// </remarks>
        protected const float MaxFrameTime = 0.25f;

        /// <summary>
        /// Fixed timestep accumulator.
        /// </summary>
        protected float _accumulator;

        /// <summary>
        /// Current simulation time in seconds.
        /// </summary>
        protected float _simulationTime;

        /// <summary>
        /// Total elapsed real time in seconds.
        /// </summary>
        protected float _realTime;

        /// <summary>
        /// Delta time for current frame.
        /// </summary>
        protected float _deltaTime;

        /// <summary>
        /// Time scale multiplier (1.0 = normal, 0.0 = paused, >1.0 = faster).
        /// </summary>
        protected float _timeScale;

        /// <summary>
        /// Whether the game is currently paused.
        /// </summary>
        protected bool _isPaused;

        /// <summary>
        /// Game time hour (0-23).
        /// </summary>
        protected int _gameTimeHour;

        /// <summary>
        /// Game time minute (0-59).
        /// </summary>
        protected int _gameTimeMinute;

        /// <summary>
        /// Game time second (0-59).
        /// </summary>
        protected int _gameTimeSecond;

        /// <summary>
        /// Game time millisecond (0-999).
        /// </summary>
        protected int _gameTimeMillisecond;

        /// <summary>
        /// Accumulator for game time milliseconds.
        /// </summary>
        protected float _gameTimeAccumulator;

        /// <summary>
        /// Gets the fixed timestep for simulation updates.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Typically 1/60 second (60 Hz).
        /// Engine-specific subclasses may override to return different values if needed.
        /// </remarks>
        public virtual float FixedTimestep
        {
            get { return DefaultFixedTimestep; }
        }

        /// <summary>
        /// Gets the current simulation time in seconds.
        /// </summary>
        public float SimulationTime
        {
            get { return _simulationTime; }
        }

        /// <summary>
        /// Gets the total elapsed real time in seconds.
        /// </summary>
        public float RealTime
        {
            get { return _realTime; }
        }

        /// <summary>
        /// Gets or sets the time scale multiplier (1.0 = normal, 0.0 = paused, >1.0 = faster).
        /// </summary>
        public float TimeScale
        {
            get { return _timeScale; }
            set
            {
                _timeScale = value;
                // Automatically update pause state based on time scale
                _isPaused = (_timeScale == 0.0f);
            }
        }

        /// <summary>
        /// Gets or sets whether the game is currently paused.
        /// </summary>
        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                _isPaused = value;
                // Automatically update time scale based on pause state
                if (_isPaused)
                {
                    _timeScale = 0.0f;
                }
                else if (_timeScale == 0.0f)
                {
                    // If unpausing and time scale was 0, restore to normal speed
                    _timeScale = 1.0f;
                }
            }
        }

        /// <summary>
        /// Gets the delta time for the current frame.
        /// </summary>
        public float DeltaTime
        {
            get { return _deltaTime; }
        }

        /// <summary>
        /// Gets the interpolation factor for smooth rendering (0.0 to 1.0).
        /// </summary>
        public float InterpolationAlpha
        {
            get { return _accumulator / FixedTimestep; }
        }

        /// <summary>
        /// Gets the current game time hour (0-23).
        /// </summary>
        public int GameTimeHour
        {
            get { return _gameTimeHour; }
        }

        /// <summary>
        /// Gets the current game time minute (0-59).
        /// </summary>
        public int GameTimeMinute
        {
            get { return _gameTimeMinute; }
        }

        /// <summary>
        /// Gets the current game time second (0-59).
        /// </summary>
        public int GameTimeSecond
        {
            get { return _gameTimeSecond; }
        }

        /// <summary>
        /// Gets the current game time millisecond (0-999).
        /// </summary>
        public int GameTimeMillisecond
        {
            get { return _gameTimeMillisecond; }
        }

        /// <summary>
        /// Initializes a new instance of the BaseTimeManager class.
        /// </summary>
        public BaseTimeManager()
        {
            _timeScale = 1.0f;
            _isPaused = false;
            _accumulator = 0.0f;
            _simulationTime = 0.0f;
            _realTime = 0.0f;
            _deltaTime = 0.0f;

            // Initialize game time to midnight
            _gameTimeHour = 0;
            _gameTimeMinute = 0;
            _gameTimeSecond = 0;
            _gameTimeMillisecond = 0;
            _gameTimeAccumulator = 0.0f;
        }

        /// <summary>
        /// Updates the accumulator with frame time.
        /// </summary>
        /// <param name="realDeltaTime">The real frame time in seconds.</param>
        /// <remarks>
        /// Common across all engines: Accumulate real frame time, clamp to max frame time, apply time scale.
        /// Engine-specific subclasses may override to add engine-specific frame timing logic.
        /// </remarks>
        public virtual void Update(float realDeltaTime)
        {
            _realTime += realDeltaTime;
            _deltaTime = Math.Min(realDeltaTime, MaxFrameTime);

            if (!_isPaused)
            {
                _accumulator += _deltaTime * _timeScale;
            }
        }

        /// <summary>
        /// Returns true if there are pending simulation ticks to process.
        /// </summary>
        /// <returns>True if accumulator has enough time for at least one fixed timestep tick.</returns>
        /// <remarks>
        /// Common across all engines: Check if accumulator >= fixed timestep.
        /// </remarks>
        public virtual bool HasPendingTicks()
        {
            return _accumulator >= FixedTimestep;
        }

        /// <summary>
        /// Advances the simulation by the fixed timestep.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Advance simulation time, update accumulator, update game time.
        /// Engine-specific subclasses may override to add engine-specific tick logic.
        /// </remarks>
        public virtual void Tick()
        {
            if (_accumulator >= FixedTimestep)
            {
                _simulationTime += FixedTimestep;
                _accumulator -= FixedTimestep;

                // Update game time (advance milliseconds)
                // Common across all engines: Game time advances at 1:1 with simulation time
                _gameTimeAccumulator += FixedTimestep * 1000.0f; // Convert to milliseconds
                while (_gameTimeAccumulator >= 1.0f)
                {
                    int millisecondsToAdd = (int)_gameTimeAccumulator;
                    _gameTimeMillisecond += millisecondsToAdd;
                    _gameTimeAccumulator -= millisecondsToAdd;

                    if (_gameTimeMillisecond >= 1000)
                    {
                        _gameTimeMillisecond -= 1000;
                        _gameTimeSecond++;

                        if (_gameTimeSecond >= 60)
                        {
                            _gameTimeSecond -= 60;
                            _gameTimeMinute++;

                            if (_gameTimeMinute >= 60)
                            {
                                _gameTimeMinute -= 60;
                                _gameTimeHour++;

                                if (_gameTimeHour >= 24)
                                {
                                    _gameTimeHour -= 24;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets the game time.
        /// </summary>
        /// <param name="hour">Hour (0-23)</param>
        /// <param name="minute">Minute (0-59)</param>
        /// <param name="second">Second (0-59)</param>
        /// <param name="millisecond">Millisecond (0-999)</param>
        /// <remarks>
        /// Common across all engines: Clamp values to valid ranges and set game time.
        /// Engine-specific subclasses may override to add engine-specific game time setting logic (e.g., save to module IFO).
        /// </remarks>
        public virtual void SetGameTime(int hour, int minute, int second, int millisecond)
        {
            _gameTimeHour = Math.Max(0, Math.Min(23, hour));
            _gameTimeMinute = Math.Max(0, Math.Min(59, minute));
            _gameTimeSecond = Math.Max(0, Math.Min(59, second));
            _gameTimeMillisecond = Math.Max(0, Math.Min(999, millisecond));
            _gameTimeAccumulator = 0.0f;
        }

        /// <summary>
        /// Gets the current game time as day and milliseconds since day start.
        /// </summary>
        /// <param name="day">Output parameter for the current day number.</param>
        /// <param name="milliseconds">Output parameter for milliseconds since day start (0-86399999).</param>
        /// <remarks>
        /// Original engine behavior (swkotor2.exe: 0x004db710 @ 0x004db710):
        /// - If paused (offset +0x24 == 1), returns pause day/time from offsets +0x28 and +0x2c
        /// - Otherwise, calculates day and milliseconds from current time system state
        /// - Returns day (int) and milliseconds (uint) since day start
        /// 
        /// This implementation converts current game time components (hour, minute, second, millisecond)
        /// to total milliseconds since day start. Day is always 0 in this base implementation.
        /// Engine-specific subclasses may override to track day numbers.
        /// </remarks>
        public virtual void GetGameTimeDayAndMilliseconds(out int day, out uint milliseconds)
        {
            // Base implementation: Always day 0, calculate milliseconds from current time components
            day = 0;
            
            // Calculate total milliseconds since day start
            // Hour (0-23) * 3600000 + Minute (0-59) * 60000 + Second (0-59) * 1000 + Millisecond (0-999)
            milliseconds = (uint)(_gameTimeHour * 3600000 + _gameTimeMinute * 60000 + _gameTimeSecond * 1000 + _gameTimeMillisecond);
        }

        /// <summary>
        /// Converts milliseconds since day start to hour, minute, second, and millisecond components.
        /// </summary>
        /// <param name="totalMilliseconds">Total milliseconds since day start.</param>
        /// <param name="minutesPerHour">Number of minutes per hour (typically 60, but can vary by time scale).</param>
        /// <param name="hour">Output parameter for hour (0-23).</param>
        /// <param name="minute">Output parameter for minute (0-59).</param>
        /// <param name="second">Output parameter for second (0-59).</param>
        /// <param name="millisecond">Output parameter for millisecond (0-999).</param>
        /// <remarks>
        /// Original engine behavior (swkotor2.exe: 0x004db660 @ 0x004db660):
        /// - Line 9: millisecond = totalMilliseconds % 1000
        /// - Line 10: Calculate total seconds = totalMilliseconds / 1000
        /// - Line 11: second = (totalSeconds) % 60
        /// - Line 12: minute = (totalSeconds / 60) % minutesPerHour
        /// - Line 13: hour = (totalSeconds / 60) / minutesPerHour
        /// 
        /// This matches the original engine's time conversion algorithm exactly.
        /// </remarks>
        public static void ConvertMillisecondsToTimeComponents(uint totalMilliseconds, int minutesPerHour, out int hour, out int minute, out int second, out int millisecond)
        {
            // Extract millisecond component (0-999)
            millisecond = (int)(totalMilliseconds % 1000);
            
            // Calculate total seconds
            ulong totalSeconds = totalMilliseconds / 1000;
            
            // Extract second component (0-59)
            second = (int)(totalSeconds % 60);
            
            // Calculate total minutes
            ulong totalMinutes = totalSeconds / 60;
            
            // Extract minute component (0 to minutesPerHour-1)
            minute = (int)(totalMinutes % (ulong)minutesPerHour);
            
            // Extract hour component
            hour = (int)(totalMinutes / (ulong)minutesPerHour);
            
            // Clamp hour to valid range (0-23) to match game day cycle
            if (hour >= 24)
            {
                hour = hour % 24;
            }
        }

        /// <summary>
        /// Gets pause day and pause time.
        /// </summary>
        /// <param name="pauseDay">Output parameter for the day when paused.</param>
        /// <param name="pauseTime">Output parameter for the time when paused (milliseconds since day start).</param>
        /// <remarks>
        /// Original engine behavior (swkotor2.exe: 0x00500290 @ 0x00500290 lines 88-90):
        /// - Gets pause day from time system object offset +0x28
        /// - Gets pause time from time system object offset +0x2c
        /// 
        /// Base implementation returns 0 for both values. Engine-specific subclasses should override
        /// to track pause state when game is paused.
        /// </remarks>
        public virtual void GetPauseDayAndTime(out uint pauseDay, out uint pauseTime)
        {
            // Base implementation: No pause tracking
            pauseDay = 0;
            pauseTime = 0;
        }

        /// <summary>
        /// Resets all time values.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Reset all time tracking to zero.
        /// </remarks>
        public virtual void Reset()
        {
            _accumulator = 0.0f;
            _simulationTime = 0.0f;
            _realTime = 0.0f;
            _deltaTime = 0.0f;
            _gameTimeHour = 0;
            _gameTimeMinute = 0;
            _gameTimeSecond = 0;
            _gameTimeMillisecond = 0;
            _gameTimeAccumulator = 0.0f;
        }
    }
}

