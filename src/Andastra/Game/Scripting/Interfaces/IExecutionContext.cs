using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Scripting.Interfaces
{
    /// <summary>
    /// Execution context for a script run.
    /// </summary>
    /// <remarks>
    /// Script Execution Context Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script execution context system
    /// - Located via string references: Script execution functions maintain context for each script run
    /// - NCS VM: NCS file format "NCS " signature @ offset 0, "V1.0" version @ offset 4, 0x42 marker @ offset 8, instructions start @ offset 0x0D
    /// - OBJECT_SELF: Set to caller entity ObjectId (constant 0x7F000001, used in NWScript GetObjectSelf function)
    /// - OBJECT_INVALID: Invalid object reference constant (0x7F000000, used for null object checks)
    /// - Original implementation: Each script execution maintains:
    ///   - Caller: The entity that owns the script (OBJECT_SELF)
    ///   - Triggerer: The entity that triggered the script (for event scripts like OnEnter, OnClick)
    ///   - World: Reference to game world for entity lookups and engine API calls
    ///   - EngineApi: Reference to NWScript engine API implementation (Kotor1 or TheSithLords)
    ///   - Globals: Reference to script globals system for global/local variable access
    /// - Script context is passed to NCS VM for ACTION opcode execution (engine function calls)
    /// - Based on NCS VM execution model in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public interface IExecutionContext
    {
        /// <summary>
        /// The object running the script (OBJECT_SELF).
        /// </summary>
        IEntity Caller { get; }

        /// <summary>
        /// The triggering object (GetEnteringObject, etc.)
        /// </summary>
        IEntity Triggerer { get; }

        /// <summary>
        /// The game world.
        /// </summary>
        IWorld World { get; }

        /// <summary>
        /// The engine API for ACTION calls.
        /// </summary>
        IEngineApi EngineApi { get; }

        /// <summary>
        /// Global script variables.
        /// </summary>
        IScriptGlobals Globals { get; }

        /// <summary>
        /// The resource provider for loading scripts.
        /// </summary>
        object ResourceProvider { get; }

        /// <summary>
        /// Creates a child context with a new caller.
        /// </summary>
        IExecutionContext WithCaller(IEntity newCaller);

        /// <summary>
        /// Creates a child context with a new triggerer.
        /// </summary>
        IExecutionContext WithTriggerer(IEntity newTriggerer);
    }
}

