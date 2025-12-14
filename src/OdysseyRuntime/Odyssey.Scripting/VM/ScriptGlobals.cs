using System;
using System.Collections.Generic;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Scripting.VM
{
    /// <summary>
    /// Implementation of script globals and local variables.
    /// </summary>
    /// <remarks>
    /// Script Globals System:
    /// - Based on swkotor2.exe script variable system
    /// - Located via string references: Global variables stored in GLOBALVARS.res GFF file (see SaveSerializer)
    /// - Original implementation: Global variables persist across saves, local variables are per-entity
    /// - Global variables: Case-insensitive string keys, typed values (int, bool, string, location)
    /// - Local variables: Stored per entity (by ObjectId), accessed via GetLocalInt/SetLocalInt NWScript functions
    /// - Variable storage: Dictionary-based storage matching original engine's variable access patterns
    /// - Save system uses reflection to access private dictionaries (_globalInts, _globalBools, _globalStrings) for serialization
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
    }
}

