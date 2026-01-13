using System.Threading;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Scripting.Interfaces;

namespace Andastra.Game.Scripting.VM
{
    /// <summary>
    /// Execution context for a script run.
    /// </summary>
    /// <remarks>
    /// Script Execution Context:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script execution context system
    /// - Located via string references: Script execution functions maintain context for each script run
    /// - NCS VM: NCS file format "NCS " signature @ offset 0, "V1.0" version @ offset 4, 0x42 marker @ offset 8, instructions start @ offset 0x0D
    /// - OBJECT_SELF: Set to caller entity ObjectId (constant 0x7F000001, used in NWScript GetObjectSelf function)
    /// - OBJECT_INVALID: Invalid object reference constant (0x7F000000, used for null object checks)
    /// - Original implementation: Each script execution maintains context for:
    ///   - Caller: The entity that owns the script (OBJECT_SELF, used by GetObjectSelf NWScript function)
    ///   - Triggerer: The entity that triggered the script (for event scripts like OnEnter, OnClick, OnPerception)
    ///   - World: Reference to game world for entity lookups and engine API calls
    ///   - EngineApi: Reference to NWScript engine API implementation (Kotor1 or TheSithLords based on game version)
    ///   - Globals: Reference to script globals system for global/local variable access (GetGlobal*, SetGlobal* functions)
    ///   - ResourceProvider: Reference to resource loading system (IGameResourceProvider or Installation) for loading scripts/assets
    /// - Script context is passed to NCS VM for ACTION opcode execution (engine function calls via EngineApi.CallEngineFunction)
    /// - Context cloning: WithCaller/WithTriggerer create new contexts with modified caller/triggerer (for nested script calls)
    /// - Additional context: Stores extra context data (DialogueManager, GameSession, etc.) for system-specific access
    /// - Current context tracking: Uses AsyncLocal to track the current execution context for delayed script execution (DelayCommand)
    ///   - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking for nested script calls and delayed actions
    ///   - Original implementation: Maintains execution context stack to allow delayed actions to access original caller/triggerer
    /// - Based on NCS VM execution model in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public class ExecutionContext : IExecutionContext
    {
        /// <summary>
        /// Thread-local storage for the current execution context.
        /// Used to track the active execution context during script execution for delayed script context capture.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking system.
        /// Located via string references: Execution context maintained for each script execution.
        /// Original implementation: Execution context stack allows nested script calls and delayed actions
        /// (DelayCommand) to access the original caller and triggerer from when the script was first executed.
        /// When QueueDelayedScript is called, it can retrieve the current execution context to capture
        /// the triggerer for delayed script execution.
        /// </remarks>
        private static readonly AsyncLocal<IExecutionContext> _currentContext = new AsyncLocal<IExecutionContext>();

        public ExecutionContext(IEntity caller, IWorld world, IEngineApi engineApi, IScriptGlobals globals)
        {
            Caller = caller;
            World = world;
            EngineApi = engineApi;
            Globals = globals;
        }

        public IEntity Caller { get; }
        public IEntity Triggerer { get; private set; }
        public IWorld World { get; }
        public IEngineApi EngineApi { get; }
        public IScriptGlobals Globals { get; }
        public object ResourceProvider { get; set; }

        /// <summary>
        /// Additional context data (e.g., DialogueManager, GameSession, etc.)
        /// </summary>
        public object AdditionalContext { get; set; }

        /// <summary>
        /// Stored VM state for DelayCommand state restoration.
        /// Set by STORE_STATE opcode when capturing execution context for delayed actions.
        /// </summary>
        public VmState StoredVmState { get; set; }

        public IExecutionContext WithCaller(IEntity newCaller)
        {
            var ctx = new ExecutionContext(newCaller, World, EngineApi, Globals);
            ctx.Triggerer = Triggerer;
            ctx.ResourceProvider = ResourceProvider;
            ctx.AdditionalContext = AdditionalContext;
            ctx.StoredVmState = StoredVmState?.Clone();
            return ctx;
        }

        public IExecutionContext WithTriggerer(IEntity newTriggerer)
        {
            var ctx = new ExecutionContext(Caller, World, EngineApi, Globals);
            ctx.Triggerer = newTriggerer;
            ctx.ResourceProvider = ResourceProvider;
            ctx.AdditionalContext = AdditionalContext;
            ctx.StoredVmState = StoredVmState?.Clone();
            return ctx;
        }

        /// <summary>
        /// Sets the triggerer for this context.
        /// </summary>
        public void SetTriggerer(IEntity triggerer)
        {
            Triggerer = triggerer;
        }

        /// <summary>
        /// Gets the current execution context for the current async execution flow.
        /// </summary>
        /// <returns>The current execution context, or null if no context is active.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking system.
        /// Original implementation: Allows code to retrieve the current execution context
        /// for delayed script execution and other operations that need access to the
        /// current script's caller and triggerer.
        /// </remarks>
        public static IExecutionContext GetCurrent()
        {
            return _currentContext.Value;
        }

        /// <summary>
        /// Sets the current execution context for the current async execution flow.
        /// </summary>
        /// <param name="context">The execution context to set as current, or null to clear.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking system.
        /// Original implementation: Sets the current execution context so that delayed
        /// script execution and other operations can access the current script's caller
        /// and triggerer. Should be called by NcsVm when starting and ending script execution.
        /// </remarks>
        public static void SetCurrent(IExecutionContext context)
        {
            _currentContext.Value = context;
        }
    }
}

