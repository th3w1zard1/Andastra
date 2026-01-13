using System;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.VM;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of script execution shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Script Executor Implementation:
    /// - Common NCS (NWScript Compiled Script) execution across all engines
    /// - Stack-based VM with instruction limit enforcement
    /// - Script event routing and hook management
    /// - Execution context management
    ///
    /// Based on reverse engineering of multiple BioWare engines.
    /// Common NCS script execution patterns identified across all engines.
    ///
    /// Common script execution features:
    /// - NCS bytecode interpretation via NcsVm
    /// - Execution context with caller, triggerer, world, globals
    /// - Script hook system for event-driven execution
    /// - Instruction budget enforcement (default 100,000 instructions)
    /// - Resource provider integration for script loading
    /// - Error handling and logging
    /// - DelayCommand state management
    /// </remarks>
    [PublicAPI]
    public abstract class BaseScriptExecutor : IScriptExecutor
    {
        protected readonly IWorld _world;
        protected readonly IEngineApi _engineApi;
        protected readonly IScriptGlobals _globals;
        protected readonly INcsVm _vm;

        /// <summary>
        /// Default instruction limit per script execution.
        /// </summary>
        /// <remarks>
        /// Common script execution budget limit across all engines.
        /// Prevents infinite loops and ensures fair frame time distribution.
        /// </remarks>
        protected const int DefaultMaxInstructions = 100000;

        /// <summary>
        /// Creates a new base script executor.
        /// </summary>
        /// <param name="world">The game world for entity lookups.</param>
        /// <param name="engineApi">The engine API for script function calls.</param>
        /// <param name="globals">Global and local variable storage.</param>
        protected BaseScriptExecutor(IWorld world, IEngineApi engineApi, IScriptGlobals globals)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _engineApi = engineApi ?? throw new ArgumentNullException(nameof(engineApi));
            _globals = globals ?? throw new ArgumentNullException(nameof(globals));
            _vm = new NcsVm
            {
                MaxInstructions = DefaultMaxInstructions,
                EnableTracing = false // Set to true for debugging
            };
        }

        /// <summary>
        /// Executes a script on an entity.
        /// </summary>
        /// <param name="caller">The entity executing the script (OBJECT_SELF).</param>
        /// <param name="scriptResRef">The resource reference name of the script.</param>
        /// <param name="triggerer">Optional entity that triggered the script.</param>
        /// <returns>Script return value (typically 1 for TRUE, 0 for FALSE).</returns>
        /// <remarks>
        /// Common across all engines: Loads NCS bytecode and executes via VM.
        /// Creates execution context with caller, triggerer, world, globals.
        /// Tracks instruction count for budget enforcement.
        /// </remarks>
        public abstract int ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer = null);

        /// <summary>
        /// Executes a script event on an entity.
        /// </summary>
        /// <param name="entity">The entity to execute the script event on.</param>
        /// <param name="eventType">The type of script event.</param>
        /// <param name="triggerer">Optional entity that triggered the event.</param>
        /// <remarks>
        /// Based on DispatchScriptEvent functions across all engines.
        /// Maps event types to script hook ResRefs and executes matching scripts.
        /// Common event types: OnHeartbeat, OnSpawn, OnDeath, OnDialogue, etc.
        /// </remarks>
        public virtual void ExecuteScriptEvent(IEntity entity, int eventType, IEntity triggerer = null)
        {
            if (entity == null)
                return;

            // Get script hooks component
            var scriptHooks = entity.GetComponent<Core.Interfaces.Components.IScriptHooksComponent>();
            if (scriptHooks == null)
                return;

            // Map event type to script ResRef
            string scriptResRef = GetScriptHookForEvent(scriptHooks, eventType);
            if (string.IsNullOrEmpty(scriptResRef))
                return;

            // Execute the script
            ExecuteScript(entity, scriptResRef, triggerer);
        }

        /// <summary>
        /// Loads NCS bytecode for a script.
        /// </summary>
        /// <param name="scriptResRef">The resource reference name of the script.</param>
        /// <returns>NCS bytecode, or null if not found.</returns>
        /// <remarks>
        /// Engine-specific: Resource loading differs across engines.
        /// Must be implemented by engine-specific subclasses.
        /// </remarks>
        protected abstract byte[] LoadNcsBytecode(string scriptResRef);

        /// <summary>
        /// Creates an execution context for script execution.
        /// </summary>
        /// <param name="caller">The calling entity (OBJECT_SELF).</param>
        /// <param name="triggerer">The triggering entity.</param>
        /// <returns>Execution context with engine API, world, globals.</returns>
        /// <remarks>
        /// Common across all engines: Execution context provides script access to game systems.
        /// Engine API varies by game but context structure is common.
        /// </remarks>
        protected virtual IExecutionContext CreateExecutionContext(IEntity caller, IEntity triggerer)
        {
            var context = new ExecutionContext(caller, _world, _engineApi, _globals);
            context.SetTriggerer(triggerer);
            context.ResourceProvider = null; // Set by engine-specific implementation
            return context;
        }

        /// <summary>
        /// Tracks script execution for budget enforcement.
        /// </summary>
        /// <param name="entity">The entity that executed the script.</param>
        /// <param name="instructionsExecuted">Number of instructions executed.</param>
        /// <remarks>
        /// Common script execution budget tracking across all engines.
        /// Accumulates instruction count to entity's action queue component.
        /// Game loop enforces per-frame script budget limits.
        /// </remarks>
        protected virtual void TrackScriptExecution(IEntity entity, int instructionsExecuted)
        {
            if (entity == null)
                return;

            var actionQueue = entity.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
            if (actionQueue != null && instructionsExecuted > 0)
            {
                // Accumulate instruction count for budget tracking
                // Common across all engines: Accumulates instruction count to entity's action queue component
                // Game loop checks total instructions per frame via GetLastInstructionCount() and may defer scripts
                // if per-frame script budget limit (MaxScriptBudget) is exceeded
                // This prevents script lockups and ensures fair frame time distribution across all entities
                actionQueue.AddInstructionCount(instructionsExecuted);
            }
        }

        /// <summary>
        /// Handles script execution errors.
        /// </summary>
        /// <param name="scriptResRef">The script that failed.</param>
        /// <param name="entity">The entity executing the script.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <remarks>
        /// Common error handling: Log error details for debugging.
        /// Original engines: Log to console/log file with entity/script info.
        /// </remarks>
        protected virtual void HandleScriptError(string scriptResRef, IEntity entity, Exception exception)
        {
            string entityInfo = entity != null ? $"Entity {entity.ObjectId:X8} ({entity.Tag})" : "Unknown entity";
            System.Diagnostics.Debug.WriteLine($"[ScriptExecutor] Error executing script '{scriptResRef}' on {entityInfo}: {exception.Message}");
            System.Diagnostics.Debug.WriteLine($"[ScriptExecutor] Stack trace: {exception.StackTrace}");
        }

        /// <summary>
        /// Gets the script ResRef for a specific event type.
        /// </summary>
        /// <param name="scriptHooks">The script hooks component.</param>
        /// <param name="eventType">The event type.</param>
        /// <returns>Script ResRef, or empty string if none.</returns>
        /// <remarks>
        /// Based on LoadScriptHooks functions in all engines.
        /// Maps event types to script hook fields (OnHeartbeat, OnSpawn, etc.).
        /// Common event mapping across all BioWare engines.
        /// </remarks>
        protected virtual string GetScriptHookForEvent(Core.Interfaces.Components.IScriptHooksComponent scriptHooks, int eventType)
        {
            var scriptEvent = (Core.Enums.ScriptEvent)eventType;
            return scriptHooks.GetScript(scriptEvent) ?? string.Empty;
        }

        /// <summary>
        /// Queues a delayed script execution.
        /// </summary>
        /// <param name="entity">The entity to execute the script on.</param>
        /// <param name="scriptResRef">The script to execute.</param>
        /// <param name="delay">Delay in seconds.</param>
        /// <remarks>
        /// Based on DelayCommand NWScript function.
        /// Uses STORE_STATE opcode to capture execution context.
        /// DelayScheduler manages timed execution.
        /// 
        /// Common across all engines: DelayCommand pattern for delayed script execution.
        /// 
        /// Implementation flow:
        /// 1. Create ActionDoCommand that executes the script with captured context
        /// 2. Schedule action with DelayScheduler for execution after delay
        /// 3. When delay expires, DelayScheduler queues action to entity's action queue
        /// 4. Action executes script with original caller/triggerer context
        /// </remarks>
        public virtual void QueueDelayedScript(IEntity entity, string scriptResRef, float delay)
        {
            if (entity == null || string.IsNullOrEmpty(scriptResRef))
                return;

            // Get DelayScheduler from world
            // Common across all engines: DelayScheduler is accessed via IWorld.DelayScheduler
            var delayScheduler = _world?.DelayScheduler;
            if (delayScheduler == null)
            {
                System.Diagnostics.Debug.WriteLine("[BaseScriptExecutor] DelayScheduler not available - cannot queue delayed script");
                return;
            }

            // Capture execution context for delayed execution
            // Common across all engines: DelayCommand captures caller and triggerer context
            // The current execution context (caller, triggerer) is captured in the closure
            // This allows the delayed script to execute with the same context as when it was queued
            IEntity capturedCaller = entity;
            IEntity capturedTriggerer = null;

            // Get triggerer from current execution context if available
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking for delayed script execution
            // Located via string references: Execution context maintained for each script execution
            // Original implementation: Delayed actions (DelayCommand) capture the current execution context's
            // triggerer so that when the delayed script executes, it has access to the original triggerer
            // from when DelayCommand was called. This enables scripts to properly identify what entity
            // triggered the original script event, even after a delay.
            var currentContext = ExecutionContext.GetCurrent();
            if (currentContext != null)
            {
                // Use the current execution context's triggerer for delayed script execution
                // This matches the original engine behavior where delayed scripts inherit the
                // execution context from the script that queued them
                capturedTriggerer = currentContext.Triggerer;
            }

            // Create ActionDoCommand that executes the script
            // Common across all engines: DelayCommand creates action that executes script with captured context
            // ActionDoCommand wraps the script execution in an action that can be scheduled
            var delayedAction = new ActionDoCommand((targetEntity) =>
            {
                // Execute script on target entity with captured context
                // Common across all engines: Delayed script executes with original caller/triggerer
                // The script will have access to the same execution context as when DelayCommand was called
                ExecuteScript(capturedCaller, scriptResRef, capturedTriggerer);
            });

            // Schedule action with DelayScheduler
            // Common across all engines: DelayScheduler manages timed execution of delayed actions
            delayScheduler.ScheduleDelay(delay, delayedAction, entity);
        }

        /// <summary>
        /// Aborts currently executing script.
        /// </summary>
        /// <remarks>
        /// Emergency abort for runaway scripts or engine shutdown.
        /// Sets abort flag in VM to stop execution.
        /// </remarks>
        public virtual void AbortCurrentScript()
        {
            if (_vm.IsRunning)
            {
                _vm.Abort();
            }
        }

        /// <summary>
        /// Gets the currently executing instruction count.
        /// </summary>
        public virtual int CurrentInstructionCount => _vm.InstructionsExecuted;

        /// <summary>
        /// Gets or sets the maximum instructions per script execution.
        /// </summary>
        public virtual int MaxInstructions
        {
            get => _vm.MaxInstructions;
            set => _vm.MaxInstructions = value;
        }

        /// <summary>
        /// Gets or sets whether VM tracing is enabled.
        /// </summary>
        /// <remarks>
        /// Enables detailed instruction-level logging for debugging.
        /// Performance impact: significant when enabled.
        /// </remarks>
        public virtual bool EnableTracing
        {
            get => _vm.EnableTracing;
            set => _vm.EnableTracing = value;
        }

        /// <summary>
        /// IScriptExecutor interface implementation (parameter order differs from ExecuteScript).
        /// </summary>
        /// <remarks>
        /// Dialogue system uses IScriptExecutor with different parameter order:
        /// ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
        /// vs BaseScriptExecutor.ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer)
        /// This adapter method provides compatibility for dialogue system.
        /// Common across all engines: Dialogue system requires this interface.
        /// </remarks>
        int IScriptExecutor.ExecuteScript(string scriptResRef, IEntity owner, IEntity triggerer)
        {
            return ExecuteScript(owner, scriptResRef, triggerer);
        }
    }
}
