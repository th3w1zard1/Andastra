using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Game.Scripting.VM
{
    /// <summary>
    /// Implementation of script globals and local variables.
    /// </summary>
    /// <remarks>
    /// Script Globals System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script variable system
    /// - Located via string references: "GLOBALVARS" @ 0x007c27bc (save file global variables GFF field name)
    /// - "Global" @ 0x007c29b0 (global constant), "GLOBAL" @ 0x007c7550 (global constant uppercase)
    /// - "RIMS:GLOBAL" @ 0x007c7544 (global RIM directory path), "globalcat" @ 0x007bddd0 (global catalog field)
    /// - "FactionGlobal" @ 0x007c28e0 (faction global variable field), "useglobalalpha" @ 0x007b6f20 (use global alpha flag)
    /// - Global variable save/load: FUN_005ac670 @ 0x005ac670 saves GLOBALVARS to save game GFF file
    /// - Original implementation: Global variables persist across saves, local variables are per-entity
    /// - Global variables: Case-insensitive string keys, typed values (int, bool, string, location)
    /// - Global variable storage: Stored in save file GFF structure with "GLOBALVARS" field name
    /// - Local variables: Stored per entity (by ObjectId), accessed via GetLocalInt/SetLocalInt NWScript functions
    /// - Local variable storage: Stored in entity's ScriptHooksComponent or per-entity dictionary
    /// - Variable storage: Dictionary-based storage matching original engine's variable access patterns
    /// - Save system uses reflection to access private dictionaries (_globalInts, _globalBools, _globalStrings, _globalLocations) for serialization
    /// - OBJECT_SELF = 0x7F000001 (constant object ID for current script owner)
    /// - OBJECT_INVALID = 0x7F000000 (constant object ID for invalid/empty object references)
    /// - Variable types: int (32-bit signed), bool (32-bit, 0 = false, non-zero = true), string (null-terminated), location (struct with position/orientation)
    /// - Variable access: Case-insensitive key lookup (original engine uses case-insensitive variable names)
    /// - Default values: Unset variables return default values (0 for int, false for bool, empty string for string, null for location)
    /// </remarks>
    public class ScriptGlobals : IScriptGlobals
    {
        private readonly Dictionary<string, int> _globalInts;
        private readonly Dictionary<string, bool> _globalBools;
        private readonly Dictionary<string, string> _globalStrings;
        private readonly Dictionary<string, object> _globalLocations;

        private readonly Dictionary<uint, Dictionary<string, int>> _localInts;
        private readonly Dictionary<uint, Dictionary<string, float>> _localFloats;
        private readonly Dictionary<uint, Dictionary<string, string>> _localStrings;
        private readonly Dictionary<uint, Dictionary<string, uint>> _localObjects;
        private readonly Dictionary<uint, Dictionary<string, object>> _localLocations;

        public ScriptGlobals()
        {
            _globalInts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _globalBools = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _globalStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _globalLocations = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            _localInts = new Dictionary<uint, Dictionary<string, int>>();
            _localFloats = new Dictionary<uint, Dictionary<string, float>>();
            _localStrings = new Dictionary<uint, Dictionary<string, string>>();
            _localObjects = new Dictionary<uint, Dictionary<string, uint>>();
            _localLocations = new Dictionary<uint, Dictionary<string, object>>();
        }

        #region Global Variables

        public int GetGlobalInt(string name)
        {
            if (_globalInts.TryGetValue(name, out int value))
            {
                return value;
            }
            return 0;
        }

        public void SetGlobalInt(string name, int value)
        {
            _globalInts[name] = value;
        }

        public bool GetGlobalBool(string name)
        {
            if (_globalBools.TryGetValue(name, out bool value))
            {
                return value;
            }
            return false;
        }

        public void SetGlobalBool(string name, bool value)
        {
            _globalBools[name] = value;
        }

        public string GetGlobalString(string name)
        {
            if (_globalStrings.TryGetValue(name, out string value))
            {
                return value;
            }
            return string.Empty;
        }

        public void SetGlobalString(string name, string value)
        {
            _globalStrings[name] = value ?? string.Empty;
        }

        public object GetGlobalLocation(string name)
        {
            if (_globalLocations.TryGetValue(name, out object value))
            {
                return value;
            }
            return null;
        }

        public void SetGlobalLocation(string name, object value)
        {
            _globalLocations[name] = value;
        }

        #endregion

        #region Local Variables

        private Dictionary<string, T> GetOrCreateLocalDict<T>(Dictionary<uint, Dictionary<string, T>> storage, IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            if (!storage.TryGetValue(entity.ObjectId, out Dictionary<string, T> dict))
            {
                dict = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
                storage[entity.ObjectId] = dict;
            }
            return dict;
        }

        public int GetLocalInt(IEntity entity, string name)
        {
            if (entity == null)
            {
                return 0;
            }

            if (_localInts.TryGetValue(entity.ObjectId, out Dictionary<string, int> dict))
            {
                if (dict.TryGetValue(name, out int value))
                {
                    return value;
                }
            }
            return 0;
        }

        public void SetLocalInt(IEntity entity, string name, int value)
        {
            Dictionary<string, int> dict = GetOrCreateLocalDict(_localInts, entity);
            if (dict != null)
            {
                dict[name] = value;
            }
        }

        public float GetLocalFloat(IEntity entity, string name)
        {
            if (entity == null)
            {
                return 0f;
            }

            if (_localFloats.TryGetValue(entity.ObjectId, out Dictionary<string, float> dict))
            {
                if (dict.TryGetValue(name, out float value))
                {
                    return value;
                }
            }
            return 0f;
        }

        public void SetLocalFloat(IEntity entity, string name, float value)
        {
            Dictionary<string, float> dict = GetOrCreateLocalDict(_localFloats, entity);
            if (dict != null)
            {
                dict[name] = value;
            }
        }

        public string GetLocalString(IEntity entity, string name)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            if (_localStrings.TryGetValue(entity.ObjectId, out Dictionary<string, string> dict))
            {
                if (dict.TryGetValue(name, out string value))
                {
                    return value;
                }
            }
            return string.Empty;
        }

        public void SetLocalString(IEntity entity, string name, string value)
        {
            Dictionary<string, string> dict = GetOrCreateLocalDict(_localStrings, entity);
            if (dict != null)
            {
                dict[name] = value ?? string.Empty;
            }
        }

        public IEntity GetLocalObject(IEntity entity, string name)
        {
            if (entity == null || entity.World == null)
            {
                return null;
            }

            if (_localObjects.TryGetValue(entity.ObjectId, out Dictionary<string, uint> dict))
            {
                if (dict.TryGetValue(name, out uint objectId))
                {
                    return entity.World.GetEntity(objectId);
                }
            }
            return null;
        }

        public void SetLocalObject(IEntity entity, string name, IEntity value)
        {
            Dictionary<string, uint> dict = GetOrCreateLocalDict(_localObjects, entity);
            if (dict != null)
            {
                dict[name] = value?.ObjectId ?? 0x7F000000;
            }
        }

        public object GetLocalLocation(IEntity entity, string name)
        {
            if (entity == null)
            {
                return null;
            }

            if (_localLocations.TryGetValue(entity.ObjectId, out Dictionary<string, object> dict))
            {
                if (dict.TryGetValue(name, out object value))
                {
                    return value;
                }
            }
            return null;
        }

        public void SetLocalLocation(IEntity entity, string name, object value)
        {
            Dictionary<string, object> dict = GetOrCreateLocalDict(_localLocations, entity);
            if (dict != null)
            {
                dict[name] = value;
            }
        }

        #endregion

        public void ClearLocals(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            uint id = entity.ObjectId;
            _localInts.Remove(id);
            _localFloats.Remove(id);
            _localStrings.Remove(id);
            _localObjects.Remove(id);
            _localLocations.Remove(id);
        }

        public void ClearGlobals()
        {
            _globalInts.Clear();
            _globalBools.Clear();
            _globalStrings.Clear();
            _globalLocations.Clear();
        }

        public IEnumerable<string> EnumerateLocalInts(IEntity entity)
        {
            if (entity == null)
            {
                yield break;
            }

            if (_localInts.TryGetValue(entity.ObjectId, out Dictionary<string, int> dict))
            {
                foreach (string key in dict.Keys)
                {
                    yield return key;
                }
            }
        }

        public IEnumerable<string> EnumerateGlobalInts()
        {
            foreach (string key in _globalInts.Keys)
            {
                yield return key;
            }
        }
    }
}

