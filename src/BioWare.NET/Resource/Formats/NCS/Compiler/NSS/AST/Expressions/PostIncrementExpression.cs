using System.Collections.Generic;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using System.Linq;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;

namespace BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST.Expressions
{

    /// <summary>
    /// Represents a post-increment expression (x++).
    /// </summary>
    public class PostIncrementExpression : Expression
    {
        public FieldAccess FieldAccess { get; set; }

        public PostIncrementExpression(FieldAccess fieldAccess)
        {
            FieldAccess = fieldAccess ?? throw new System.ArgumentNullException(nameof(fieldAccess));
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:2872-2899
        public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
        {
            // Matching PyKotor classes.py line 2877
            DynamicDataType variableType = FieldAccess.Compile(ncs, root, block);
            // Matching PyKotor classes.py line 2878
            block.TempStack += 4;

            // Matching PyKotor classes.py lines 2880-2886
            if (variableType != DynamicDataType.INT)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new CompileError(
                    $"Increment operator (++) requires integer variable, got {variableType.Builtin.ToScriptString()}\n" +
                    $"  Variable: {varName}");
            }

            // Matching PyKotor classes.py line 2888
            GetScopedResult scoped = FieldAccess.GetScoped(block, root);
            bool isGlobal = scoped.IsGlobal;
            int stackIndex = scoped.Offset;
            if (scoped.IsConst)
            {
                string varName = string.Join(".", FieldAccess.Identifiers.Select(i => i.Label));
                throw new NSS.CompileError($"Cannot increment const variable '{varName}'");
            }
            // Matching PyKotor classes.py lines 2893-2896
            if (isGlobal)
            {
                ncs.Add(NCSInstructionType.INCxBP, new List<object> { stackIndex });
            }
            else
            {
                ncs.Add(NCSInstructionType.INCxSP, new List<object> { stackIndex });
            }

            // Matching PyKotor classes.py line 2898
            block.TempStack -= 4;
            return variableType;
        }

        public override string ToString()
        {
            return $"{FieldAccess}++";
        }
    }
}

