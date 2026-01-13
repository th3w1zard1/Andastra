using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Engines.Odyssey.Components
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) specific script hooks component.
    /// </summary>
    /// <remarks>
    /// Odyssey Script Hooks Component:
    /// - Inherits from BaseScriptHooksComponent (common functionality)
    /// - Odyssey-specific: No engine-specific differences identified - uses base implementation
    /// - Based on swkotor.exe and swkotor2.exe script event system
    /// - Located via string references: "ScriptHeartbeat" @ 0x007beeb0 (swkotor2.exe), "ScriptOnNotice" @ 0x007beea0
    /// - "ScriptAttacked" @ 0x007bee80, "ScriptDamaged" @ 0x007bee70, "ScriptDeath" @ 0x007bee20
    /// - "ScriptDialogue" @ 0x007bee40, "ScriptEndDialogue" @ 0x007bede0, "ScriptSpawn" @ 0x007bee30
    /// - "ScriptOnEnter" @ 0x007c1d40, "ScriptOnExit" @ 0x007c1d30, "ScriptOnUsed" @ 0x007beeb8
    /// - "ScriptOnOpen" @ 0x007c1a54, "ScriptOnClose" @ 0x007c1a8c, "ScriptOnDisarm" @ 0x007c1a1c
    /// - "ScriptOnTrapTriggered" @ 0x007c1a34, "ScriptOnClick" @ 0x007c1a20, "ScriptOnLock" @ 0x007c1a0c
    /// - "ScriptOnUnlock" @ 0x007c1a00, "ScriptOnFailToOpen" @ 0x007c1a10, "ScriptOnUserDefined" @ 0x007bee10
    /// - Original implementation:
    ///   - swkotor.exe: FUN_004ebf20, FUN_00500610, FUN_0058e660, FUN_0058da80 (script hooks save/load)
    ///   - swkotor2.exe: FUN_005226d0 @ 0x005226d0 (save script hooks for creatures)
    ///   - swkotor2.exe: FUN_00585ec0 @ 0x00585ec0 (save script hooks for placeables)
    ///   - swkotor2.exe: FUN_00584f40 @ 0x00584f40 (save script hooks for doors)
    ///   - swkotor2.exe: FUN_0050c510 @ 0x0050c510 (load script hooks from UTC template)
    /// - Maps script events to script resource references (ResRef strings)
    /// - Scripts are executed by NCS VM when events fire (OnHeartbeat, OnPerception, OnAttacked, etc.)
    /// - Script ResRefs stored in GFF structures (e.g., ScriptHeartbeat, ScriptOnNotice fields)
    /// - Local variables (int, float, string) stored per-entity for script execution context
    /// - Local variables accessed via GetLocalInt/GetLocalFloat/GetLocalString NWScript functions
    /// - Script execution context: Entity is caller (OBJECT_SELF), event triggerer is parameter
    /// </remarks>
    public class ScriptHooksComponent : BaseScriptHooksComponent
    {
        // Odyssey-specific implementation: Currently uses base class implementation
        // No engine-specific differences identified - all script hooks functionality is common
    }
}
