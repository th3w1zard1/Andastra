using System;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Entities
{
    /// <summary>
    /// Legacy time manager implementation. Use engine-specific implementations instead.
    /// </summary>
    /// <remarks>
    /// Time Manager (Legacy):
    /// - DEPRECATED: This class is maintained for backward compatibility only.
    /// - New code should use engine-specific implementations:
    ///   - OdysseyTimeManager (Runtime.Games.Odyssey) for KOTOR 1/2
    ///   - AuroraTimeManager (Runtime.Games.Aurora) for NWN/NWN2
    ///   - EclipseTimeManager (Runtime.Games.Eclipse) for Dragon Age/
    ///   - InfinityTimeManager (Runtime.Games.Infinity) for  games
    /// - This class throws NotSupportedException since Core cannot depend on Games.
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) time management system
    /// - Located via string references: "TIME_PAUSETIME" @ 0x007bdf88 (pause time constant), "TIME_PAUSEDAY" @ 0x007bdf98 (pause day constant), "TIME_MILLISECOND" @ 0x007bdfa8 (millisecond constant)
    /// - Time unit constants: "TIME_SECOND" @ 0x007bdfbc (second constant), "TIME_MINUTE" @ 0x007bdfc8 (minute constant), "TIME_HOUR" @ 0x007bdfd4 (hour constant)
    /// - "TIME_DAY" @ 0x007bdfe0 (day constant), "TIME_MONTH" @ 0x007bdfec (month constant), "TIME_YEAR" @ 0x007bdff8 (year constant)
    /// - Game time fields: "TIMEPLAYED" @ 0x007be1c4 (time played field in save game), "TIMESTAMP" @ 0x007be19c (timestamp field), "TimeElapsed" @ 0x007bed5c (time elapsed field)
    /// - "Mod_PauseTime" @ 0x007be89c (module pause time field), "GameTime" @ 0x007c1a78 (game time field), "GameTimeScale" @ 0x007c1a80 (game time scaling factor)
    /// - Frame timing: "frameStart" @ 0x007ba698 (frame start marker), "frameEnd" @ 0x007ba668 (frame end marker)
    /// - Animation timing: "transtime" @ 0x007bb0c4 (transition time field), "combinetime" @ 0x007ba36c (combine time field)
    /// - "combinetimekey" @ 0x007ba35c (combine time key field), "combinetimebezierkey" @ 0x007ba344 (combine time bezier key field)
    /// - Timer fields: "Timer" @ 0x007bfacc (generic timer field), "PauseTimer" @ 0x007bfad4 (pause timer field), "ActionTimer" @ 0x007bf820 (action timer field)
    /// - "BleedTimer" @ 0x007bfaa4 (bleed damage timer), "ExpireTime" @ 0x007c032c (expiration time field), "HeartbeatTime" @ 0x007c0c30 (heartbeat timer)
    /// - "LastHrtbtTime" @ 0x007c1370 (last heartbeat time), "LastSpawnTime" @ 0x007c0c10 (last spawn time), "ResetTime" @ 0x007c0cec (reset timer)
    /// - Spell timing: "CastTime" @ 0x007c32e8 (spell casting time), "ConjTime" @ 0x007c3354 (conjure time), "CatchTime" @ 0x007c3234 (catch time)
    /// - "AnimationTime" @ 0x007bf810 (animation timing), "FadeTime" @ 0x007c60ec (fade duration), "TimePerHP" @ 0x007bf234 (time per HP regen)
    /// - "FPRegenTime" @ 0x007bf524 (Force point regen time), "JNL_Time" @ 0x007bf254 (journal time field)
    /// - Combat timing: "Times" @ 0x007c10d4 (times field), "0@Force Timeout, executing a forced move to point." @ 0x007c0506 (force timeout error)
    /// - Audio timing: "EAX3 modulation time" @ 0x007c5f8c, "EAX3 echo time" @ 0x007c5fb4, "EAX decay time" @ 0x007c6020 (EAX audio timing fields)
    /// - GUI: "BTN_FILTER_TIME" @ 0x007ca974 (time filter button), "LBL_TIMEPLAYED" @ 0x007ced78 (time played label)
    /// - Event: "EVENT_TIMED_EVENT" @ 0x007bce20 (timed event constant)
    /// - Windows API: "timeGetTime" @ 0x0080a8c4 (high-resolution timer), "GetLocalTime" @ 0x0080b050 (get local system time)
    /// - "GetSystemTimeAsFileTime" @ 0x0080b0b4 (get system time as file time), "SystemTimeToFileTime" @ 0x0080b038 (convert system time to file time)
    /// - "FileTimeToSystemTime" @ 0x0080b112 (convert file time to system time), "FileTimeToLocalFileTime" @ 0x0080b12a (convert to local file time)
    /// - "GetTimeZoneInformation" @ 0x0080b268 (get timezone information), "CompareFileTime" @ 0x0080b0f0 (compare file times)
    /// - "LC_TIME" @ 0x007d40d8 (locale time constant)
    /// - Error: "Exceeded limits:Time limit" @ 0x007be438 (time limit exceeded error)
    /// - Combat timer errors:
    ///   - "CSWSCombatRound::IncrementTimer - %s Timer is negative at %d; Ending combat round and resetting" @ 0x007bfbc8 (negative timer error)
    ///   - "CSWSCombatRound::IncrementTimer - %s Master IS found (%x) and round has expired (%d %d); Resetting" @ 0x007bfc28 (combat round expiration)
    ///   - "CSWSCombatRound::IncrementTimer - %s Master cannot be found and round has expired; Resetting" @ 0x007bfc90 (master not found expiration)
    ///   - "CSWSCombatRound::DecrementPauseTimer - %s Master cannot be found expire the round; Resetting" @ 0x007bfcf0 (pause timer master error)
    /// - Original implementation: Fixed timestep simulation for deterministic gameplay (60 Hz = 1/60s = 0.01667s per tick)
    /// - Game time tracking: Day, month, year, time of day (hours, minutes, seconds) stored in module (IFO) time fields
    /// - TIMEPLAYED: Total seconds played, stored in save game metadata (NFO.res TIMEPLAYED field)
    /// - Fixed timestep ensures consistent simulation regardless of frame rate (physics, combat, scripts)
    /// - Time scale allows for pause (TimeScale = 0), slow-motion (TimeScale < 1), fast-forward (TimeScale > 1) effects
    /// - Original engine uses 60 Hz fixed timestep for game logic, variable timestep for rendering
    /// - Frame timing: frameStart and frameEnd mark frame boundaries for timing measurement
    /// - Timer system: Various timers track durations for effects, animations, combat rounds, spawn delays, etc.
    /// </remarks>
    /// <summary>
    /// Legacy time manager. Wraps OdysseyTimeManager for backward compatibility.
    /// </summary>
    [System.Obsolete("Use engine-specific time manager implementations (OdysseyTimeManager, AuroraTimeManager, etc.) instead.")]
    public class TimeManager : ITimeManager
    {
        public TimeManager()
        {
            throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead.");
        }

        public float FixedTimestep
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public float SimulationTime
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public float RealTime
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public float TimeScale
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
            set { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public bool IsPaused
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
            set { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public float DeltaTime
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public float InterpolationAlpha
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public int GameTimeHour
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public int GameTimeMinute
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public int GameTimeSecond
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public int GameTimeMillisecond
        {
            get { throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead."); }
        }

        public void SetGameTime(int hour, int minute, int second, int millisecond)
        {
            throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead.");
        }

        public void Update(float realDeltaTime)
        {
            throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead.");
        }

        public bool HasPendingTicks()
        {
            throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead.");
        }

        public void Tick()
        {
            throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead.");
        }

        /// <summary>
        /// Resets all time values.
        /// </summary>
        public void Reset()
        {
            throw new NotSupportedException("TimeManager is obsolete. Use engine-specific time manager implementations instead.");
        }
    }
}

