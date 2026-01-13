using Andastra.Runtime.Scripting.VM;

namespace Andastra.Game.Scripting.Interfaces
{
    /// <summary>
    /// NWScript compiled script virtual machine.
    /// </summary>
    /// <remarks>
    /// NCS VM Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) NCS VM implementation
    /// - Located via string references: NCS script execution engine handles bytecode interpretation
    /// - NCS file format: "NCS " signature (bytes 0-3), "V1.0" version (bytes 4-7), 0x42 marker at offset 8
    /// - Instructions start at offset 0x0D (13 decimal) - matches original engine NCS file structure
    /// - Original implementation: NCS VM executes bytecode instructions, handles ACTION opcode for engine function calls
    /// - Stack-based VM with 65536-byte stack, 4-byte aligned
    /// - Opcodes: ACTION (0x2A) calls engine functions, others handle stack operations, jumps, conditionals
    /// - ACTION opcode format: uint16 routineId (big-endian) + uint8 argCount (stack elements, not bytes)
    /// - Original engine uses big-endian encoding for all multi-byte values
    /// - Stack alignment: 4-byte aligned, vectors are 12 bytes (3 floats)
    /// - Jump offsets: Relative to instruction start (current PC), not next instruction
    /// - Object references: 0x7F000000 = OBJECT_INVALID, 0x7F000001 = OBJECT_SELF (caller entity)
    /// - Instruction limit: MaxInstructions prevents infinite loops (default 100000 instructions per execution)
    /// - String handling: Strings stored in string pool with integer handles (off-stack storage)
    /// - Based on NCS file format documentation in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public interface INcsVm
    {
        /// <summary>
        /// Executes a script and returns the result.
        /// </summary>
        int Execute(byte[] ncsBytes, IExecutionContext ctx);

        /// <summary>
        /// Executes a script by resource reference.
        /// </summary>
        int ExecuteScript(string resRef, IExecutionContext ctx);

        /// <summary>
        /// Aborts the currently executing script.
        /// </summary>
        void Abort();

        /// <summary>
        /// Whether a script is currently running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the total instructions executed in the current run.
        /// </summary>
        int InstructionsExecuted { get; }

        /// <summary>
        /// Maximum instructions per execution (budget).
        /// </summary>
        int MaxInstructions { get; set; }

        /// <summary>
        /// Whether to enable instruction tracing.
        /// </summary>
        bool EnableTracing { get; set; }

        /// <summary>
        /// Restores VM state from a previously stored VmState.
        /// Used when executing delayed actions to restore the execution context.
        /// </summary>
        /// <param name="state">The VM state to restore.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): State restoration for DelayCommand.
        /// Original implementation: Restores stack, locals, and pools before executing delayed action.
        /// </remarks>
        void RestoreState(VmState state);
    }
}

