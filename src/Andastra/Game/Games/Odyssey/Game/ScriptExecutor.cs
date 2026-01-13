using System;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common;
using Andastra.Game.Scripting.EngineApi;
using Andastra.Game.Scripting.Interfaces;
using Andastra.Game.Scripting.VM;
using JetBrains.Annotations;
using EngineApi = Andastra.Game.Games.Odyssey.EngineApi;
using VM = Andastra.Game.Scripting.VM;

namespace Andastra.Game.Games.Odyssey
{
    /// <summary>
    /// Unified Odyssey Engine script executor implementation for KOTOR 1 and KOTOR 2 (TSL).
    /// Uses OdysseyEngineApi with conditional logic based on game type.
    /// </summary>
    /// <remarks>
    /// Odyssey Script Executor:
    /// - Unified implementation for both KOTOR1 and KOTOR2
    /// - Inherits from BaseScriptExecutor (Runtime.Games.Common) with Odyssey-specific resource loading
    /// - Uses Installation resource system for NCS bytecode loading
    /// - Supports both swkotor.exe (KOTOR1) and swkotor2.exe (KOTOR2) via OdysseyEngineApi parameter
    /// - Game-specific behavior is handled by OdysseyEngineApi (conditional logic based on BioWareGame enum)
    ///
    /// Common Odyssey script execution features:
    /// - NCS file format: Compiled NWScript bytecode with "NCS " signature, "V1.0" version string
    /// - Script loading: Loads NCS files from installation via ResourceLookup (ResourceType.NCS)
    /// - Execution context: Creates ExecutionContext with owner (OBJECT_SELF), world, engine API, globals
    /// - OBJECT_SELF: Set to owner entity ObjectId (0x7F000001 = OBJECT_SELF constant)
    /// - OBJECT_INVALID: 0x7F000000 (invalid object reference constant)
    /// - Triggerer: Optional triggering entity (for event-driven scripts like OnEnter, OnClick, etc.)
    /// - Return value: Script return value (0 = FALSE, non-zero = TRUE) for condition scripts
    /// - Error handling: Returns 0 (FALSE) on script load failure or execution error
    /// - Instruction budget tracking: Tracks instruction count per entity for budget enforcement
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Script execution functions (KOTOR1)
    /// - swkotor2.exe: DispatchScriptEvent @ 0x004dd730, 0x004dcfb0 @ 0x004dcfb0 (KOTOR2)
    /// - Script execution: 0x004dcfb0 dispatches script events and executes scripts
    ///   - Function signature: `int 0x004dcfb0(void *param_1, int param_2, void *param_3, int param_4)`
    ///   - param_1: Entity pointer (owner of script)
    ///   - param_2: Script event type (CSWSSCRIPTEVENT_EVENTTYPE_* constant)
    ///   - param_3: Triggerer entity pointer (optional, can be null)
    ///   - param_4: Unknown flag
    ///   - Loads script ResRef from entity's script hook field based on event type
    ///   - Executes script with owner as OBJECT_SELF, triggerer as OBJECT_TRIGGERER
    ///   - Returns script return value (0 = FALSE, non-zero = TRUE)
    /// - Based on NCS VM execution in vendor/PyKotor/wiki/NCS-File-Format.md
    /// - Event types: Comprehensive mapping from 0x0 (ON_HEARTBEAT) to 0x26 (ON_DESTROYPLAYERCREATURE)
    /// </remarks>
    public class OdysseyScriptExecutor : BaseScriptExecutor
    {
        private readonly Installation _installation;
        private readonly IGameServicesContext _servicesContext;

        public OdysseyScriptExecutor([NotNull] IWorld world, [NotNull] IEngineApi engineApi, [NotNull] IScriptGlobals globals, [NotNull] Installation installation, [CanBeNull] IGameServicesContext servicesContext = null)
            : base(world, engineApi, globals)
        {
            _installation = installation ?? throw new ArgumentNullException(nameof(installation));
            _servicesContext = servicesContext;
        }

        /// <summary>
        /// Executes a script with Odyssey-specific resource loading.
        /// </summary>
        /// <remarks>
        /// Common across KOTOR1 and KOTOR2: Uses Installation resource loading.
        /// Game-specific behavior is handled by the engine API (Kotor1 vs TheSithLords).
        /// </remarks>
        public override int ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer = null)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return 0; // FALSE
            }

            try
            {
                // Load NCS bytecode using Odyssey resource system
                byte[] bytecode = LoadNcsBytecode(scriptResRef);
                if (bytecode == null || bytecode.Length == 0)
                {
                    Console.WriteLine("[OdysseyScriptExecutor] Script not found: " + scriptResRef);
                    return 0; // FALSE
                }

                // Create execution context with Odyssey-specific enhancements
                var context = CreateExecutionContext(caller, triggerer);

                // Execute script via VM
                // Common across KOTOR1 and KOTOR2: Script execution with instruction budget tracking
                // Based on swkotor.exe/swkotor2.exe: Script execution budget limits per frame
                // Original implementation: Tracks instruction count per entity for budget enforcement
                int returnValue = _vm.Execute(bytecode, context);

                // Accumulate instruction count to owner entity's action queue component
                // This allows the game loop to enforce per-frame script budget limits
                TrackScriptExecution(caller, _vm.InstructionsExecuted);

                return returnValue;
            }
            catch (Exception ex)
            {
                HandleScriptError(scriptResRef, caller, ex);
                return 0; // FALSE on error
            }
        }

        /// <summary>
        /// Loads NCS bytecode using Odyssey Installation resource system.
        /// </summary>
        /// <remarks>
        /// Common across KOTOR1 and KOTOR2: Uses Installation.ResourceLookup for NCS files.
        /// Based on swkotor.exe/swkotor2.exe resource loading patterns.
        /// </remarks>
        protected override byte[] LoadNcsBytecode(string scriptResRef)
        {
            var resource = _installation.Resources.LookupResource(scriptResRef, ResourceType.NCS);
            return resource?.Data;
        }

        /// <summary>
        /// Creates an execution context with Odyssey-specific enhancements including game services context.
        /// </summary>
        /// <param name="caller">The calling entity (OBJECT_SELF).</param>
        /// <param name="triggerer">The triggering entity.</param>
        /// <returns>Execution context with engine API, world, globals, and game services context.</returns>
        /// <remarks>
        /// Odyssey-specific: Enhanced execution context with game services support.
        /// Sets AdditionalContext to IGameServicesContext to provide script access to:
        /// - DialogueManager: Dialogue system for conversation management
        /// - PlayerEntity: Current player character entity
        /// - CombatManager: Combat system for battle management
        /// - PartyManager: Party management for party member operations
        /// - ModuleLoader: Module loading system for area transitions
        /// - FactionManager: Faction relationship management
        /// - PerceptionManager: Creature perception system
        /// - CameraController: Camera positioning and movement
        /// - SoundPlayer: Audio playback system
        /// - GameSession: Game state management
        /// - JournalSystem: Quest and journal management
        /// - UISystem: UI screen and overlay management
        ///
        /// Based on reverse engineering of:
        /// - swkotor.exe: Script execution context setup (KOTOR1)
        /// - swkotor2.exe: 0x005226d0 @ 0x005226d0 (script execution context setup, KOTOR2)
        /// - Engine API functions (Kotor1, TheSithLords) access AdditionalContext as IGameServicesContext
        /// - Script execution: Engine API functions check for VMExecutionContext.AdditionalContext
        ///   and cast to IGameServicesContext to access game services
        /// </remarks>
        protected override IExecutionContext CreateExecutionContext(IEntity caller, IEntity triggerer)
        {
            var context = base.CreateExecutionContext(caller, triggerer);

            // Set resource provider to Installation for script resource loading
            if (context is VM.ExecutionContext vmContext)
            {
                vmContext.ResourceProvider = _installation;

                // Set additional context to game services context for engine API access
                // Engine API functions (Kotor1, TheSithLords) check for AdditionalContext
                // and cast to IGameServicesContext to access game services like PartyManager,
                // CombatManager, DialogueManager, etc.
                vmContext.AdditionalContext = _servicesContext;
            }

            return context;
        }
    }
}

