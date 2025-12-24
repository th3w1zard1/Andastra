using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Module
{
    /// <summary>
    /// Runtime module implementation.
    /// Represents a game module (collection of areas and global state).
    /// </summary>
    /// <remarks>
    /// Module System:
    /// - Based on swkotor2.exe module system
    /// - Located via string references: "Module" @ 0x007bc4e0, "ModuleName" @ 0x007bde2c, "ModuleLoaded" @ 0x007bdd70
    /// - "MODULE" @ 0x007beab8, "ModuleList" @ 0x007bdd3c, "GetModuleList" @ 0x007bdd48
    /// - "ModuleRunning" @ 0x007bdd58, "LinkedToModule" @ 0x007bd7bc
    /// - "Mod_Tag" @ 0x007be720, "Mod_Entry_Area" @ 0x007be9b4, "Mod_StartMovie" (module entry area and start movie)
    /// - "Mod_Entry_X" @ 0x007be998, "Mod_Entry_Y" @ 0x007be98c, "Mod_Entry_Z" @ 0x007be980, "Mod_Entry_Dir" @ 0x007be974 (entry position/direction)
    /// - "Mod_OnClientEntrance" @ 0x007be718, "Mod_OnHeartbeat" (module script hooks)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// - "MODULES:" @ 0x007b58b4, ":MODULES" @ 0x007be258, "MODULES" @ 0x007c6bc4 (module directory paths)
    /// - ".\modules" @ 0x007c6bcc, "d:\modules" @ 0x007c6bd8, ":modules" @ 0x007cc0d8 (module directory variants)
    /// - "LIVE%d:MODULES\" @ 0x007be680 (live module directory format)
    /// - "LASTMODULE" @ 0x007be1d0 (last loaded module reference)
    /// - "modulesave" @ 0x007bde20 (module save reference), "module000" @ 0x007cb9cc (default module name)
    /// - "Module: %s" @ 0x007c79c8 (module debug message)
    /// - GUI: "LB_MODULES" @ 0x007cbff4 (module list box), "PlayModuleCharacterList" @ 0x007c2428
    /// - Server messages: "ServerStatus.ModuleList " @ 0x007c2fb8, ":: Module savegame list: %s.\n" @ 0x007cbbb4
    /// - ":: Server module list: " @ 0x007cbc2c, ":: Server mode: Module Running.\n" @ 0x007cbc44
    /// - ":: Server mode: Module Loaded.\n" @ 0x007cbc68
    /// - Windows API: GetModuleHandleA, GetModuleFileNameA (module handle/file name functions)
    /// - IFO file format: GFF with "IFO " signature containing module metadata
    /// - Module loading: FUN_00708990 @ 0x00708990 (loads module, sets up areas, spawns entities)
    /// - FUN_00633270 @ 0x00633270 sets up all game directories including MODULES
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
            AreaList = new List<string>();
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

        /// <summary>
        /// Loading screen image resource reference (from IFO LoadScreenResRef field).
        /// </summary>
        public string LoadScreenResRef { get; set; }

        /// <summary>
        /// Ordered list of area ResRefs from Mod_Area_list in the module IFO file.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Mod_Area_list contains ordered list of area ResRefs.
        /// Each entry in Mod_Area_list contains an Area_Name field with the area ResRef.
        /// This list is used for resolving transition targets by index (TransPendNextID).
        /// Stored during module loading to avoid re-reading the IFO file.
        /// </remarks>
        public List<string> AreaList { get; set; }

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
        /// Gets the number of days in the specified month for the specified year.
        /// </summary>
        /// <param name="month">Month number (1-12).</param>
        /// <param name="year">Year number.</param>
        /// <returns>Number of days in the month (28-31).</returns>
        /// <remarks>
        /// Based on swkotor2.exe calendar system.
        /// Uses standard calendar with variable days per month:
        /// - Months 1, 3, 5, 7, 8, 10, 12: 31 days
        /// - Months 4, 6, 9, 11: 30 days
        /// - Month 2: 28 days (no leap year support in game calendar)
        /// </remarks>
        private int GetDaysInMonth(int month, int year)
        {
            // Validate month range
            if (month < 1 || month > 12)
            {
                return 30; // Default fallback
            }

            // Days per month lookup table (1-indexed: [0] unused, [1-12] used)
            // Standard calendar: Jan=31, Feb=28, Mar=31, Apr=30, May=31, Jun=30,
            //                   Jul=31, Aug=31, Sep=30, Oct=31, Nov=30, Dec=31
            int[] daysPerMonth = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

            return daysPerMonth[month];
        }

        /// <summary>
        /// Advances time by the specified minutes.
        /// Handles day/month/year overflow correctly with variable days per month.
        /// </summary>
        /// <param name="minutes">Number of minutes to advance.</param>
        /// <remarks>
        /// Based on swkotor2.exe time advancement system.
        /// Properly handles:
        /// - Day overflow into next month (respects days per month)
        /// - Month overflow into next year (12 months per year)
        /// - Year increment on year overflow
        /// </remarks>
        public void AdvanceTime(int minutes)
        {
            MinutesPastMidnight += minutes;

            // Handle day overflow (1440 minutes = 24 hours)
            while (MinutesPastMidnight >= 1440)
            {
                MinutesPastMidnight -= 1440;
                Day++;

                // Get days in current month
                int daysInCurrentMonth = GetDaysInMonth(Month, Year);

                // Handle day overflow into next month
                if (Day > daysInCurrentMonth)
                {
                    Day = 1;
                    Month++;

                    // Handle month overflow into next year
                    if (Month > 12)
                    {
                        Month = 1;
                        Year++;

                        // Year overflow protection (prevent integer overflow)
                        // Game calendar uses years like 3951, so this should never happen in practice
                        // but we add protection for safety
                        if (Year < 0)
                        {
                            Year = 1; // Reset to year 1 if overflow occurs
                        }
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

