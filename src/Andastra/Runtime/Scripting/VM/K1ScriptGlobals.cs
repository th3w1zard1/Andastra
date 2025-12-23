using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Scripting.VM
{
    /// <summary>
    /// KOTOR 1-specific implementation of script globals and local variables.
    /// </summary>
    /// <remarks>
    /// K1 Script Globals System:
    /// - Based on swkotor.exe script variable system
    /// - Located via string references: "GLOBALVARS" (save file global variables GFF field name)
    /// - "Global" (global constant), "GLOBAL" (global constant uppercase)
    /// - "RIMS:GLOBAL" (global RIM directory path), "globalcat" (global catalog field)
    /// - "FactionGlobal" (faction global variable field)
    /// - Global variable save/load: Saves GLOBALVARS to save game GFF file
    /// - Original implementation: Global variables persist across saves, local variables are per-entity
    /// - Global variables: Case-insensitive string keys, typed values (int, bool, string, location)
    /// - Global variable storage: Stored in save file GFF structure with "GLOBALVARS" field name
    /// - Local variables: Stored per entity (by ObjectId), accessed via GetLocalInt/SetLocalInt NWScript functions
    /// - Local variable storage: Stored in entity's ScriptHooksComponent or per-entity dictionary
    /// - Variable storage: Dictionary-based storage matching original engine's variable access patterns
    /// - OBJECT_SELF = 0x7F000001 (constant object ID for current script owner)
    /// - OBJECT_INVALID = 0x7F000000 (constant object ID for invalid/empty object references)
    /// - Variable types: int (32-bit signed), bool (32-bit, 0 = false, non-zero = true), string (null-terminated), location (struct with position/orientation)
    /// - Variable access: Case-insensitive key lookup (original engine uses case-insensitive variable names)
    /// - Default values: Unset variables return default values (0 for int, false for bool, empty string for string, null for location)
    /// - K1-specific: Initializes with KOTOR 1 default global variable values if needed
    /// </remarks>
    public class K1ScriptGlobals : ScriptGlobals
    {
        /// <summary>
        /// Initializes a new instance of K1ScriptGlobals for KOTOR 1.
        /// </summary>
        /// <remarks>
        /// K1 Script Globals Initialization:
        /// - Based on swkotor.exe: Script globals system initializes global variables at game start
        /// - Original implementation: Global variables initialized from GLOBALVARS.res if present, otherwise empty
        /// - K1-specific initialization can be added here if needed (e.g., default story flags, quest states)
        /// </remarks>
        public K1ScriptGlobals()
            : base()
        {
            // K1-specific initialization can be added here if needed
            // For now, base class initialization is sufficient
        }
    }
}
