using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents a binary operation expression (e.g., a + b, x == y).
/// </summary>
public class BinaryOperatorExpression : Expression
{
    public Expression Left { get; set; }
    public Expression Right { get; set; }
    public Operator Operator { get; set; }
    public OperatorMapping OperatorMapping { get; set; }

    public BinaryOperatorExpression(
        Expression left,
        Expression right,
        Operator op,
        OperatorMapping operatorMapping)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));
        Operator = op;
        OperatorMapping = operatorMapping ?? throw new ArgumentNullException(nameof(operatorMapping));
    }

    public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
    {
        DynamicDataType leftType = Left.Compile(ncs, root, block);
        block.TempStack += leftType.Size(root);

        DynamicDataType rightType = Right.Compile(ncs, root, block);
        block.TempStack += rightType.Size(root);

        // Find matching operator mapping
        BinaryOperatorMapping? mapping = OperatorMapping.Binary.FirstOrDefault(m =>
            m.Lhs == leftType.Builtin && m.Rhs == rightType.Builtin);

        if (mapping == null)
        {
            throw new CompileError(
                $"No operator '{GetOperatorSymbol(Operator)}' defined for types " +
                $"{leftType.Builtin.ToScriptString()} and {rightType.Builtin.ToScriptString()}\n" +
                $"  Available combinations: {GetAvailableCombinations()}");
        }

        ncs.Add(mapping.Instruction, new List<object>());

        block.TempStack -= leftType.Size(root);
        block.TempStack -= rightType.Size(root);
        block.TempStack += new DynamicDataType(mapping.Result).Size(root);

        return new DynamicDataType(mapping.Result);
    }

    private static string GetOperatorSymbol(Operator op)
    {
        return op switch
        {
            Operator.ADDITION => "+",
            Operator.SUBTRACT => "-",
            Operator.MULTIPLY => "*",
            Operator.DIVIDE => "/",
            Operator.MODULUS => "%",
            Operator.EQUAL => "==",
            Operator.NOT_EQUAL => "!=",
            Operator.GREATER_THAN => ">",
            Operator.LESS_THAN => "<",
            Operator.GREATER_THAN_OR_EQUAL => ">=",
            Operator.LESS_THAN_OR_EQUAL => "<=",
            Operator.AND => "&&",
            Operator.OR => "||",
            Operator.BITWISE_AND => "&",
            Operator.BITWISE_OR => "|",
            Operator.BITWISE_XOR => "^",
            Operator.BITWISE_LEFT => "<<",
            Operator.BITWISE_RIGHT => ">>",
            _ => op.ToString()
        };
    }

    private string GetAvailableCombinations()
    {
        IEnumerable<string> combinations = OperatorMapping.Binary
            .Select(m => $"{m.Lhs.ToScriptString()} {GetOperatorSymbol(Operator)} {m.Rhs.ToScriptString()}")
            .Take(5);
        return string.Join(", ", combinations);
    }

    public override string ToString()
    {
        return $"({Left} {GetOperatorSymbol(Operator)} {Right})";
    }
}

