using System;
using System.Collections.Generic;
using System.Linq;

namespace BioWare.NET.Resource.Formats.NCS.Optimizers
{
    /// <summary>
    /// Optimizer to remove unused global variables from the stack.
    ///
    /// This optimizer identifies global variables declared in the globals section
    /// (before SAVEBP) and removes those that are never accessed via CPTOPBP,
    /// CPDOWNBP, INCxBP, or DECxBP instructions.
    ///
    /// When a global is removed:
    /// 1. The RSADD instruction that declares it is removed
    /// 2. All subsequent global variables shift up in the stack
    /// 3. All BP-relative instruction offsets are adjusted to account for the removed global
    ///
    /// References:
    ///     vendor/PyKotor/wiki/NCS-File-Format.md - Global variable access via BP
    ///     vendor/xoreos/src/aurora/nwscript/ncsfile.cpp:389-394 (globals)
    ///     vendor/xoreos/src/aurora/nwscript/ncsfile.cpp:1039-1060 (SAVEBP/RESTOREBP)
    /// </summary>
    public class RemoveUnusedGlobalsInStackOptimizer : NCSOptimizer
    {
        /// <summary>
        /// Represents a global variable declaration with its stack position and size.
        /// </summary>
        private class GlobalVariable
        {
            public NCSInstruction RsaddInstruction { get; set; }
            public int StackOffset { get; set; } // Negative offset from BP (e.g., -4, -8, -12)
            public int Size { get; set; } // Size in bytes (typically 4, or 12 for vectors)
            public bool IsUsed { get; set; }
        }

        public override void Optimize(NCS ncs)
        {
            if (ncs.Instructions.Count == 0)
            {
                return;
            }

            // Find the SAVEBP instruction that marks the end of the globals section
            int savebpIndex = -1;
            for (int i = 0; i < ncs.Instructions.Count; i++)
            {
                if (ncs.Instructions[i].InsType == NCSInstructionType.SAVEBP)
                {
                    // Use the LAST SAVEBP before main() as the globals boundary
                    // (some files have multiple SAVEBP instructions)
                    savebpIndex = i;
                }
            }

            // If no SAVEBP found, there are no globals to optimize
            if (savebpIndex < 0)
            {
                return;
            }

            // Step 1: Identify all global variable declarations (RSADD instructions before SAVEBP)
            var globals = new List<GlobalVariable>();
            int currentStackOffset = 0; // Track cumulative stack offset

            for (int i = 0; i < savebpIndex; i++)
            {
                NCSInstruction inst = ncs.Instructions[i];
                if (IsRsaddInstruction(inst.InsType))
                {
                    int size = GetRsaddSize(inst.InsType);
                    currentStackOffset -= size; // Negative offset (stack grows downward)
                    globals.Add(new GlobalVariable
                    {
                        RsaddInstruction = inst,
                        StackOffset = currentStackOffset,
                        Size = size,
                        IsUsed = false
                    });
                }
            }

            // If no globals found, nothing to optimize
            if (globals.Count == 0)
            {
                return;
            }

            // Step 2: Track which globals are actually used via BP-relative instructions
            // Scan all instructions after SAVEBP for CPTOPBP, CPDOWNBP, INCxBP, DECxBP
            for (int i = savebpIndex + 1; i < ncs.Instructions.Count; i++)
            {
                NCSInstruction inst = ncs.Instructions[i];
                int offset = 0;
                bool isBpInstruction = false;

                if (inst.InsType == NCSInstructionType.CPTOPBP || inst.InsType == NCSInstructionType.CPDOWNBP)
                {
                    // CPTOPBP/CPDOWNBP: Args[0] = offset (signed int), Args[1] = size (unsigned short)
                    if (inst.Args.Count >= 2 && inst.Args[0] is int offsetArg)
                    {
                        offset = offsetArg;
                        isBpInstruction = true;
                    }
                }
                else if (inst.InsType == NCSInstructionType.INCxBP || inst.InsType == NCSInstructionType.DECxBP)
                {
                    // INCxBP/DECxBP: Args[0] = offset (signed int)
                    if (inst.Args.Count >= 1 && inst.Args[0] is int offsetArg)
                    {
                        offset = offsetArg;
                        isBpInstruction = true;
                    }
                }

                if (!isBpInstruction)
                {
                    continue;
                }

                // Mark all globals that overlap with this offset as used
                // A global is used if the instruction accesses any byte within its range
                // Ranges are [start, end) where start is inclusive and end is exclusive
                foreach (GlobalVariable global in globals)
                {
                    int globalStart = global.StackOffset;
                    int globalEnd = global.StackOffset + global.Size;
                    int accessStart = offset;
                    int accessSize = 4; // Default size for INCxBP/DECxBP

                    // For CPTOPBP/CPDOWNBP, get the access size from args
                    if (inst.InsType == NCSInstructionType.CPTOPBP || inst.InsType == NCSInstructionType.CPDOWNBP)
                    {
                        if (inst.Args[1] is ushort sizeArg)
                        {
                            accessSize = sizeArg;
                        }
                        else if (inst.Args[1] is int sizeArgInt)
                        {
                            accessSize = sizeArgInt;
                        }
                    }

                    int accessEnd = offset + accessSize;

                    // Check if access range [accessStart, accessEnd) overlaps with global range [globalStart, globalEnd)
                    // Two ranges overlap if: accessStart < globalEnd && accessEnd > globalStart
                    // Note: Since offsets are negative, "less than" means more negative (further from 0)
                    if (accessStart < globalEnd && accessEnd > globalStart)
                    {
                        global.IsUsed = true;
                    }
                }
            }

            // Step 3: Identify unused globals and calculate offset adjustments
            var unusedGlobals = globals.Where(g => !g.IsUsed).ToList();
            if (unusedGlobals.Count == 0)
            {
                return; // All globals are used, nothing to remove
            }

            // Step 4: No need to pre-calculate adjustments - we'll calculate them on-the-fly
            // when adjusting BP-relative instructions

            // Step 5: Remove unused RSADD instructions
            var instructionsToRemove = new HashSet<NCSInstruction>();
            foreach (GlobalVariable unused in unusedGlobals)
            {
                instructionsToRemove.Add(unused.RsaddInstruction);
            }

            // Remove the instructions
            ncs.Instructions = ncs.Instructions.Where(inst => !instructionsToRemove.Contains(inst)).ToList();
            InstructionsCleared += instructionsToRemove.Count;

            // Step 6: Adjust all BP-relative instruction offsets
            // Re-find SAVEBP index after removals
            savebpIndex = -1;
            for (int i = 0; i < ncs.Instructions.Count; i++)
            {
                if (ncs.Instructions[i].InsType == NCSInstructionType.SAVEBP)
                {
                    savebpIndex = i;
                }
            }

            if (savebpIndex < 0)
            {
                return; // Should not happen, but safety check
            }

            // Adjust offsets in all BP-relative instructions after SAVEBP
            for (int i = savebpIndex + 1; i < ncs.Instructions.Count; i++)
            {
                NCSInstruction inst = ncs.Instructions[i];
                if (inst.Args.Count < 1)
                {
                    continue;
                }

                if (inst.InsType == NCSInstructionType.CPTOPBP || inst.InsType == NCSInstructionType.CPDOWNBP)
                {
                    if (inst.Args[0] is int offset)
                    {
                        // Calculate adjustment: sum of sizes of all removed globals that come
                        // BEFORE (more negative than) this offset
                        int adjustment = 0;
                        foreach (GlobalVariable removedGlobal in unusedGlobals)
                        {
                            // If the removed global is more negative (comes before) this offset,
                            // then this offset needs to be adjusted
                            if (removedGlobal.StackOffset < offset)
                            {
                                adjustment += removedGlobal.Size;
                            }
                        }

                        // Adjust the offset (make it less negative, i.e., add the adjustment)
                        inst.Args[0] = offset + adjustment;
                    }
                }
                else if (inst.InsType == NCSInstructionType.INCxBP || inst.InsType == NCSInstructionType.DECxBP)
                {
                    if (inst.Args[0] is int offset)
                    {
                        // Calculate adjustment: sum of sizes of all removed globals that come
                        // BEFORE (more negative than) this offset
                        int adjustment = 0;
                        foreach (GlobalVariable removedGlobal in unusedGlobals)
                        {
                            // If the removed global is more negative (comes before) this offset,
                            // then this offset needs to be adjusted
                            if (removedGlobal.StackOffset < offset)
                            {
                                adjustment += removedGlobal.Size;
                            }
                        }

                        // Adjust the offset (make it less negative, i.e., add the adjustment)
                        inst.Args[0] = offset + adjustment;
                    }
                }
            }
        }

        /// <summary>
        /// Check if an instruction type is an RSADD variant.
        /// </summary>
        private static bool IsRsaddInstruction(NCSInstructionType insType)
        {
            return insType == NCSInstructionType.RSADDI ||
                   insType == NCSInstructionType.RSADDF ||
                   insType == NCSInstructionType.RSADDS ||
                   insType == NCSInstructionType.RSADDO ||
                   insType == NCSInstructionType.RSADDEFF ||
                   insType == NCSInstructionType.RSADDEVT ||
                   insType == NCSInstructionType.RSADDLOC ||
                   insType == NCSInstructionType.RSADDTAL;
        }

        /// <summary>
        /// Get the size in bytes for an RSADD instruction type.
        /// All RSADD variants reserve 4 bytes except vectors (which use 3 RSADDF = 12 bytes).
        /// </summary>
        private static int GetRsaddSize(NCSInstructionType insType)
        {
            // All RSADD variants reserve 4 bytes on the stack
            // Vectors are represented as 3 consecutive RSADDF instructions (12 bytes total)
            return 4;
        }
    }
}

