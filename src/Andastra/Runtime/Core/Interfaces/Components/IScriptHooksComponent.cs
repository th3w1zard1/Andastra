using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for script event hooks.
    /// </summary>
    /// <remarks>
    /// Script Hooks Component Interface:
    /// - Common interface for script hooks functionality across all BioWare engines
    /// - Base implementation: BaseScriptHooksComponent in Runtime.Games.Common.Components
    /// - Engine-specific implementations:
    ///   - Odyssey: ScriptHooksComponent (inherits from base, no differences)
    ///   - Aurora: Uses BaseScriptHooksComponent directly
    ///   - Eclipse: EclipseScriptHooksComponent (inherits from base, no differences)
    ///   - Infinity: InfinityScriptHooksComponent (inherits from base, no differences)
    /// - Cross-engine analysis completed via Ghidra reverse engineering:
    ///   - Odyssey: swkotor.exe, swkotor2.exe
    ///     - swkotor.exe: 0x004ebf20, 0x00500610, 0x0058e660, 0x0058da80 (script hooks save/load)
    ///     - swkotor2.exe: 0x005226d0 @ 0x005226d0 (save script hooks for creatures), 0x00585ec0 @ 0x00585ec0 (save script hooks for placeables), 0x00584f40 @ 0x00584f40 (save script hooks for doors), 0x0050c510 @ 0x0050c510 (load script hooks from UTC template)
    ///     - String references: "ScriptHeartbeat" @ 0x007beeb0, "ScriptOnNotice" @ 0x007beea0, "ScriptAttacked" @ 0x007bee80
    ///   - Aurora: nwmain.exe
    ///     - SaveCreature @ 0x1403a0a60, LoadFromTemplate @ 0x140501c90, SaveTrigger @ 0x140504290
    ///     - String references: "ScriptHeartbeat" @ 0x140dddb10, "ScriptOnNotice" @ 0x140dddb20, "ScriptAttacked" @ 0x140dddb40
    ///   - Eclipse: daorigins.exe, DragonAge2.exe (script hooks system similar, needs verification)
    ///   - Infinity: ,  (script hooks system similar, needs verification)
    /// - Script events: Stored as script ResRef strings in GFF structures (UTC, UTD, UTP, etc.)
    /// - GetScript/SetScript: Manages script ResRefs for event types (OnHeartbeat, OnAttacked, etc.)
    /// - Local variables: Per-entity local variables (int, float, string) stored in GFF LocalVars structure
    /// - Local variables persist in save games and are accessible via NWScript GetLocal* functions
    /// - Script hooks executed by event bus when game events occur (combat, damage, dialogue, etc.)
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

