using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents a division assignment expression (x /= y).
/// </summary>
public class DivisionAssignmentExpression : Expression
{
    public FieldAccess FieldAccess { get; set; }
    public Expression Value { get; set; }

    public DivisionAssignmentExpression(FieldAccess fieldAccess, Expression value)
    {
        FieldAccess = fieldAccess ?? throw new ArgumentNullException(nameof(fieldAccess));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
    {
        // Copy the variable to the top of the stack
        (bool isGlobal, DynamicDataType variableType, int stackIndex) = FieldAccess.GetScoped(block, root);
        NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;
        ncs.Add(instructionType, new List<object> { stackIndex, variableType.Size(root) });
        block.TempStack += variableType.Size(root);

        // Add the result of the expression to the stack
        DynamicDataType expressionType = Value.Compile(ncs, root, block);
        block.TempStack += expressionType.Size(root);

        // Determine what instruction to apply to the two values
        NCSInstructionType arithmeticInstruction;
        if (variableType.Builtin == DataType.Int && expressionType.Builtin == DataType.Int)
        {
            arithmeticInstruction = NCSInstructionType.DIVII;
        }
        else if (variableType.Builtin == DataType.Int && expressionType.Builtin == DataType.Float)
        {
            arithmeticInstruction = NCSInstructionType.DIVIF;
        }
        else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Float)
        {
            arithmeticInstruction = NCSInstructionType.DIVFF;
        }
        else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Int)
        {
            arithmeticInstruction = NCSInstructionType.DIVFI;
        }
        else if (variableType.Builtin == DataType.Vector && expressionType.Builtin == DataType.Float)
        {
            arithmeticInstruction = NCSInstructionType.DIVVF;
        }
        else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Vector)
        {
            arithmeticInstruction = NCSInstructionType.DIVFV;
        }
        else
        {
            string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
            throw new CompileError(
                $"Type mismatch in /= operation on '{varName}'\n" +
                $"  Variable type: {variableType.Builtin.ToScriptString()}\n" +
                $"  Expression type: {expressionType.Builtin.ToScriptString()}\n" +
                "  Supported: int/=int, float/=float/int, vector/=float, float/=vector");
        }

        // Divide the variable by the expression
        ncs.Add(arithmeticInstruction, new List<object>());

        // Copy the result to the original variable in the stack
        NCSInstructionType insCpDown = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
        int offsetCpDown = isGlobal ? stackIndex : stackIndex - expressionType.Size(root);
        ncs.Add(insCpDown, new List<object> { offsetCpDown, variableType.Size(root) });

        block.TempStack -= expressionType.Size(root);
        block.TempStack -= variableType.Size(root);

        return expressionType;
    }

    public override string ToString()
    {
        return $"{FieldAccess} /= {Value}";
    }
}

