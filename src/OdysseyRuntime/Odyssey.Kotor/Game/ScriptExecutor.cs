using System;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.VM;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.EngineApi;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;

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
    /// - NCS file format: Compiled NWScript bytecode with "NCS " signature, "V1.0" version string
    /// - Script loading: Loads NCS files from installation via ResourceLookup (ResourceType.NCS)
    /// - Execution context: Creates ExecutionContext with owner (OBJECT_SELF), world, engine API, globals
    /// - OBJECT_SELF: Set to owner entity ObjectId (0x7F000001 = OBJECT_SELF constant)
    /// - OBJECT_INVALID: 0x7F000000 (invalid object reference constant)
    /// - Triggerer: Optional triggering entity (for event-driven scripts like OnEnter, OnClick, etc.)
    /// - Return value: Script return value (0 = FALSE, non-zero = TRUE) for condition scripts
    /// - Error handling: Returns 0 (FALSE) on script load failure or execution error
    /// - Script execution: FUN_004dcfb0 @ 0x004dcfb0 dispatches script events and executes scripts
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
                ResourceResult resource = _installation.Resources.LookupResource(scriptResRef, ResourceType.NCS);
                if (resource == null || resource.Data == null)
                {
                    Console.WriteLine("[ScriptExecutor] Script not found: " + scriptResRef);
                    return 0; // FALSE
                }

                // Create execution context
                var context = new VM.ExecutionContext(owner, _world, _engineApi, _globals);
                if (triggerer != null)
                {
                    context = context.WithTriggerer(triggerer);
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

