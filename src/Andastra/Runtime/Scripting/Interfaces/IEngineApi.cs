using System.Collections.Generic;

namespace Andastra.Runtime.Scripting.Interfaces
{
    /// <summary>
    /// Engine function dispatch interface for NWScript ACTION calls.
    /// </summary>
    /// <remarks>
    /// Engine API Interface:
    /// - Based on swkotor2.exe NWScript engine API system
    /// - Located via string references: ACTION opcode handler dispatches to engine function implementations
    /// - "ActionList" @ 0x007bebdc (action list GFF field), "ActionId" @ 0x007bebd0 (action ID field)
    /// - "ActionType" @ 0x007bf7f8 (action type field), "PRINTSTRING: %s\n" @ 0x007c29f8 (PrintString debug output)
    /// - Original implementation: ACTION opcode (0x2A) calls engine functions by routine ID
    /// - Routine ID: uint16 value (big-endian) from NCS bytecode, maps to function index in engine API
    /// - Function dispatch: Original engine uses dispatch table indexed by routine ID to call function implementations
    /// - K1 functions: ~850 functions (routine IDs 0-849)
    /// - K2 functions: ~950 functions (routine IDs 0-949, K1 functions 0-799 shared)
    /// - Function signature: All functions receive variable arguments list and execution context
    /// - Return value: Functions return Variable (can be int, float, string, object, location, void)
    /// - Function implementations must match original engine behavior for script compatibility
    /// - Based on NCS VM ACTION opcode semantics in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public interface IEngineApi
    {
        /// <summary>
        /// Calls an engine function by routine ID.
        /// </summary>
        Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx);

        /// <summary>
        /// Gets the name of an engine function by routine ID.
        /// </summary>
        string GetFunctionName(int routineId);

        /// <summary>
        /// Gets the expected argument count for a function.
        /// </summary>
        int GetArgumentCount(int routineId);

        /// <summary>
        /// Whether the function is implemented.
        /// </summary>
        bool IsImplemented(int routineId);
    }
}

