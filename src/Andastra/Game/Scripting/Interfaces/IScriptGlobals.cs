using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Scripting.Interfaces
{
    /// <summary>
    /// Persistent script state - global and local variables.
    /// </summary>
    /// <remarks>
    /// Script Globals Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script variable system
    /// - Located via string references: "GLOBALVARS" @ 0x007c27bc (save file global variables GFF field name)
    /// - "Global" @ 0x007c29b0 (global constant), "GLOBAL" @ 0x007c7550 (global constant uppercase)
    /// - "RIMS:GLOBAL" @ 0x007c7544 (global RIM directory path), "globalcat" @ 0x007bddd0 (global catalog field)
    /// - "FactionGlobal" @ 0x007c28e0 (faction global variable field)
    /// - Global variable save/load: 0x005ac670 @ 0x005ac670 saves GLOBALVARS to save game GFF file
    /// - Original implementation: Global variables persist across saves, local variables are per-entity
    /// - Global variables: Case-insensitive string keys, typed values (int, bool, string, location)
    /// - Global variable storage: Stored in save file GFF structure with "GLOBALVARS" field name
    /// - Local variables: Stored per entity (by ObjectId), accessed via GetLocalInt/SetLocalInt NWScript functions
    /// - Local variable storage: Stored in entity's ScriptHooksComponent or per-entity dictionary
    /// - Variable types: int (32-bit signed), bool (32-bit, 0 = false, non-zero = true), string (null-terminated), location (struct with position/orientation)
    /// - Variable access: Case-insensitive key lookup (original engine uses case-insensitive variable names)
    /// - Default values: Unset variables return default values (0 for int, false for bool, empty string for string, null for location)
    /// - Based on NWScript variable system from vendor/PyKotor/wiki/
    /// </remarks>
    public interface IScriptGlobals
    {
        // Global integers
        int GetGlobalInt(string name);
        void SetGlobalInt(string name, int value);

        // Global booleans
        bool GetGlobalBool(string name);
        void SetGlobalBool(string name, bool value);

        // Global strings
        string GetGlobalString(string name);
        void SetGlobalString(string name, string value);

        // Global locations
        object GetGlobalLocation(string name);
        void SetGlobalLocation(string name, object value);

        // Local integers (per object)
        int GetLocalInt(IEntity entity, string name);
        void SetLocalInt(IEntity entity, string name, int value);

        // Local floats (per object)
        float GetLocalFloat(IEntity entity, string name);
        void SetLocalFloat(IEntity entity, string name, float value);

        // Local strings (per object)
        string GetLocalString(IEntity entity, string name);
        void SetLocalString(IEntity entity, string name, string value);

        // Local objects (per object)
        IEntity GetLocalObject(IEntity entity, string name);
        void SetLocalObject(IEntity entity, string name, IEntity value);

        // Local locations (per object)
        object GetLocalLocation(IEntity entity, string name);
        void SetLocalLocation(IEntity entity, string name, object value);

        /// <summary>
        /// Clears all local variables for an entity.
        /// </summary>
        void ClearLocals(IEntity entity);

        /// <summary>
        /// Clears all global variables.
        /// </summary>
        void ClearGlobals();

        /// <summary>
        /// Enumerates all local integer variable names for an entity.
        /// </summary>
        /// <param name="entity">Entity to enumerate local variables for.</param>
        /// <returns>Collection of variable names.</returns>
        IEnumerable<string> EnumerateLocalInts(IEntity entity);

        /// <summary>
        /// Enumerates all global integer variable names.
        /// </summary>
        /// <returns>Collection of variable names.</returns>
        IEnumerable<string> EnumerateGlobalInts();
    }
}

