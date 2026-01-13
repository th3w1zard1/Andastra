using System.Collections.Generic;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;

namespace BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST.Expressions
{

    /// <summary>
    /// Represents an object literal expression (OBJECT_SELF, OBJECT_INVALID, or object ID).
    /// </summary>
    public class ObjectExpression : Expression
    {
        public int Value { get; set; }

        public ObjectExpression(int value)
        {
            Value = value;
        }

        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            ncs.Add(NCSInstructionType.CONSTO, new List<object> { Value });
            return new DynamicDataType(DataType.Object);
        }

        public override string ToString()
        {
            switch (Value)
            {
                case 0:
                    return "OBJECT_SELF";
                case 1:
                    return "OBJECT_INVALID";
                default:
                    return $"Object({Value})";
            }
        }
    }
}

