using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using TSLPatcher.Core.Common.Script;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{


    // Note: EmptyStatement, NopStatement, ExpressionStatement, DeclarationStatement are defined in NSS/AST/Statements/

    // Variable Declaration helper class (used by DeclarationStatement)

    public class VariableDeclarator
    {
        public Identifier Identifier { get; }
        [CanBeNull] public Expression Initializer { get; }

        public VariableDeclarator(Identifier identifier, [CanBeNull] Expression initializer = null)
        {
            Identifier = identifier;
            Initializer = initializer;
        }

        public void Compile(NCS ncs, CodeRoot root, CodeBlock block, DynamicDataType declaredType)
        {
            // Reserve stack space
            switch (declaredType.Builtin)
            {
                case DataType.Int: ncs.Add(NCSInstructionType.RSADDI); break;
                case DataType.Float: ncs.Add(NCSInstructionType.RSADDF); break;
                case DataType.String: ncs.Add(NCSInstructionType.RSADDS); break;
                case DataType.Object: ncs.Add(NCSInstructionType.RSADDO); break;
                case DataType.Vector:
                    ncs.Add(NCSInstructionType.RSADDF);
                    ncs.Add(NCSInstructionType.RSADDF);
                    ncs.Add(NCSInstructionType.RSADDF);
                    break;
                case DataType.Struct:
                    string structName = declaredType.Struct ?? throw new CompileError("Struct has no name");
                    root.StructMap[structName].Initialize(ncs, root);
                    break;
                default:
                    throw new CompileError($"Unsupported local variable type: {declaredType.Builtin}");
            }

            block.AddScoped(Identifier, declaredType);

            // Handle initializer
            if (Initializer != null)
            {
                DynamicDataType initType = Initializer.Compile(ncs, root, block);
                if (initType != declaredType)
                {
                    throw new CompileError($"Type mismatch in variable '{Identifier}' initializer");
                }

                GetScopedResult scoped = block.GetScoped(Identifier, root);
                NCSInstructionType instruction = scoped.IsGlobal ? NCSInstructionType.CPDOWNBP : NCSInstructionType.CPDOWNSP;
                ncs.Add(instruction, new List<object> { scoped.Offset - initType.Size(root), initType.Size(root) });
            }
        }
    }


    // If / Else
    public class ConditionalBlock : Statement
    {
        public List<ConditionAndBlock> IfBlocks { get; }
        [CanBeNull] public CodeBlock ElseBlock { get; }

        public ConditionalBlock(List<ConditionAndBlock> ifBlocks, [CanBeNull] CodeBlock elseBlock)
        {
            IfBlocks = ifBlocks;
            ElseBlock = elseBlock;
        }

        public override object Compile(
            NCS ncs,
            [CanBeNull] CodeRoot root,
            CodeBlock block,
            NCSInstruction returnInstruction,
            NCSInstruction breakInstruction,
            [CanBeNull] NCSInstruction continueInstruction)
        {
            var jumpTargets = new List<NCSInstruction>();
            for (int i = 0; i < IfBlocks.Count + 1; i++)
            {
                jumpTargets.Add(new NCSInstruction(NCSInstructionType.NOP));
            }

            for (int i = 0; i < IfBlocks.Count; i++)
            {
                ConditionAndBlock branch = IfBlocks[i];
                branch.Condition.Compile(ncs, root, block);
                ncs.Add(NCSInstructionType.JZ, jump: jumpTargets[i]);

                branch.Block.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
                ncs.Add(NCSInstructionType.JMP, jump: jumpTargets[jumpTargets.Count - 1]);
                ncs.Instructions.Add(jumpTargets[i]);
            }

            ElseBlock?.Compile(ncs, root, block, returnInstruction, breakInstruction, continueInstruction);
            ncs.Instructions.Add(jumpTargets[jumpTargets.Count - 1]);

            return DynamicDataType.VOID;
        }
    }

    public class ConditionAndBlock
    {
        public Expression Condition { get; }
        public CodeBlock Block { get; }

        public ConditionAndBlock(Expression condition, CodeBlock block)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Block = block ?? throw new ArgumentNullException(nameof(block));
        }
    }

    // While / Do-While / For
    public class WhileLoopBlock : Statement
    {
        public Expression Condition { get; }
        public CodeBlock Block { get; }

        public WhileLoopBlock(Expression condition, CodeBlock block)
        {
            Condition = condition;
            Block = block;
        }

        public override object Compile(NCS ncs, [CanBeNull] CodeRoot root, CodeBlock block, NCSInstruction returnInstruction, NCSInstruction breakInstruction, [CanBeNull] NCSInstruction continueInstruction)
        {
            block.MarkBreakScope();

            NCSInstruction loopStart = ncs.Add(NCSInstructionType.NOP);
            var loopEnd = new NCSInstruction(NCSInstructionType.NOP);

            Condition.Compile(ncs, root, block);
            ncs.Add(NCSInstructionType.JZ, jump: loopEnd);

            Block.Compile(ncs, root, block, returnInstruction, loopEnd, loopStart);
            ncs.Add(NCSInstructionType.JMP, jump: loopStart);
            ncs.Instructions.Add(loopEnd);

            return DynamicDataType.VOID;
        }
    }

    public class DoWhileLoopBlock : Statement
    {
        public Expression Condition { get; }
        public CodeBlock Block { get; }

        public DoWhileLoopBlock(Expression condition, CodeBlock block)
        {
            Condition = condition;
            Block = block;
        }

        public override object Compile(NCS ncs, [CanBeNull] CodeRoot root, CodeBlock block, NCSInstruction returnInstruction, NCSInstruction breakInstruction, [CanBeNull] NCSInstruction continueInstruction)
        {
            block.MarkBreakScope();

            NCSInstruction loopStart = ncs.Add(NCSInstructionType.NOP);
            var conditionStart = new NCSInstruction(NCSInstructionType.NOP);
            var loopEnd = new NCSInstruction(NCSInstructionType.NOP);

            Block.Compile(ncs, root, block, returnInstruction, loopEnd, conditionStart);
            ncs.Instructions.Add(conditionStart);

            Condition.Compile(ncs, root, block);
            ncs.Add(NCSInstructionType.JZ, jump: loopEnd);
            ncs.Add(NCSInstructionType.JMP, jump: loopStart);
            ncs.Instructions.Add(loopEnd);

            return DynamicDataType.VOID;
        }
    }

    public class ForLoopBlock : Statement
    {
        public Expression Initializer { get; }
        public Expression Condition { get; }
        public Expression Iteration { get; }
        public CodeBlock Block { get; }

        public ForLoopBlock(Expression initializer, Expression condition, Expression iteration, CodeBlock block)
        {
            Initializer = initializer;
            Condition = condition;
            Iteration = iteration;
            Block = block;
        }

        public override object Compile(NCS ncs, [CanBeNull] CodeRoot root, CodeBlock block, NCSInstruction returnInstruction, NCSInstruction breakInstruction, [CanBeNull] NCSInstruction continueInstruction)
        {
            block.MarkBreakScope();

            DynamicDataType initType = Initializer.Compile(ncs, root, block);
            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -initType.Size(root) });

            NCSInstruction loopStart = ncs.Add(NCSInstructionType.NOP);
            var updateStart = new NCSInstruction(NCSInstructionType.NOP);
            var loopEnd = new NCSInstruction(NCSInstructionType.NOP);

            Condition.Compile(ncs, root, block);
            ncs.Add(NCSInstructionType.JZ, jump: loopEnd);

            Block.Compile(ncs, root, block, returnInstruction, loopEnd, updateStart);
            ncs.Instructions.Add(updateStart);

            DynamicDataType iterType = Iteration.Compile(ncs, root, block);
            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -iterType.Size(root) });
            ncs.Add(NCSInstructionType.JMP, jump: loopStart);
            ncs.Instructions.Add(loopEnd);

            return DynamicDataType.VOID;
        }
    }

    // Note: BreakStatement and ContinueStatement are defined in NSS/AST/Statements/

}
