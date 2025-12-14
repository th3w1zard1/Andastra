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
    /// Maps script events to script resource references.
    /// Scripts are executed by the VM when events fire.
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
