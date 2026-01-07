using System;
using System.Collections.Generic;
using Andastra.Runtime.Scripting.Types;

namespace Andastra.Runtime.Scripting.VM
{
    /// <summary>
    /// Represents the serialized state of the NCS VM stack and local variables.
    /// Used by STORE_STATE opcode for DelayCommand state restoration.
    /// </summary>
    /// <remarks>
    /// VM State Serialization:
    /// - Based on swkotor2.exe: STORE_STATE opcode implementation
    /// - Located via string references: "DelayCommand" @ 0x007be900 (NWScript DelayCommand function)
    /// - Original implementation: STORE_STATE opcode serializes stack and local variable state
    /// - Stack state: Captures stack region from (SP - stackBytes) to SP
    /// - Local state: Captures local variables from BP to (BP + localsBytes)
    /// - String pool: Captures string handles referenced in stack/locals
    /// - Location pool: Captures location IDs referenced in stack/locals
    /// - State restoration: When delayed action executes, state is restored to VM before action script runs
    /// - This enables closure semantics: delayed actions can access variables from their original execution context
    /// </remarks>
    public class VmState
    {
        /// <summary>
        /// Serialized stack region (from SP - stackBytes to SP).
        /// </summary>
        public byte[] StackData { get; set; }

        /// <summary>
        /// Number of bytes in the stack region.
        /// </summary>
        public int StackBytes { get; set; }

        /// <summary>
        /// Serialized local variables (from BP to BP + localsBytes).
        /// </summary>
        public byte[] LocalsData { get; set; }

        /// <summary>
        /// Number of bytes in the locals region.
        /// </summary>
        public int LocalsBytes { get; set; }

        /// <summary>
        /// Stack pointer value at time of state capture.
        /// </summary>
        public int StackPointer { get; set; }

        /// <summary>
        /// Base pointer value at time of state capture.
        /// </summary>
        public int BasePointer { get; set; }

        /// <summary>
        /// String pool snapshot (handle -> string mapping).
        /// Contains all strings referenced by stack/locals data.
        /// </summary>
        public Dictionary<int, string> StringPool { get; set; }

        /// <summary>
        /// Location pool snapshot (ID -> Location mapping).
        /// Contains all locations referenced by stack/locals data.
        /// </summary>
        public Dictionary<uint, Location> LocationPool { get; set; }

        /// <summary>
        /// Next string handle value at time of state capture.
        /// </summary>
        public int NextStringHandle { get; set; }

        /// <summary>
        /// Next location ID value at time of state capture.
        /// </summary>
        public uint NextLocationId { get; set; }

        /// <summary>
        /// Creates a new empty VM state.
        /// </summary>
        public VmState()
        {
            StringPool = new Dictionary<int, string>();
            LocationPool = new Dictionary<uint, Location>();
        }

        /// <summary>
        /// Creates a deep copy of this VM state.
        /// </summary>
        public VmState Clone()
        {
            var clone = new VmState
            {
                StackBytes = StackBytes,
                LocalsBytes = LocalsBytes,
                StackPointer = StackPointer,
                BasePointer = BasePointer,
                NextStringHandle = NextStringHandle,
                NextLocationId = NextLocationId
            };

            // Deep copy stack data
            if (StackData != null)
            {
                clone.StackData = new byte[StackData.Length];
                Array.Copy(StackData, clone.StackData, StackData.Length);
            }

            // Deep copy locals data
            if (LocalsData != null)
            {
                clone.LocalsData = new byte[LocalsData.Length];
                Array.Copy(LocalsData, clone.LocalsData, LocalsData.Length);
            }

            // Deep copy string pool
            foreach (var kvp in StringPool)
            {
                clone.StringPool[kvp.Key] = kvp.Value;
            }

            // Deep copy location pool
            foreach (var kvp in LocationPool)
            {
                clone.LocationPool[kvp.Key] = kvp.Value;
            }

            return clone;
        }
    }
}

