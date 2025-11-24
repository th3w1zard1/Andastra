using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents an assignment expression (field = value).
/// </summary>
public class AssignmentExpression : Expression
{
    public FieldAccess FieldAccess { get; set; }
    public Expression Value { get; set; }

    public AssignmentExpression(FieldAccess fieldAccess, Expression value)
    {
        FieldAccess = fieldAccess ?? throw new ArgumentNullException(nameof(fieldAccess));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
    {
        // Compile the value expression first
        DynamicDataType valueType = Value.Compile(ncs, root, block);

        // Get the field information
        (bool isGlobal, DynamicDataType fieldType, int stackIndex) = FieldAccess.GetScoped(block, root);

        // Determine the copy instruction based on scope
        NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;

        // Adjust offset for local variables
        if (!isGlobal)
        {
            stackIndex -= valueType.Size(root);
        }

        // Track the value pushed onto the stack
        block.TempStack += valueType.Size(root);

        // Type check
        if (valueType != fieldType)
        {
            string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
            throw new CompileError(
                $"Type mismatch in assignment to '{varName}'\n" +
                $"  Variable type: {fieldType.Builtin.ToScriptString()}\n" +
                $"  Expression type: {valueType.Builtin.ToScriptString()}");
        }

        // Copy the value to the variable's location
        ncs.Add(instructionType, new List<object> { stackIndex, fieldType.Size(root) });

        block.TempStack -= valueType.Size(root);

        return valueType;
    }

    public override string ToString()
    {
        return $"{FieldAccess} = {Value}";
    }
}

