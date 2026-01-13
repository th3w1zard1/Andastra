using System.Collections.Generic;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST.Statements
{

    /// <summary>
    /// Represents a local variable declaration statement (supports multiple declarators).
    /// </summary>
    public class DeclarationStatement : Statement
    {
        public DynamicDataType DataType { get; set; }
        public List<VariableDeclarator> Declarators { get; set; }

        public DeclarationStatement(DynamicDataType dataType, List<VariableDeclarator> declarators)
        {
            DataType = dataType ?? throw new System.ArgumentNullException(nameof(dataType));
            Declarators = declarators ?? throw new System.ArgumentNullException(nameof(declarators));
        }

        public override object Compile(
            NCS ncs,
            CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            [CanBeNull] NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            foreach (VariableDeclarator declarator in Declarators)
            {
                declarator.Compile(ncs, root, block, DataType);
            }

            return DynamicDataType.VOID;
        }
    }
}

