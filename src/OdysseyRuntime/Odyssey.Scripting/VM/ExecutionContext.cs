using Odyssey.Core.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Scripting.VM
{
    /// <summary>
    /// Execution context for a script run.
    /// </summary>
    /// <remarks>
    /// Script Execution Context:
    /// - Based on swkotor2.exe script execution context system
    /// - Located via string references: Script execution functions maintain context for each script run
    /// - OBJECT_SELF: Set to caller entity ObjectId (constant 0x7F000001)
    /// - OBJECT_INVALID: Invalid object reference constant (0x7F000000)
    /// - Original implementation: Each script execution maintains:
    ///   - Caller: The entity that owns the script (OBJECT_SELF)
    ///   - Triggerer: The entity that triggered the script (for event scripts like OnEnter, OnClick)
    ///   - World: Reference to game world for entity lookups and engine API calls
    ///   - EngineApi: Reference to NWScript engine API implementation (K1EngineApi or K2EngineApi)
    ///   - Globals: Reference to script globals system for global/local variable access
    /// - Script context is passed to NCS VM for ACTION opcode execution (engine function calls)
    /// - Based on NCS VM execution model in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public class ExecutionContext : IExecutionContext
    {
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
        
        public IExecutionContext WithCaller(IEntity newCaller)
        {
            var ctx = new ExecutionContext(newCaller, World, EngineApi, Globals);
            ctx.Triggerer = Triggerer;
            ctx.ResourceProvider = ResourceProvider;
            ctx.AdditionalContext = AdditionalContext;
            return ctx;
        }
        
        public IExecutionContext WithTriggerer(IEntity newTriggerer)
        {
            var ctx = new ExecutionContext(Caller, World, EngineApi, Globals);
            ctx.Triggerer = newTriggerer;
            ctx.ResourceProvider = ResourceProvider;
            ctx.AdditionalContext = AdditionalContext;
            return ctx;
        }
        
        /// <summary>
        /// Sets the triggerer for this context.
        /// </summary>
        public void SetTriggerer(IEntity triggerer)
        {
            Triggerer = triggerer;
        }
    }
}

