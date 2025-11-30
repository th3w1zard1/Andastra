using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace TSLPatcher.Core.Formats.NCS
{

    /// <summary>
    /// Represents a compiled NWScript bytecode program.
    ///
    /// NCS contains a sequence of bytecode instructions that implement NWScript logic.
    /// Instructions are executed sequentially by a stack-based virtual machine, with
    /// control flow instructions (JMP, JSR, JZ, JNZ) allowing jumps to other instructions.
    ///
    /// References:
    ///     vendor/reone/include/reone/script/program.h - ScriptProgram class
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp:34-40 (program creation)
    ///     vendor/xoreos/src/aurora/nwscript/ncsfile.h:86-280 - NCSFile class
    ///     vendor/Kotor.NET/Kotor.NET/Formats/KotorNCS/NCS.cs:9-17 - NCS class
    /// </summary>
    public class NCS : IEquatable<NCS>
    {
        public List<NCSInstruction> Instructions { get; set; }

        public NCS()
        {
            Instructions = new List<NCSInstruction>();
        }

        public NCSInstruction Add(
            [CanBeNull] NCSInstructionType instructionType,
            [CanBeNull] List<object> args = null,
            [CanBeNull] NCSInstruction jump = null,
            [CanBeNull] int? index = null)
        {
            var instruction = new NCSInstruction(instructionType, args, jump);
            if (!(index is null) && index.HasValue)
            {
                Instructions.Insert(index.Value, instruction);
            }
            else
            {
                Instructions.Add(instruction);
            }
            return instruction;
        }

        public List<NCSInstruction> LinksTo(NCSInstruction target)
        {
            return Instructions.Where(inst => ReferenceEquals(inst.Jump, target)).ToList();
        }

        public void Optimize(List<INCSOptimizer> optimizers)
        {
            foreach (INCSOptimizer optimizer in optimizers)
            {
                optimizer.Optimize(this);
            }
        }

        public void Merge(NCS other)
        {
            Instructions.AddRange(other.Instructions);
        }

        public List<string> Validate()
        {
            var issues = new List<string>();

            for (int i = 0; i < Instructions.Count; i++)
            {
                NCSInstruction inst = Instructions[i];
                if (inst.Jump != null && !Instructions.Contains(inst.Jump))
                {
                    issues.Add($"Instruction #{i} ({inst.InsType}) jumps to instruction not in list");
                }
            }

            var jumpRequired = new HashSet<NCSInstructionType>
        {
            NCSInstructionType.JMP,
            NCSInstructionType.JSR,
            NCSInstructionType.JZ,
            NCSInstructionType.JNZ
        };

            for (int i = 0; i < Instructions.Count; i++)
            {
                NCSInstruction inst = Instructions[i];
                if (jumpRequired.Contains(inst.InsType) && inst.Jump == null)
                {
                    issues.Add($"Instruction #{i} ({inst.InsType}) requires jump but has none");
                }
            }

            // Check for missing required arguments
            for (int i = 0; i < Instructions.Count; i++)
            {
                NCSInstruction inst = Instructions[i];
                int? expectedArgs = GetExpectedArgCount(inst.InsType);
                if (expectedArgs.HasValue && inst.Args.Count != expectedArgs.Value)
                {
                    issues.Add($"Instruction #{i} ({inst.InsType}) has {inst.Args.Count} args, expected {expectedArgs.Value}");
                }
            }

            return issues;
        }

        [CanBeNull]
        public NCSInstruction GetInstructionAtIndex(int index)
        {
            return index >= 0 && index < Instructions.Count ? Instructions[index] : null;
        }

        public int GetInstructionIndex(NCSInstruction instruction)
        {
            return Instructions.IndexOf(instruction);
        }

        public HashSet<NCSInstruction> GetReachableInstructions()
        {
            var reachable = new HashSet<NCSInstruction>();
            if (Instructions.Count == 0)
            {
                return reachable;
            }

            var toCheck = new Queue<NCSInstruction>();
            toCheck.Enqueue(Instructions[0]);

            while (toCheck.Count > 0)
            {
                NCSInstruction inst = toCheck.Dequeue();
                if (reachable.Contains(inst))
                {
                    continue;
                }

                reachable.Add(inst);

                int instIdx = GetInstructionIndex(inst);
                if (instIdx >= 0 && instIdx + 1 < Instructions.Count)
                {
                    NCSInstruction nextInst = Instructions[instIdx + 1];
                    if (!reachable.Contains(nextInst))
                    {
                        toCheck.Enqueue(nextInst);
                    }
                }

                if (inst.Jump != null && !reachable.Contains(inst.Jump))
                {
                    toCheck.Enqueue(inst.Jump);
                }

                if (inst.InsType == NCSInstructionType.JZ || inst.InsType == NCSInstructionType.JNZ)
                {
                    if (instIdx >= 0 && instIdx + 1 < Instructions.Count)
                    {
                        NCSInstruction nextInst = Instructions[instIdx + 1];
                        if (!reachable.Contains(nextInst))
                        {
                            toCheck.Enqueue(nextInst);
                        }
                    }
                }
            }

            return reachable;
        }

        /// <summary>
        /// Partition instructions into basic blocks for decompilation.
        ///
        /// A basic block is a sequence of instructions with a single entry point
        /// and a single exit point (no jumps into the middle, no branches except at end).
        /// </summary>
        public List<List<NCSInstruction>> GetBasicBlocks()
        {
            var blocks = new List<List<NCSInstruction>>();
            if (Instructions.Count == 0)
            {
                return blocks;
            }

            var currentBlock = new List<NCSInstruction>();
            var jumpTargets = new HashSet<NCSInstruction>();
            foreach (NCSInstruction inst in Instructions)
            {
                if (inst.Jump != null)
                {
                    jumpTargets.Add(inst.Jump);
                }
            }

            for (int i = 0; i < Instructions.Count; i++)
            {
                NCSInstruction inst = Instructions[i];
                // Start new block if this is a jump target
                if (jumpTargets.Contains(inst) && currentBlock.Count > 0)
                {
                    blocks.Add(currentBlock);
                    currentBlock = new List<NCSInstruction> { inst };
                }
                else
                {
                    currentBlock.Add(inst);
                }

                // End block if this instruction branches
                if (inst.IsControlFlow() && inst.InsType != NCSInstructionType.JSR)
                {
                    blocks.Add(currentBlock);
                    currentBlock = new List<NCSInstruction>();
                }
            }

            // Add final block
            if (currentBlock.Count > 0)
            {
                blocks.Add(currentBlock);
            }

            return blocks;
        }

        /// <summary>
        /// Get expected argument count for instruction type, or null if variable/complex.
        /// </summary>
        private static int? GetExpectedArgCount(NCSInstructionType insType)
        {
            // Instructions with 2 args
            if (insType == NCSInstructionType.CPDOWNSP ||
                insType == NCSInstructionType.CPTOPSP ||
                insType == NCSInstructionType.CPDOWNBP ||
                insType == NCSInstructionType.CPTOPBP ||
                insType == NCSInstructionType.ACTION ||
                insType == NCSInstructionType.STORE_STATE)
            {
                return 2;
            }
            // Instructions with 1 arg
            if (insType == NCSInstructionType.CONSTI ||
                insType == NCSInstructionType.CONSTF ||
                insType == NCSInstructionType.CONSTS ||
                insType == NCSInstructionType.CONSTO ||
                insType == NCSInstructionType.MOVSP ||
                insType == NCSInstructionType.DECxSP ||
                insType == NCSInstructionType.INCxSP ||
                insType == NCSInstructionType.DECxBP ||
                insType == NCSInstructionType.INCxBP)
            {
                return 1;
            }
            // Instructions with 3 args
            if (insType == NCSInstructionType.DESTRUCT)
            {
                return 3;
            }
            // Most other instructions have 0 args
            if (insType == NCSInstructionType.RETN ||
                insType == NCSInstructionType.NOP ||
                insType == NCSInstructionType.SAVEBP ||
                insType == NCSInstructionType.RESTOREBP ||
                insType == NCSInstructionType.NOTI ||
                insType == NCSInstructionType.COMPI)
            {
                return 0;
            }
            // Complex/variable - return null
            return null;
        }

        public bool Equals([CanBeNull] NCS other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Instructions.Count != other.Instructions.Count)
            {
                return false;
            }

            var selfIndexMap = Instructions.Select((inst, idx) => (inst, idx)).ToDictionary(x => x.inst, x => x.idx);
            var otherIndexMap = other.Instructions.Select((inst, idx) => (inst, idx)).ToDictionary(x => x.inst, x => x.idx);

            for (int i = 0; i < Instructions.Count; i++)
            {
                NCSInstruction instruction = Instructions[i];
                NCSInstruction otherInstruction = other.Instructions[i];

                if (instruction.InsType != otherInstruction.InsType)
                {
                    return false;
                }
                if (!instruction.Args.SequenceEqual(otherInstruction.Args))
                {
                    return false;
                }

                if ((instruction.Jump == null) != (otherInstruction.Jump == null))
                {
                    return false;
                }

                if (instruction.Jump != null && otherInstruction.Jump != null)
                {
                    if (!selfIndexMap.TryGetValue(instruction.Jump, out int jumpIdx) ||
                        !otherIndexMap.TryGetValue(otherInstruction.Jump, out int otherJumpIdx) ||
                        jumpIdx != otherJumpIdx)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals([CanBeNull] object obj)
        {
            return obj is NCS other && Equals(other);
        }

        public override int GetHashCode()
        {
            var indexMap = Instructions.Select((inst, idx) => (inst, idx)).ToDictionary(x => x.inst, x => x.idx);
            var signature = new List<(NCSInstructionType, int, int?)>();

            foreach (NCSInstruction instruction in Instructions)
            {
                int? jumpIndex = null;
                if (instruction.Jump != null && indexMap.TryGetValue(instruction.Jump, out int idx))
                {
                    jumpIndex = idx;
                }
                signature.Add((instruction.InsType, instruction.Args.Count, jumpIndex));
            }

            return signature.Aggregate(0, (hash, item) => HashCode.Combine(hash, item));
        }

        public override string ToString()
        {
            if (Instructions.Count == 0)
            {
                return "NCS (empty - no instructions)";
            }

            return $"NCS with {Instructions.Count} instructions";
        }

        public void Print()
        {
            for (int i = 0; i < Instructions.Count; i++)
            {
                NCSInstruction instruction = Instructions[i];
                if (instruction.Jump != null)
                {
                    int jumpIndex = Instructions.IndexOf(instruction.Jump);
                    Console.WriteLine($"{i}:\t{instruction.InsType,-15} --> {jumpIndex}");
                }
                else
                {
                    string argsStr = instruction.Args.Count > 0 ? $" [{string.Join(", ", instruction.Args)}]" : "";
                    Console.WriteLine($"{i}:\t{instruction.InsType,-15}{argsStr}");
                }
            }
        }
    }
}

