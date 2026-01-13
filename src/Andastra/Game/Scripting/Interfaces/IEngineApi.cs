using System.Collections.Generic;

namespace Andastra.Game.Scripting.Interfaces
{
    /// <summary>
    /// Engine function dispatch interface for NWScript ACTION calls.
    /// </summary>
    /// <remarks>
    /// Engine API Interface:
    /// - CVirtualMachineInternal::ExecuteCode @ (K1: 0x005d2bd0, TSL: TODO: Find this address) NWScript engine API system
    /// - Located via string references: ACTION opcode handler dispatches to engine function implementations
    /// - "ActionList" @ 0x007bebdc (K2 action list GFF field), "ActionList" @ 0x00745ea0 (K1 action list GFF field)
    /// - "ActionId" @ 0x007bebd0 (K2 action ID field), "PRINTSTRING: %s\n" @ 0x007c29f8 (K2 PrintString debug output)
    /// - "PRINTSTRING: %s\n" @ 0x00748718 (K1 PrintString debug output)
    /// - Original implementation: ACTION opcode (0x05 in bytecode, enum value ACTION in switch) calls engine functions by routine ID
    /// - ACTION opcode handler: In ExecuteCode switch statement, case ACTION: (line 281-293) calls command_implementer->vtable->ExecuteCommand
    /// - ExecuteCommand parameters: routine ID (uint16 big-endian from bytes 2-3) and arg count (uint8 from byte 4)
    /// - Routine ID: uint16 value (big-endian) from NCS bytecode, maps to function index in engine API
    /// - Function dispatch: Original engine uses dispatch table indexed by routine ID to call function implementations
    /// - K1 functions: ~850 functions (routine IDs 0-849)
    /// - K2 functions: ~950 functions (routine IDs 0-949, K1 functions 0-799 shared)
    /// - Function signature: All functions receive variable arguments list and execution context
    /// - Return value: Functions return Variable (can be int, float, string, object, location, void)
    /// - Function implementations must match original engine behavior for script compatibility
    /// - Based on NCS VM ACTION opcode semantics in vendor/PyKotor/wiki/NCS-File-Format.md
    /// - swkotor.exe: ExecuteCode @ 0x005d2bd0, ACTION case calls ExecuteCommand vtable function with routine ID and arg count
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

