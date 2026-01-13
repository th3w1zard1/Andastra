using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of script hooks component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Script Hooks Component Implementation:
    /// - Common script hooks functionality shared across all engines
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (if any differences exist)
    /// - Cross-engine analysis shows common script hooks component patterns across all engines.
    ///   - Infinity: ,  (script hooks system similar, needs verification)
    ///
    /// Common functionality across all engines:
    /// - Script ResRef storage: Maps ScriptEvent enum to script resource reference strings
    /// - Local variables: Per-entity local variables (int, float, string) stored in dictionaries
    /// - Script execution: Scripts executed by NCS VM when events fire (OnHeartbeat, OnPerception, OnAttacked, etc.)
    /// - GFF serialization: Script ResRefs stored in GFF structures (e.g., ScriptHeartbeat, ScriptOnNotice fields)
    /// - Local variable persistence: Local variables persist in save games and are accessible via NWScript GetLocal* functions
    /// - Script execution context: Entity is caller (OBJECT_SELF), event triggerer is parameter
    ///
    /// Engine-specific differences (handled in entity serialization, not component):
    /// - GFF field names may vary slightly between engines
    /// - Serialization format details differ (handled by entity Serialize/Deserialize methods)
    /// - Script event types may vary (handled by ScriptEvent enum)
    /// </remarks>
    public class BaseScriptHooksComponent : IComponent, IScriptHooksComponent
    {
        private readonly Dictionary<ScriptEvent, string> _scripts;
        private readonly Dictionary<string, int> _localInts;
        private readonly Dictionary<string, float> _localFloats;
        private readonly Dictionary<string, string> _localStrings;

        public IEntity Owner { get; set; }

        public virtual void OnAttach() { }
        public virtual void OnDetach() { }

        public BaseScriptHooksComponent()
        {
            _scripts = new Dictionary<ScriptEvent, string>();
            _localInts = new Dictionary<string, int>();
            _localFloats = new Dictionary<string, float>();
            _localStrings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the script ResRef for an event.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns script ResRef string for the specified event type.
        /// Returns empty string if no script is assigned to the event.
        /// </remarks>
        public virtual string GetScript(ScriptEvent evt)
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
        /// <remarks>
        /// Common across all engines: Sets or removes script ResRef for the specified event type.
        /// Passing null or empty string removes the script hook.
        /// </remarks>
        public virtual void SetScript(ScriptEvent evt, string scriptResRef)
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
        /// <remarks>
        /// Common across all engines: Returns true if a non-empty script ResRef is assigned to the event.
        /// </remarks>
        public virtual bool HasScript(ScriptEvent evt)
        {
            return _scripts.ContainsKey(evt) && !string.IsNullOrEmpty(_scripts[evt]);
        }

        /// <summary>
        /// Gets all registered script events.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns all ScriptEvent values that have scripts assigned.
        /// </remarks>
        public virtual IEnumerable<ScriptEvent> GetScriptEvents()
        {
            return _scripts.Keys;
        }

        /// <summary>
        /// Removes a script hook.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Removes the script hook for the specified event type.
        /// </remarks>
        public virtual void RemoveScript(ScriptEvent evt)
        {
            _scripts.Remove(evt);
        }

        /// <summary>
        /// Clears all script hooks.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Removes all script hooks from the component.
        /// </remarks>
        public virtual void Clear()
        {
            _scripts.Clear();
        }

        /// <summary>
        /// Gets a local integer variable.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns the value of a local integer variable.
        /// Returns 0 if the variable doesn't exist.
        /// Based on NWScript GetLocalInt function behavior.
        /// </remarks>
        public virtual int GetLocalInt(string name)
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
        /// <remarks>
        /// Common across all engines: Sets the value of a local integer variable.
        /// Based on NWScript SetLocalInt function behavior.
        /// </remarks>
        public virtual void SetLocalInt(string name, int value)
        {
            _localInts[name] = value;
        }

        /// <summary>
        /// Gets a local float variable.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns the value of a local float variable.
        /// Returns 0.0f if the variable doesn't exist.
        /// Based on NWScript GetLocalFloat function behavior.
        /// </remarks>
        public virtual float GetLocalFloat(string name)
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
        /// <remarks>
        /// Common across all engines: Sets the value of a local float variable.
        /// Based on NWScript SetLocalFloat function behavior.
        /// </remarks>
        public virtual void SetLocalFloat(string name, float value)
        {
            _localFloats[name] = value;
        }

        /// <summary>
        /// Gets a local string variable.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Returns the value of a local string variable.
        /// Returns empty string if the variable doesn't exist.
        /// Based on NWScript GetLocalString function behavior.
        /// </remarks>
        public virtual string GetLocalString(string name)
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
        /// <remarks>
        /// Common across all engines: Sets the value of a local string variable.
        /// Based on NWScript SetLocalString function behavior.
        /// </remarks>
        public virtual void SetLocalString(string name, string value)
        {
            _localStrings[name] = value ?? string.Empty;
        }
    }
}

