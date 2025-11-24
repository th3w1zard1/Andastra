using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST;

/// <summary>
/// Represents a field access chain (e.g., myStruct.field, myVector.x).
/// </summary>
public class FieldAccess : Expression
{
    public List<Identifier> Identifiers { get; set; }

    public FieldAccess(List<Identifier> identifiers)
    {
        Identifiers = identifiers ?? throw new ArgumentNullException(nameof(identifiers));

        if (!Identifiers.Any())
        {
            throw new ArgumentException("FieldAccess must have at least one identifier", nameof(identifiers));
        }
    }

    /// <summary>
    /// Get scoped variable information for this field access.
    /// </summary>
    public (bool IsGlobal, DynamicDataType DataType, int Offset) GetScoped(CodeBlock? block, CodeRoot root)
    {
        if (!Identifiers.Any())
        {
            throw new CompileError("Internal error: FieldAccess has no identifiers");
        }

        Identifier first = Identifiers[0];
        (bool isGlobal, DynamicDataType dataType, int offset) = root.GetScoped(first, block);

        // Process remaining identifiers as member accesses
        foreach (Identifier? nextIdent in Identifiers.Skip(1))
        {
            if (dataType.Builtin == DataType.Vector)
            {
                dataType = new DynamicDataType(DataType.Float);

                if (nextIdent.Label == "x")
                {
                    offset += 0;
                }
                else if (nextIdent.Label == "y")
                {
                    offset += 4;
                }
                else if (nextIdent.Label == "z")
                {
                    offset += 8;
                }
                else
                {
                    throw new CompileError(
                        $"Attempting to access unknown member '{nextIdent}' on vector.\n" +
                        "  Valid members: x, y, z");
                }
            }
            else if (dataType.Builtin == DataType.Struct)
            {
                if (dataType.Struct == null)
                {
                    throw new CompileError($"Internal error: Struct datatype has no struct name");
                }

                if (!root.StructMap.TryGetValue(dataType.Struct, out Struct? structDef))
                {
                    throw new CompileError($"Unknown struct type: {dataType.Struct}");
                }

                offset += structDef.ChildOffset(root, nextIdent);
                dataType = structDef.ChildType(root, nextIdent);
            }
            else
            {
                throw new CompileError(
                    $"Attempting to access member '{nextIdent}' on non-composite type '{dataType.Builtin.ToScriptString()}'.\n" +
                    "  Only struct and vector types have members.");
            }
        }

        return (isGlobal, dataType, offset);
    }

    public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
    {
        (bool isGlobal, DynamicDataType variableType, int stackIndex) = GetScoped(block, root);
        NCSInstructionType instructionType = isGlobal ? NCSInstructionType.CPTOPBP : NCSInstructionType.CPTOPSP;

        ncs.Add(instructionType, new List<object> { stackIndex, variableType.Size(root) });
        block.TempStack += variableType.Size(root);

        return variableType;
    }

    public override string ToString()
    {
        return string.Join(".", Identifiers.Select(i => i.Label));
    }
}

