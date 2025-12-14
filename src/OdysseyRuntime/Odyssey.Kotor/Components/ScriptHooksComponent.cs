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

        public ScriptHooksComponent()
        {
            _scripts = new Dictionary<ScriptEvent, string>();
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

        // IScriptHooksComponent implementation using generic ResRef
        CSharpKOTOR.Common.ResRef IScriptHooksComponent.GetScript(ScriptEvent evt)
        {
            string script;
            if (_scripts.TryGetValue(evt, out script) && !string.IsNullOrEmpty(script))
            {
                return CSharpKOTOR.Common.ResRef.FromString(script);
            }
            return CSharpKOTOR.Common.ResRef.FromBlank();
        }
    }
}
