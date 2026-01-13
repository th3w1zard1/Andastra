using System.Collections.Generic;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST.Statements
{

    /// <summary>
    /// Represents a NOP (no operation) statement.
    /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2321
    /// Original: class NopStatement(Statement):
    /// </summary>
    public class NopStatement : Statement
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2322
        // Original: def __init__(self, string: str):
        public string String { get; }

        public NopStatement(string str)
        {
            String = str;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2326
        // Original: def compile(self, ncs: NCS, root: CodeRoot, block: CodeBlock, return_instruction: NCSInstruction, break_instruction: NCSInstruction | None, continue_instruction: NCSInstruction | None) -> DynamicDataType:
        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2335
            // Original: ncs.add(NCSInstructionType.NOP, args=[self.string])
            ncs.Add(NCSInstructionType.NOP, new List<object> { String });
            return DynamicDataType.VOID;
        }
    }
}

