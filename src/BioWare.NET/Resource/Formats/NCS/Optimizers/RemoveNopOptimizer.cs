using System.Collections.Generic;
using System.Linq;

namespace BioWare.NET.Resource.Formats.NCS.Optimizers
{

    /// <summary>
    /// Removes NOP (no-operation) instructions from compiled NCS bytecode.
    ///
    /// NCS Compiler uses NOP instructions as stubs to simplify the compilation process
    /// however as their name suggests they do not perform any actual function. This optimizer
    /// removes all occurrences of NOP instructions from the compiled script, updating jump
    /// targets to skip over removed NOPs.
    ///
    /// References:
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS optimization patterns)
    ///     Standard compiler optimization techniques (dead code elimination)
    ///     Note: NOP removal is a common bytecode optimization
    /// </summary>
    public class RemoveNopOptimizer : NCSOptimizer
    {
        public override void Optimize(NCS ncs)
        {
            var nops = ncs.Instructions.Where(inst => inst.InsType == NCSInstructionType.NOP).ToList();

            if (nops.Count == 0)
            {
                return;
            }

            var removable = new HashSet<NCSInstruction>(new ReferenceInstructionComparer());
            bool debug = System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true";

            foreach (NCSInstruction nop in nops)
            {
                int nopIndex = ncs.GetInstructionIndex(nop);
                if (nopIndex < 0)
                {
                    continue;
                }

                // Find replacement instruction (next non-NOP instruction after this NOP)
                // Based on PyKotor implementation: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/optimizers.py
                // The replacement is the first non-NOP instruction following the NOP
                // If no replacement exists, we cannot safely retarget jumps (function entry stub case)
                NCSInstruction replacement = null;
                for (int i = nopIndex + 1; i < ncs.Instructions.Count; i++)
                {
                    NCSInstruction candidate = ncs.Instructions[i];
                    if (candidate.InsType != NCSInstructionType.NOP)
                    {
                        replacement = candidate;
                        break;
                    }
                }

                // Get all instructions that jump to this NOP
                // Based on PyKotor implementation: inbound_links = ncs.links_to(nop)
                // LinksTo returns all instructions whose Jump property points to this NOP
                List<NCSInstruction> inboundLinks = ncs.LinksTo(nop);

                // If there are inbound links (instructions jump to this NOP), we need to handle retargeting
                // Based on PyKotor implementation: if inbound_links and replacement is None: keep NOP
                // If there are inbound links AND no replacement, keep this NOP (likely a function entry stub)
                // This is the case where a NOP is at the end of the instruction list and is a jump target
                // Function entry stubs are NOPs that serve as placeholders for function entry points
                // and cannot be safely removed if they're the last instruction (no replacement available)
                if (inboundLinks.Count > 0 && replacement == null)
                {
                    // Cannot safely retarget jumps - keep this NOP (function entry stub case)
                    // Based on PyKotor: "We cannot safely retarget inbound jumps, so keep this NOP."
                    if (debug)
                    {
                        System.Console.WriteLine($"RemoveNop: keeping NOP idx={nopIndex} inbound={inboundLinks.Count} (no replacement available - function entry stub)");
                    }
                    continue;
                }

                // If there are inbound links but we have a replacement, retarget all jumps to the replacement
                // Based on PyKotor implementation: for link in inbound_links: link.jump = replacement
                // This ensures that all instructions that previously jumped to the NOP now jump to the replacement
                // This is safe because the replacement is the next non-NOP instruction, so control flow is preserved
                if (inboundLinks.Count > 0 && replacement != null)
                {
                    // Retarget all inbound links to the replacement instruction
                    // Based on RemoveMoveSPEqualsZeroOptimizer pattern: link.Jump = replacement
                    foreach (NCSInstruction link in inboundLinks)
                    {
                        link.Jump = replacement;
                    }
                    if (debug)
                    {
                        System.Console.WriteLine($"RemoveNop: retargeting {inboundLinks.Count} jump(s) from NOP idx={nopIndex} to replacement idx={ncs.GetInstructionIndex(replacement)}");
                    }
                }

                // Mark NOP as removable (either no inbound links, or inbound links have been retargeted)
                // Based on PyKotor implementation: removable_ids.add(id(nop))
                removable.Add(nop);
                if (debug)
                {
                    System.Console.WriteLine($"RemoveNop: removing NOP idx={nopIndex} replacementIdx={ncs.GetInstructionIndex(replacement)}");
                }
            }

            if (removable.Count == 0)
            {
                return;
            }

            ncs.Instructions = ncs.Instructions.Where(inst => !removable.Contains(inst)).ToList();
            InstructionsCleared += removable.Count;
        }
    }

    internal sealed class ReferenceInstructionComparer : IEqualityComparer<NCSInstruction>
    {
        public bool Equals(NCSInstruction x, NCSInstruction y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(NCSInstruction obj)
        {
            return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}

