using System;
using JetBrains.Annotations;
using Odyssey.Core.Dialogue;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.VM;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.EngineApi;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Executes NCS scripts using the NCS VM.
    /// </summary>
    /// <remarks>
    /// Script Executor:
    /// - Based on swkotor2.exe script execution system
    /// - Located via string references: Script loading and execution functions handle NCS bytecode files
    /// - "ObjectId" @ 0x007bce5c (object ID field), "ObjectIDList" @ 0x007bfd7c (object ID list)
    /// - Script event types: "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x007bcb90 (0x0), "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68 (0x1)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED" @ 0x007bcaec (0x5)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT" @ 0x007bcb3c (0x6), "CSWSSCRIPTEVENT_EVENTTYPE_ON_ATTACKED" (0x7)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH" @ 0x007bca54 (0x8), "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4 (0x9)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN" @ 0x007bca9c (0xa), "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED" @ 0x007bca78 (0xb)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT" @ 0x007bca24 (0xc), "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER" @ 0x007bc9f8 (0xd)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT" @ 0x007bc9cc (0xe), "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER" @ 0x007bc9a0 (0xf)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT" @ 0x007bc974 (0x10), "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948 (0x15)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c (0x14), "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4 (0x1d)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c (0x1e), "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0 (0x1f)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED" @ 0x007bc868 (0x10), "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (0x16)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (0x17), "CSWSSCRIPTEVENT_EVENTTYPE_ON_USED" @ 0x007bc7d8 (0x19)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM" @ 0x007bc7fc (0x18), "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac (0x1a)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b), "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c (0x1d), "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704 (0x1e)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED" @ 0x007bc6d8 (0x1f), "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING" @ 0x007bc6ac (0x20)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED" @ 0x007bc678 (0x21), "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST" @ 0x007bc620 (0x22)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN" @ 0x007bc64c (0x23), "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP" @ 0x007bc5bc (0x24)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594 (0x25), "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec (0x26)
    /// - Script hook fields: "ScriptHeartbeat" @ 0x007beeb0, "ScriptOnNotice" @ 0x007beea0, "ScriptSpellAt" @ 0x007bee90
    /// - "ScriptAttacked" @ 0x007bee80, "ScriptDamaged" @ 0x007bee70, "ScriptDisturbed" @ 0x007bee60, "ScriptEndRound" @ 0x007bee50
    /// - "ScriptDialogue" @ 0x007bee40, "ScriptSpawn" @ 0x007bee34, "ScriptRested" @ 0x007bee24, "ScriptDeath" @ 0x007bee18
    /// - "ScriptUserDefine" @ 0x007bee04, "ScriptOnBlocked" @ 0x007bedf4, "ScriptEndDialogue" @ 0x007bede0
    /// - "ScriptOnEnter" @ 0x007c1d40, "ScriptOnExit" @ 0x007c1d30 (trigger scripts)
    /// - NCS file format: Compiled NWScript bytecode with "NCS " signature, "V1.0" version string
    /// - Script loading: Loads NCS files from installation via ResourceLookup (ResourceType.NCS)
    /// - Execution context: Creates ExecutionContext with owner (OBJECT_SELF), world, engine API, globals
    /// - OBJECT_SELF: Set to owner entity ObjectId (0x7F000001 = OBJECT_SELF constant)
    /// - OBJECT_INVALID: 0x7F000000 (invalid object reference constant)
    /// - Triggerer: Optional triggering entity (for event-driven scripts like OnEnter, OnClick, etc.)
    /// - Return value: Script return value (0 = FALSE, non-zero = TRUE) for condition scripts
    /// - Error handling: Returns 0 (FALSE) on script load failure or execution error
    /// - Script execution: FUN_004dcfb0 @ 0x004dcfb0 dispatches script events and executes scripts
    ///   - Function signature: `int FUN_004dcfb0(void *param_1, int param_2, void *param_3, int param_4)`
    ///   - param_1: Entity pointer (owner of script)
    ///   - param_2: Script event type (CSWSSCRIPTEVENT_EVENTTYPE_* constant)
    ///   - param_3: Triggerer entity pointer (optional, can be null)
    ///   - param_4: Unknown flag
    ///   - Loads script ResRef from entity's script hook field based on event type
    ///   - Executes script with owner as OBJECT_SELF, triggerer as OBJECT_TRIGGERER
    ///   - Returns script return value (0 = FALSE, non-zero = TRUE)
    /// - Based on NCS VM execution in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public class ScriptExecutor : IScriptExecutor
    {
        private readonly NcsVm _vm;
        private readonly IWorld _world;
        private readonly IScriptGlobals _globals;
        private readonly Installation _installation;
        private readonly IEngineApi _engineApi;

        public ScriptExecutor([NotNull] NcsVm vm, [NotNull] IWorld world, [NotNull] IScriptGlobals globals, [NotNull] Installation installation, [NotNull] IEngineApi engineApi)
        {
            _vm = vm ?? throw new ArgumentNullException("vm");
            _world = world ?? throw new ArgumentNullException("world");
            _globals = globals ?? throw new ArgumentNullException("globals");
            _installation = installation ?? throw new ArgumentNullException("installation");
            _engineApi = engineApi ?? throw new ArgumentNullException("engineApi");
        }

        /// <summary>
        /// Executes a script.
        /// </summary>
        /// <param name="scriptResRef">The script resource reference.</param>
        /// <param name="owner">The owner entity (OBJECT_SELF).</param>
        /// <param name="triggerer">The triggering entity.</param>
        /// <returns>The script return value (0 = FALSE, non-zero = TRUE).</returns>
        public int ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return 0; // FALSE
            }

            try
            {
                // Load NCS file from installation
                AuroraEngine.Common.Installation.ResourceResult resource = _installation.Resources.LookupResource(scriptResRef, ResourceType.NCS);
                if (resource == null || resource.Data == null)
                {
                    Console.WriteLine("[ScriptExecutor] Script not found: " + scriptResRef);
                    return 0; // FALSE
                }

                // Create execution context
                var context = new Odyssey.Scripting.VM.ExecutionContext(owner, _world, _engineApi, _globals);
                if (triggerer != null)
                {
                    context.SetTriggerer(triggerer);
                }

                // Execute script via VM
                int returnValue = _vm.Execute(resource.Data, context);

                return returnValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ScriptExecutor] Error executing script " + scriptResRef + ": " + ex.Message);
                return 0; // FALSE on error
            }
        }
    }
}

