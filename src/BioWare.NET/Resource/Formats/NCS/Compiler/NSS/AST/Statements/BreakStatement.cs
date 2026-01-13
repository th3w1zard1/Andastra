using System.Collections.Generic;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using JetBrains.Annotations;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;

namespace BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST.Statements
{

    /// <summary>
    /// Represents a break statement in a loop or switch.
    /// </summary>
    public class BreakStatement : Statement
    {
        [CanBeNull]
        public NCSInstruction JumpTarget { get; set; }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            if (breakInstruction == null)
            {
                throw new CompileError("break statement not inside loop or switch");
            }

            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -block.BreakScopeSize(root) });
            ncs.Add(NCSInstructionType.JMP, jump: breakInstruction);
            return DynamicDataType.VOID;
        }
    }
}

