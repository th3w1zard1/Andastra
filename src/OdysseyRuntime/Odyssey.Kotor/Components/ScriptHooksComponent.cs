using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for storing script event hooks.
    /// </summary>
    /// <remarks>
    /// Script Hooks Component:
    /// - Based on swkotor2.exe script event system
    /// - Located via string references: "ScriptHeartbeat" @ 0x007beeb0, "ScriptOnNotice" @ 0x007beea0
    /// - "ScriptAttacked" @ 0x007bee80, "ScriptDamaged" @ 0x007bee70, "ScriptDeath" @ 0x007bee20
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 (save script hooks for creatures)
    /// - FUN_00585ec0 @ 0x00585ec0 (save script hooks for placeables)
    /// - FUN_00584f40 @ 0x00584f40 (save script hooks for doors)
    /// - Maps script events to script resource references (ResRef strings)
    /// - Scripts are executed by NCS VM when events fire (OnHeartbeat, OnPerception, OnAttacked, etc.)
    /// - Script ResRefs stored in GFF structures (e.g., ScriptHeartbeat, ScriptOnNotice fields)
    /// - Local variables (int, float, string) stored per-entity for script execution context
    /// - Local variables accessed via GetLocalInt/GetLocalFloat/GetLocalString NWScript functions
    /// </remarks>
    public class ScriptHooksComponent : IComponent, IScriptHooksComponent
    {
        private readonly Dictionary<ScriptEvent, string> _scripts;
        private readonly Dictionary<string, int> _localInts;
        private readonly Dictionary<string, float> _localFloats;
        private readonly Dictionary<string, string> _localStrings;

        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public ScriptHooksComponent()
        {
            _scripts = new Dictionary<ScriptEvent, string>();
            _localInts = new Dictionary<string, int>();
            _localFloats = new Dictionary<string, float>();
            _localStrings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the script ResRef for an event.
        /// </summary>
        public string GetScript(ScriptEvent evt)
        {
            string script;
            if (_scripts.TryGetValue(evt, out script))
            {
                return script;
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets the script ResRef for an event.
        /// </summary>
        public void SetScript(ScriptEvent evt, string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                _scripts.Remove(evt);
            }
            else
            {
                _scripts[evt] = scriptResRef;
            }
        }

        /// <summary>
        /// Checks if an event has a script assigned.
        /// </summary>
        public bool HasScript(ScriptEvent evt)
        {
            return _scripts.ContainsKey(evt) && !string.IsNullOrEmpty(_scripts[evt]);
        }

        /// <summary>
        /// Gets all registered script events.
        /// </summary>
        public IEnumerable<ScriptEvent> GetScriptEvents()
        {
            return _scripts.Keys;
        }

        /// <summary>
        /// Removes a script hook.
        /// </summary>
        public void RemoveScript(ScriptEvent evt)
        {
            _scripts.Remove(evt);
        }

        /// <summary>
        /// Clears all script hooks.
        /// </summary>
        public void Clear()
        {
            _scripts.Clear();
        }

        /// <summary>
        /// Gets a local integer variable.
        /// </summary>
        public int GetLocalInt(string name)
        {
            int value;
            if (_localInts.TryGetValue(name, out value))
            {
                return value;
            }
            return 0;
        }

        /// <summary>
        /// Sets a local integer variable.
        /// </summary>
        public void SetLocalInt(string name, int value)
        {
            _localInts[name] = value;
        }

        /// <summary>
        /// Gets a local float variable.
        /// </summary>
        public float GetLocalFloat(string name)
        {
            float value;
            if (_localFloats.TryGetValue(name, out value))
            {
                return value;
            }
            return 0f;
        }

        /// <summary>
        /// Sets a local float variable.
        /// </summary>
        public void SetLocalFloat(string name, float value)
        {
            _localFloats[name] = value;
        }

        /// <summary>
        /// Gets a local string variable.
        /// </summary>
        public string GetLocalString(string name)
        {
            string value;
            if (_localStrings.TryGetValue(name, out value))
            {
                return value;
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets a local string variable.
        /// </summary>
        public void SetLocalString(string name, string value)
        {
            _localStrings[name] = value ?? string.Empty;
        }
    }
}
