using System.Collections.Generic;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents a post-decrement expression (x--).
/// </summary>
public class PostDecrementExpression : Expression
{
    public FieldAccess FieldAccess { get; set; }

    public PostDecrementExpression(FieldAccess fieldAccess)
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

        // Push current value onto stack first (before decrement)
        NCSInstructionType copyInst = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;
        ncs.Add(copyInst, new List<object> { stackIndex, variableType.Size(root) });
        block.TempStack += variableType.Size(root);

        // Then decrement the variable
        NCSInstructionType instructionType = isGlobal ? NCSInstructionType.DECxBP : NCSInstructionType.DECxSP;
        ncs.Add(instructionType, new List<object> { stackIndex });

        return variableType;
    }

    public override string ToString()
    {
        return $"{FieldAccess}--";
    }
}

