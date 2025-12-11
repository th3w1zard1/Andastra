using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.Compiler.NSS;

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
            // Matching PyKotor classes.py lines 2906-2935 exactly
            // First compile the field access to push value to stack
            // Note: FieldAccess.Compile does NOT add to temp_stack, so we don't either
            DynamicDataType variableType = FieldAccess.Compile(ncs, root, block);

            if (variableType.Builtin != DataType.Int && variableType.Builtin != DataType.Float)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError(
                    $"Decrement operator (--) requires integer variable, got {variableType.Builtin.ToScriptString().ToLower()}\n" +
                    $"  Variable: {varName}");
            }

            // Get scoped info after compiling (matching PyKotor line 2917)
            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            int stackIndex = scoped.Offset;
            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError($"Cannot decrement const variable '{varName}'");
            }

            // Decrement the value on the stack (the value that was just pushed by FieldAccess.Compile)
            // Matching PyKotor line 2922: ncs.add(NCSInstructionType.DECxSP, args=[-4])
            // DECxSP with negative offset decrements the value at the top of the stack
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

            // Matching PyKotor line 2935: return variable_type
            // The decremented value is still on the stack (for assignment), so temp_stack is correct
            return variableType;
        }

        public override string ToString()
        {
            return $"--{FieldAccess}";
        }
    }
}

