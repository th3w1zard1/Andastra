using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for script event hooks.
    /// </summary>
    /// <remarks>
    /// Script Hooks Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe script event system
    /// - Located via string references: "ScriptHeartbeat" @ 0x007bee60, "ScriptOnNotice" @ 0x007bee70,
    ///   "ScriptAttacked" @ 0x007bee80, "ScriptDamaged" @ 0x007bee90, "ScriptDeath" @ 0x007beea0
    /// - Script events: Stored as script ResRef strings in GFF structures (UTC, UTD, UTP, etc.)
    /// - GetScript/SetScript: Manages script ResRefs for event types (OnHeartbeat, OnAttacked, etc.)
    /// - Local variables: Per-entity local variables (int, float, string) stored in GFF LocalVars structure
    /// - Local variables persist in save games and are accessible via NWScript GetLocal* functions
    /// - Script hooks executed by event bus when game events occur (combat, damage, dialogue, etc.)
    /// - Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 (save script hooks), FUN_0050c510 @ 0x0050c510 (load script hooks)
    /// </remarks>
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

