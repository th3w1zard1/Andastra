using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for script event hooks.
    /// </summary>
    public interface IScriptHooksComponent : IComponent
    {
        /// <summary>
        /// Gets the script resref for an event.
        /// </summary>
        string GetScript(ScriptEvent eventType);

        /// <summary>
        /// Sets the script resref for an event.
        /// </summary>
        void SetScript(ScriptEvent eventType, string resRef);

        /// <summary>
        /// Gets a local integer variable.
        /// </summary>
        int GetLocalInt(string name);

        /// <summary>
        /// Sets a local integer variable.
        /// </summary>
        void SetLocalInt(string name, int value);

        /// <summary>
        /// Gets a local float variable.
        /// </summary>
        float GetLocalFloat(string name);

        /// <summary>
        /// Sets a local float variable.
        /// </summary>
        void SetLocalFloat(string name, float value);

        /// <summary>
        /// Gets a local string variable.
        /// </summary>
        string GetLocalString(string name);

        /// <summary>
        /// Sets a local string variable.
        /// </summary>
        void SetLocalString(string name, string value);
    }
}

