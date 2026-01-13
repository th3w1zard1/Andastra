using System.Collections.Generic;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;

namespace BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST.Expressions
{

    /// <summary>
    /// Represents a logical NOT expression (!x).
    /// </summary>
    public class LogicalNotExpression : Expression
    {
        public Expression Operand { get; set; }

        public LogicalNotExpression(Expression operand)
        {
            Operand = operand ?? throw new System.ArgumentNullException(nameof(operand));
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            DynamicDataType operandType = Operand.Compile(ncs, root, block);
            block.TempStack += 4;

            if (operandType.Builtin != DataType.Int)
            {
                throw new CompileError(
                    $"Logical NOT requires integer operand, got {operandType.Builtin.ToScriptString()}\n" +
                    "  Note: In NWScript, only int types can be used in logical operations");
            }

            ncs.Add(NCSInstructionType.NOTI, new List<object>());

            block.TempStack -= 4;
            return new DynamicDataType(DataType.Int);
        }

        public override string ToString()
        {
            return $"!{Operand}";
        }
    }
}

