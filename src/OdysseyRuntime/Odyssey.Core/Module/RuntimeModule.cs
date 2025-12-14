using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Module
{
    /// <summary>
    /// Runtime module implementation.
    /// Represents a game module (collection of areas and global state).
    /// </summary>
    /// <remarks>
    /// Module System:
    /// - Based on swkotor2.exe module system
    /// - Located via string references: "Mod_" prefix for module fields (Mod_Tag, Mod_Entry_Area, Mod_StartMovie, etc.)
    /// - IFO file format: GFF with "IFO " signature containing module metadata
    /// - Module loading: FUN_00708990 @ 0x00708990 (loads module, sets up areas, spawns entities)
    /// - Original implementation stores module data in IFO file, references areas by ResRef
    /// 
    /// Module Loading Sequence:
    /// 1. Read IFO - Parse module metadata (IFO file with "IFO " signature)
    /// 2. Check Requirements - Verify Expansion_Pack and MinGameVer
    /// 3. Load HAKs - Mount HAK files in order
    /// 4. Play Movie - Show Mod_StartMovie if set
    /// 5. Load Entry Area - Read ARE + GIT for Mod_Entry_Area
    /// 6. Spawn Player - Place at Entry position/direction (Mod_Entry_X, Mod_Entry_Y, Mod_Entry_Z, Mod_Entry_Dir)
    /// 7. Fire OnModLoad - Execute module load script (Mod_OnClientEntrance script)
    /// 8. Fire OnModStart - Execute module start script (Mod_OnHeartbeat script)
    /// 9. Start Gameplay - Enable player control
    /// 
    /// Based on IFO file format documentation in vendor/PyKotor/wiki/GFF-IFO.md.
    /// </remarks>
    public class RuntimeModule : IModule
    {
        private readonly Dictionary<string, RuntimeArea> _areas;
        private readonly Dictionary<ScriptEvent, string> _scripts;

        public RuntimeModule()
        {
            _areas = new Dictionary<string, RuntimeArea>(StringComparer.OrdinalIgnoreCase);
            _scripts = new Dictionary<ScriptEvent, string>();

            // Default values
            ResRef = string.Empty;
            DisplayName = string.Empty;
            Tag = string.Empty;
            EntryArea = string.Empty;
            EntryPosition = Vector3.Zero;
            EntryDirectionX = 0f;
            EntryDirectionY = 1f;
            DawnHour = 6;
            DuskHour = 18;
            MinutesPastMidnight = 720; // Noon
            Day = 1;
            Month = 1;
            Year = 3951; // Default KotOR2 year
            XPScale = 100;
            StartMovie = string.Empty;
        }

        #region IModule Implementation

        public string ResRef { get; set; }
        public string DisplayName { get; set; }
        public string Tag { get; set; }
        public string EntryArea { get; set; }

        public IEnumerable<IArea> Areas
        {
            get { return _areas.Values; }
        }

        public IArea GetArea(string resRef)
        {
            RuntimeArea area;
            if (_areas.TryGetValue(resRef, out area))
            {
                return area;
            }
            return null;
        }

        public string GetScript(ScriptEvent eventType)
        {
            string script;
            if (_scripts.TryGetValue(eventType, out script))
            {
                return script;
            }
            return string.Empty;
        }

        public int DawnHour { get; set; }
        public int DuskHour { get; set; }
        public int MinutesPastMidnight { get; set; }
        public int Day { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }

        #endregion

        #region Extended Properties

        /// <summary>
        /// Entry position coordinates.
        /// </summary>
        public Vector3 EntryPosition { get; set; }

        /// <summary>
        /// Entry direction X component.
        /// </summary>
        public float EntryDirectionX { get; set; }

        /// <summary>
        /// Entry direction Y component.
        /// </summary>
        public float EntryDirectionY { get; set; }

        /// <summary>
        /// Computed entry facing angle in radians.
        /// </summary>
        public float EntryFacing
        {
            get { return (float)Math.Atan2(EntryDirectionY, EntryDirectionX); }
        }

        /// <summary>
        /// Experience point multiplier (0-200, 100 = normal).
        /// </summary>
        public int XPScale { get; set; }

        /// <summary>
        /// Starting movie file (BIK).
        /// </summary>
        public string StartMovie { get; set; }

        /// <summary>
        /// Voice-over folder name.
        /// </summary>
        public string VoiceOverId { get; set; }

        /// <summary>
        /// Module unique identifier (GUID).
        /// </summary>
        public byte[] ModuleId { get; set; }

        /// <summary>
        /// Required expansion pack bitfield.
        /// </summary>
        public int ExpansionPack { get; set; }

        /// <summary>
        /// Minimum game version required.
        /// </summary>
        public string MinGameVersion { get; set; }

        /// <summary>
        /// HAK files required (semicolon-separated).
        /// </summary>
        public string HakFiles { get; set; }

        /// <summary>
        /// Whether to cache compiled scripts.
        /// </summary>
        public bool CacheNSSData { get; set; }

        #endregion

        #region Area Management

        /// <summary>
        /// Adds an area to the module.
        /// </summary>
        public void AddArea(RuntimeArea area)
        {
            if (area == null)
            {
                throw new ArgumentNullException("area");
            }
            _areas[area.ResRef] = area;
        }

        /// <summary>
        /// Removes an area from the module.
        /// </summary>
        public bool RemoveArea(string resRef)
        {
            return _areas.Remove(resRef);
        }

        /// <summary>
        /// Gets all area ResRefs in this module.
        /// </summary>
        public IEnumerable<string> GetAreaResRefs()
        {
            return _areas.Keys;
        }

        #endregion

        #region Script Management

        /// <summary>
        /// Sets a script for a module event.
        /// </summary>
        public void SetScript(ScriptEvent eventType, string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                _scripts.Remove(eventType);
            }
            else
            {
                _scripts[eventType] = scriptResRef;
            }
        }

        /// <summary>
        /// Gets all registered script events.
        /// </summary>
        public IEnumerable<ScriptEvent> GetScriptEvents()
        {
            return _scripts.Keys;
        }

        #endregion

        #region Time Management

        /// <summary>
        /// Gets the current hour (0-23).
        /// </summary>
        public int GetCurrentHour()
        {
            return MinutesPastMidnight / 60;
        }

        /// <summary>
        /// Gets the current minute (0-59).
        /// </summary>
        public int GetCurrentMinute()
        {
            return MinutesPastMidnight % 60;
        }

        /// <summary>
        /// Sets the current time.
        /// </summary>
        public void SetTime(int hour, int minute)
        {
            hour = Math.Max(0, Math.Min(23, hour));
            minute = Math.Max(0, Math.Min(59, minute));
            MinutesPastMidnight = hour * 60 + minute;
        }

        /// <summary>
        /// Advances time by the specified minutes.
        /// </summary>
        public void AdvanceTime(int minutes)
        {
            MinutesPastMidnight += minutes;
            while (MinutesPastMidnight >= 1440) // 24 * 60
            {
                MinutesPastMidnight -= 1440;
                Day++;
                // Handle month/year overflow (simplified - assume 30 days/month, 12 months/year)
                if (Day > 30)
                {
                    Day = 1;
                    Month++;
                    if (Month > 12)
                    {
                        Month = 1;
                        Year++;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if it's currently daytime.
        /// </summary>
        public bool IsDay()
        {
            int hour = GetCurrentHour();
            return hour >= DawnHour && hour < DuskHour;
        }

        /// <summary>
        /// Checks if it's currently nighttime.
        /// </summary>
        public bool IsNight()
        {
            return !IsDay();
        }

        #endregion
    }
}

