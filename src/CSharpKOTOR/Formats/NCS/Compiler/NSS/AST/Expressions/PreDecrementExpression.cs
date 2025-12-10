using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS;

namespace CSharpKOTOR.Formats.NCS.Compiler
{

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
            // Matching PyKotor classes.py lines 2906-2935
            // First compile the field access to push value to stack
            DynamicDataType variableType = FieldAccess.Compile(ncs, root, block);
            // FieldAccess.Compile pushes value to stack but doesn't update temp_stack
            // We need to track it ourselves
            block.TempStack += variableType.Size(root);

            if (variableType.Builtin != DataType.Int && variableType.Builtin != DataType.Float)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new CompileError(
                    $"Decrement operator (--) requires integer variable, got {variableType.Builtin.ToScriptString().ToLower()}\n" +
                    $"  Variable: {varName}");
            }

            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            int stackIndex = scoped.Offset;
            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new CompileError($"Cannot decrement const variable '{varName}'");
            }

            // Decrement the value on the stack (the value that was just pushed by FieldAccess.Compile)
            // Matching PyKotor line 2922: ncs.add(NCSInstructionType.DECxSP, args=[-4])
            ncs.Add(NCSInstructionType.DECxSP, new List<object> { -variableType.Size(root) });

            // Copy the decremented value back to the variable location
            // Matching PyKotor lines 2924-2933
            if (isGlobal)
            {
                ncs.Add(NCSInstructionType.CPDOWNBP, new List<object> { stackIndex, variableType.Size(root) });
            }
            else
            {
                ncs.Add(NCSInstructionType.CPDOWNSP, new List<object> { stackIndex - variableType.Size(root), variableType.Size(root) });
            }

            return variableType;
        }

        public override string ToString()
        {
            return $"--{FieldAccess}";
        }
    }
}

