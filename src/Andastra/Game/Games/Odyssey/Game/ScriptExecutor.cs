using System;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.VM;
using JetBrains.Annotations;
using EngineApi = Andastra.Runtime.Engines.Odyssey.EngineApi;
using VM = Andastra.Runtime.Scripting.VM;

namespace Andastra.Game.Games.Odyssey
{
    /// <summary>
    /// Base Odyssey Engine script executor implementation.
    /// </summary>
    /// <remarks>
    /// Base Odyssey Script Executor:
    /// - Common script execution logic shared between KOTOR1 and KOTOR2
    /// - Inherits from BaseScriptExecutor (Runtime.Games.Common) with Odyssey-specific resource loading
    /// - Uses Installation resource system for NCS bytecode loading
    /// - Supports both swkotor.exe (KOTOR1) and swkotor2.exe (KOTOR2) via engine API parameter
    /// - Game-specific behavior is handled by engine API (Kotor1 vs TheSithLords)
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
    /// - swkotor2.exe: DispatchScriptEvent @ 0x004dd730, FUN_004dcfb0 @ 0x004dcfb0 (KOTOR2)
    /// - Script execution: FUN_004dcfb0 dispatches script events and executes scripts
    ///   - Function signature: `int FUN_004dcfb0(void *param_1, int param_2, void *param_3, int param_4)`
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
    public abstract class OdysseyScriptExecutor : BaseScriptExecutor
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
        /// - swkotor2.exe: FUN_005226d0 @ 0x005226d0 (script execution context setup, KOTOR2)
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

    /// <summary>
    /// KOTOR 1 (swkotor.exe) script executor implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 1 Script Executor:
    /// - Based on swkotor.exe script execution system
    /// - Uses Kotor1 engine API (~850 engine functions, function IDs 0-849)
    /// - KOTOR1-specific features: Pazaak, Swoop Racing, Turret minigames
    /// - Does not support: Influence system, Prestige Classes, Combat Forms, Item Crafting
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Script execution functions
    /// - Script execution: FUN_004dcfb0 @ 0x004dcfb0 (similar to KOTOR2 but with K1-specific behavior)
    /// - ExecuteScript NWScript function: Different implementation than KOTOR2 (doesn't use _runScriptVar)
    /// - Located via string references: KOTOR1-specific script event type definitions
    /// </remarks>
    public class Kotor1ScriptExecutor : OdysseyScriptExecutor
    {
        public Kotor1ScriptExecutor([NotNull] IWorld world, [NotNull] IEngineApi engineApi, [NotNull] IScriptGlobals globals, [NotNull] Installation installation, [CanBeNull] IGameServicesContext servicesContext = null)
            : base(world, engineApi, globals, installation, servicesContext)
        {
            // Validate that engine API is Kotor1
            if (!(engineApi is EngineApi.Kotor1))
            {
                throw new ArgumentException("Kotor1ScriptExecutor requires Kotor1 engine API", nameof(engineApi));
            }
        }
    }

    /// <summary>
    /// KOTOR 2: The Sith Lords (swkotor2.exe) script executor implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Script Executor:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script execution system
    /// - Uses TheSithLords engine API (~950 engine functions, function IDs 0-949)
    /// - TSL-specific features: Influence system, Prestige Classes, Combat Forms, Item Crafting
    /// - Enhanced script execution with additional context support
    ///
    /// Based on reverse engineering of:
    /// - swkotor2.exe: DispatchScriptEvent @ 0x004dd730, FUN_004dcfb0 @ 0x004dcfb0
    /// - Script execution: Enhanced script execution with instruction budget tracking
    /// - ExecuteScript NWScript function: Uses _runScriptVar for script variable support
    /// - Located via string references: TSL-specific script event type definitions
    /// - Event types: Comprehensive mapping from 0x0 (ON_HEARTBEAT) to 0x26 (ON_DESTROYPLAYERCREATURE)
    /// </remarks>
    public class Kotor2ScriptExecutor : OdysseyScriptExecutor
    {
        public Kotor2ScriptExecutor([NotNull] IWorld world, [NotNull] IEngineApi engineApi, [NotNull] IScriptGlobals globals, [NotNull] Installation installation, [CanBeNull] IGameServicesContext servicesContext = null)
            : base(world, engineApi, globals, installation, servicesContext)
        {
            // Validate that engine API is TheSithLords
            if (!(engineApi is EngineApi.TheSithLords))
            {
                throw new ArgumentException("Kotor2ScriptExecutor requires TheSithLords engine API", nameof(engineApi));
            }
        }
    }
}

