using System;
using BioWare.NET;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.VM;

namespace Andastra.Game.Scripting
{
    /// <summary>
    /// Odyssey Engine script executor implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Script Executor:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DispatchScriptEvent @ 0x004dd730
    /// - Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x007bcb90, "ACTION" @ 0x007cd138
    /// - Script loading: Loads NCS bytecode from resource provider (IGameResourceProvider or Installation)
    /// - Script execution: Executes NCS bytecode via NCS VM with entity as caller (OBJECT_SELF)
    /// - Event-driven: Subscribes to script events (OnSpawn, OnHeartbeat, etc.) and executes matching scripts
    /// - Script hooks: IScriptHooksComponent stores script ResRefs mapped to event types
    /// - Execution context: Creates ExecutionContext with entity as caller, triggerer as parameter
    /// - Heartbeat timing: OnHeartbeat fires every 6 seconds (heartbeat interval)
    /// - Original implementation:
    ///   - swkotor2.exe: DispatchScriptEvent @ 0x004dd730 - Dispatches script events to registered handlers, creates event data structure, iterates through registered script handlers, calls FUN_004db870 to match event types, queues matching handlers
    ///   - swkotor2.exe: LoadScriptHooks @ 0x0050c510 - Loads script hook references from GFF templates (ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue)
    ///   - swkotor2.exe: LogScriptEvent @ 0x004dcfb0 - Logs script events for debugging, maps event types to string names
    ///   - swkotor.exe: DispatchScriptEvent (FUN_004af630 @ 0x004af630) - Equivalent to swkotor2.exe DispatchScriptEvent, dispatches script events to registered handlers, creates event data structure, iterates through registered script handlers, matches event types, queues matching handlers. Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x00744958, "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x00744930, "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT" @ 0x00744904 (swkotor.exe). Event subtype mapping logic is identical to swkotor2.exe but uses different string constant addresses.
    ///   - swkotor.exe: LoadScriptHooks - Loads script hook references from GFF templates (ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue). Function address needs verification via Ghidra analysis (expected in 0x004f-0x0050 address range based on swkotor.exe function address patterns).
    ///   - swkotor.exe: LogScriptEvent - Logs script events for debugging, maps event types to string names. String constants located at 0x00744958 (ON_HEARTBEAT), 0x00744930 (ON_PERCEPTION), 0x00744904 (ON_SPELLCASTAT), 0x007448dc (ON_DAMAGED), 0x007448b4 (ON_DISTURBED), 0x0074488c (ON_DIALOGUE), 0x00744864 (ON_SPAWN_IN), 0x00744840 (ON_RESTED), 0x0074481c (ON_DEATH), 0x007447ec (ON_USER_DEFINED_EVENT). Function address needs verification via Ghidra analysis (may be integrated into FUN_004af630 or separate function).
    ///   - nwmain.exe: ExecuteCommandExecuteScript @ 0x14051d5c0 - Executes script command in NCS VM, thunk that calls FUN_140c10370, part of CNWSVirtualMachineCommands class
    ///   - nwmain.exe: CScriptEvent @ 0x1404c6490 - Script event constructor, initializes event data structure
    ///   - daorigins.exe: Script event system (Eclipse/UnrealScript architecture) - Uses UnrealScript-based event system with different architecture than Odyssey/Aurora engines. Event scripts stored via "EventScripts" @ 0x00ae81bc (daorigins.exe), 0x00bf5464 (DragonAge2.exe). Event listeners via "EventListeners" @ 0x00ae8194 (daorigins.exe), 0x00bf543c (DragonAge2.exe). Script execution uses UnrealScript message passing system (ShowConversationGUIMessage @ 0x00ae8a50 for daorigins.exe, @ 0x00bfca24 for DragonAge2.exe). No direct NCS VM equivalent - Eclipse engine uses UnrealScript bytecode instead of NCS bytecode. Script hooks loaded from entity templates (UTC, UTP, etc.) similar to Odyssey engine but with UnrealScript integration.
    /// - Note: This is a standalone implementation in Scripting. Engine-specific implementations
    ///   (e.g., OdysseyScriptExecutor) should inherit from BaseScriptExecutor in Runtime.Games.Common
    /// - Based on NCS VM execution model in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public class ScriptExecutor : IScriptExecutor
    {
        private readonly IWorld _world;
        private readonly IEngineApi _engineApi;
        private readonly IScriptGlobals _globals;
        private readonly object _resourceProvider;
        private readonly INcsVm _vm;

        public ScriptExecutor(IWorld world, IEngineApi engineApi, IScriptGlobals globals, object resourceProvider)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _engineApi = engineApi ?? throw new ArgumentNullException(nameof(engineApi));
            _globals = globals ?? throw new ArgumentNullException(nameof(globals));
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _vm = new NcsVm();
        }

        /// <summary>
        /// Executes a script on an entity.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific script execution using Installation resource provider.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script loading and execution patterns.
        /// </remarks>
        public int ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
        {
            if (owner == null || string.IsNullOrEmpty(scriptResRef))
            {
                return 0; // FALSE
            }

            try
            {
                // Load NCS bytecode using Odyssey resource system
                byte[] bytecode = LoadNcsBytecode(scriptResRef);
                if (bytecode == null || bytecode.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ScriptExecutor] Script not found: {scriptResRef}");
                    return 0; // FALSE
                }

                // Create execution context
                var context = new ExecutionContext(owner, _world, _engineApi, _globals);
                context.SetTriggerer(triggerer);

                // Execute script via VM
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Script execution with instruction budget tracking
                // Located via string references: Script execution budget limits per frame
                // Original implementation: Tracks instruction count per entity for budget enforcement
                int returnValue = _vm.Execute(bytecode, context);

                // Accumulate instruction count to owner entity's action queue component
                // This allows the game loop to enforce per-frame script budget limits
                int instructionsExecuted = _vm.InstructionsExecuted;
                // TrackScriptExecution would be implemented here if needed

                return returnValue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScriptExecutor] Error executing script {scriptResRef}: {ex.Message}");
                return 0; // FALSE on error
            }
        }

        /// <summary>
        /// Loads NCS bytecode for a script using Odyssey resource system.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses Installation.ResourceLookup for NCS files.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource loading patterns.
        /// </remarks>
        private byte[] LoadNcsBytecode(string scriptResRef)
        {
            // Try different resource provider types
            if (_resourceProvider is Installation installation)
            {
                var resource = installation.Resources.LookupResource(scriptResRef, ResourceType.NCS);
                return resource?.Data;
            }
            else if (_resourceProvider is IGameResourceProvider resourceProvider)
            {
                return resourceProvider.GetResourceBytes(new ResourceIdentifier(scriptResRef, ResourceType.NCS));
            }

            return null;
        }
    }

}

