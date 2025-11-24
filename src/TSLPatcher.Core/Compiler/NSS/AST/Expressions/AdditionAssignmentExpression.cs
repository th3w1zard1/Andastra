using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents an addition assignment expression (x += y).
/// </summary>
public class AdditionAssignmentExpression : Expression
{
    public FieldAccess FieldAccess { get; set; }
    public Expression Value { get; set; }

    public AdditionAssignmentExpression(FieldAccess fieldAccess, Expression value)
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
            arithmeticInstruction = NCSInstructionType.ADDII;
        }
        else if (variableType.Builtin == DataType.Int && expressionType.Builtin == DataType.Float)
        {
            arithmeticInstruction = NCSInstructionType.ADDIF;
        }
        else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Float)
        {
            arithmeticInstruction = NCSInstructionType.ADDFF;
        }
        else if (variableType.Builtin == DataType.Float && expressionType.Builtin == DataType.Int)
        {
            arithmeticInstruction = NCSInstructionType.ADDFI;
        }
        else if (variableType.Builtin == DataType.String && expressionType.Builtin == DataType.String)
        {
            arithmeticInstruction = NCSInstructionType.ADDSS;
        }
        else if (variableType.Builtin == DataType.Vector && expressionType.Builtin == DataType.Vector)
        {
            arithmeticInstruction = NCSInstructionType.ADDVV;
        }
        else
        {
            string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
            throw new CompileError(
                $"Type mismatch in += operation on '{varName}'\n" +
                $"  Variable type: {variableType.Builtin.ToScriptString()}\n" +
                $"  Expression type: {expressionType.Builtin.ToScriptString()}\n" +
                "  Supported: int+=int, float+=float/int, string+=string, vector+=vector");
        }

        // Add the expression and our temp variable copy together
        ncs.Add(arithmeticInstruction, new List<object>());

        // Copy the result to the original variable in the stack
        NCSInstructionType insCpDown = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
        int offsetCpDown = isGlobal ? stackIndex : stackIndex - expressionType.Size(root);
        ncs.Add(insCpDown, new List<object> { offsetCpDown, variableType.Size(root) });

        block.TempStack -= variableType.Size(root);
        block.TempStack -= expressionType.Size(root);

        return expressionType;
    }

    public override string ToString()
    {
        return $"{FieldAccess} += {Value}";
    }
}

