using System.Collections.Generic;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents a pre-decrement expression (--x).
/// </summary>
public class PreDecrementExpression : Expression
{
    public FieldAccess FieldAccess { get; set; }

    public PreDecrementExpression(FieldAccess fieldAccess)
    {
        FieldAccess = fieldAccess ?? throw new System.ArgumentNullException(nameof(fieldAccess));
    }

    public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
    {
        (bool isGlobal, DynamicDataType variableType, int stackIndex) = FieldAccess.GetScoped(block, root);

        if (variableType.Builtin != DataType.Int && variableType.Builtin != DataType.Float)
        {
            throw new CompileError(
                $"Decrement operator requires int or float type, got {variableType.Builtin.ToScriptString()}");
        }

        NCSInstructionType instructionType = isGlobal ? NCSInstructionType.DECxBP : NCSInstructionType.DECxSP;
        ncs.Add(instructionType, new List<object> { stackIndex });

        // Push decremented value onto stack
        NCSInstructionType copyInst = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;
        ncs.Add(copyInst, new List<object> { stackIndex, variableType.Size(root) });
        block.TempStack += variableType.Size(root);

        return variableType;
    }

    public override string ToString()
    {
        return $"--{FieldAccess}";
    }
}

