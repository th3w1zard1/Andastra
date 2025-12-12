//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptutils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class MainPass : PrunedDepthFirstAdapter
    {
        protected LocalVarStack stack;
        protected NodeAnalysisData nodedata;
        protected SubroutineAnalysisData subdata;
        protected bool skipdeadcode;
        protected SubScriptState state;
        private ActionsData actions;
        protected bool globals;
        protected LocalVarStack backupstack;
        protected Utils.Type type;
        public MainPass(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions)
        {
            this.stack = new LocalVarStack();
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.actions = actions;
            state.InitStack(this.stack);
            this.skipdeadcode = false;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:72
            // Original: this.state = new SubScriptState(nodedata, subdata, this.stack, state, actions, FileDecompiler.preferSwitches);
            this.state = new SubScriptState(nodedata, subdata, this.stack, state, actions, FileDecompiler.preferSwitches);
            this.globals = false;
            this.backupstack = null;
            this.type = state.Type();
        }

        protected MainPass(NodeAnalysisData nodedata, SubroutineAnalysisData subdata)
        {
            this.stack = new LocalVarStack();
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.skipdeadcode = false;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:80
            // Original: this.state = new SubScriptState(nodedata, subdata, this.stack, FileDecompiler.preferSwitches);
            this.state = new SubScriptState(nodedata, subdata, this.stack, FileDecompiler.preferSwitches);
            this.globals = true;
            this.backupstack = null;
            this.type = new UtilsType((byte)255); // -1 as unsigned byte
        }

        public virtual void Done()
        {
            this.stack = null;
            this.nodedata = null;
            this.subdata = null;
            if (this.state != null)
            {
                this.state.ParseDone();
            }

            this.state = null;
            this.actions = null;
            this.backupstack = null;
            this.type = null;
        }

        public virtual void AssertStack()
        {
            if ((this.type.Equals((byte)0) || this.type.Equals((byte)255)) && this.stack.Size() > 0)
            {
                throw new Exception("Error: Final stack size " + this.stack.Size() + this.stack.ToString());
            }
        }

        public virtual string GetCode()
        {
            return this.state.ToString();
        }

        public virtual string GetProto()
        {
            return this.state.GetProto();
        }

        public virtual ASub GetScriptRoot()
        {
            return this.state.GetRoot();
        }

        public virtual SubScriptState GetState()
        {
            return this.state;
        }

        public override void OutARsaddCommand(ARsaddCommand node)
        {
            if (!this.skipdeadcode)
            {
                Variable var = new Variable(NodeUtils.GetType(node));
                this.stack.Push(var);
                var = null;
                this.state.TransformRSAdd(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Handle AST.ARsaddCommand as well (from NcsToAstConverter)
        public virtual void OutARsaddCommand(AST.ARsaddCommand node)
        {
            if (!this.skipdeadcode)
            {
                // Extract type from AST.ARsaddCommand's GetType() which returns TIntegerConstant
                int typeVal = 0;
                if (node.GetType() != null && node.GetType().GetText() != null)
                {
                    if (int.TryParse(node.GetType().GetText(), out int parsedType))
                    {
                        typeVal = parsedType;
                    }
                }
                Variable var = new Variable(new UtilsType((byte)typeVal));
                this.stack.Push(var);
                var = null;
                this.state.TransformRSAdd(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy > 1)
                {
                    this.stack.Structify(loc - copy + 1, copy, this.subdata);
                }

                this.state.TransformCopyDownSp(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                VarStruct varstruct = null;
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy > 1)
                {
                    varstruct = this.stack.Structify(loc - copy + 1, copy, this.subdata);
                }

                this.state.TransformCopyTopSp(node);
                if (copy > 1)
                {
                    this.stack.Push(varstruct);
                }
                else
                {
                    for (int i = 0; i < copy; ++i)
                    {
                        StackEntry entry = this.stack.Get(loc);
                        this.stack.Push(entry);
                    }
                }
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:189-201
        // Original: public void outAConstCommand(AConstCommand node)
        public override void OutAConstCommand(AConstCommand node)
        {
            if (!this.skipdeadcode)
            {
                Type type = NodeUtils.GetType(node);
                Const aconst;
                switch (type.ByteValue())
                {
                    case 3:
                        aconst = Const.NewConst(type, NodeUtils.GetIntConstValue(node));
                        break;
                    case 4:
                        aconst = Const.NewConst(type, NodeUtils.GetFloatConstValue(node));
                        break;
                    case 5:
                        aconst = Const.NewConst(type, NodeUtils.GetStringConstValue(node));
                        break;
                    case 6:
                        aconst = Const.NewConst(type, NodeUtils.GetObjectConstValue(node));
                        break;
                    default:
                        throw new Exception("Invalid const type " + type);
                }
                this.stack.Push(aconst);
                this.state.TransformConst(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAActionCommand(AActionCommand node)
        {
            if (!this.skipdeadcode)
            {
                StackEntry entry;
                for (int remove = NodeUtils.ActionRemoveElementCount(node, this.actions), i = 0; i < remove; i += entry.Size())
                {
                    entry = this.RemoveFromStack();
                }

                UtilsType type = NodeUtils.GetReturnType(node, this.actions);
                if (type.Equals(unchecked((byte)(-16))))
                {
                    for (int j = 0; j < 3; ++j)
                    {
                        Variable var1 = new Variable((byte)4);
                        this.stack.Push(var1);
                    }

                    this.stack.Structify(1, 3, this.subdata);
                }
                else if (!type.Equals((byte)0))
                {
                    Variable var2 = new Variable(type);
                    this.stack.Push(var2);
                }

                //UtilsType type2 = null;
                this.state.TransformAction(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutALogiiCommand(ALogiiCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.RemoveFromStack();
                this.RemoveFromStack();
                Variable var = new Variable((byte)3);
                this.stack.Push(var);
                var = null;
                this.state.TransformLogii(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutABinaryCommand(ABinaryCommand node)
        {
            if (!this.skipdeadcode)
            {
                int sizep3;
                int sizep2;
                int sizeresult;
                UtilsType resulttype;
                if (NodeUtils.IsEqualityOp(node))
                {
                    if (NodeUtils.GetType(node).Equals((byte)36))
                    {
                        sizep2 = (sizep3 = NodeUtils.StackSizeToPos(node.GetSize()));
                    }
                    else
                    {
                        sizep2 = (sizep3 = 1);
                    }

                    sizeresult = 1;
                    resulttype = new UtilsType((byte)3);
                }
                else if (NodeUtils.IsVectorAllowedOp(node))
                {
                    sizep3 = NodeUtils.GetParam1Size(node);
                    sizep2 = NodeUtils.GetParam2Size(node);
                    sizeresult = NodeUtils.GetResultSize(node);
                    resulttype = NodeUtils.GetReturnType(node);
                }
                else
                {
                    sizep3 = 1;
                    sizep2 = 1;
                    sizeresult = 1;
                    resulttype = new UtilsType((byte)3);
                }

                for (int i = 0; i < sizep3 + sizep2; ++i)
                {
                    this.RemoveFromStack();
                }

                for (int j = 0; j < sizeresult; ++j)
                {
                    Variable varLocal = new Variable(resulttype);
                    this.stack.Push(varLocal);
                }

                //Variable var = null;
                resulttype = null;
                this.state.TransformBinary(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAUnaryCommand(AUnaryCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformUnary(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformMoveSp(node);
                this.backupstack = (LocalVarStack)this.stack.Clone();
                int remove = NodeUtils.StackOffsetToPos(node.GetOffset());
                List<object> entries = new List<object>();
                int i = 0;
                while (i < remove)
                {
                    StackEntry entryLocal = this.RemoveFromStack();
                    i += entryLocal.Size();
                    if (entryLocal is Variable && !((Variable)entryLocal).IsPlaceholder(this.stack) && !((Variable)entryLocal).IsOnStack(this.stack))
                    {
                        entries.Add(entryLocal);
                    }
                }

                if (entries.Count > 0 && !this.nodedata.DeadCode(node))
                {
                    this.state.TransformMoveSPVariablesRemoved(entries, node);
                }

                //StackEntry entry = null;
                entries = null;
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            if (!this.skipdeadcode)
            {
                if (this.nodedata.LogOrCode(node))
                {
                    this.state.TransformLogOrExtraJump(node);
                }
                else
                {
                    this.state.TransformConditionalJump(node);
                }

                this.RemoveFromStack();
                if (!this.nodedata.LogOrCode(node))
                {
                    this.StoreStackState(this.nodedata.GetDestination(node), this.nodedata.DeadCode(node));
                }
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformJump(node);
                this.StoreStackState(this.nodedata.GetDestination(node), this.nodedata.DeadCode(node));
                if (this.backupstack != null)
                {
                    this.stack.DoneWithStack();
                    this.stack = this.backupstack;
                    this.state.SetStack(this.stack);
                }
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            if (!this.skipdeadcode)
            {
                SubroutineState substate = this.subdata.GetState(this.nodedata.GetDestination(node));
                for (int paramsize = substate.GetParamCount(), i = 0; i < paramsize; ++i)
                {
                    this.RemoveFromStack();
                }

                this.state.TransformJSR(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutADestructCommand(ADestructCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformDestruct(node);
                int removesize = NodeUtils.StackSizeToPos(node.GetSizeRem());
                int savestart = NodeUtils.StackSizeToPos(node.GetOffset());
                int savesize = NodeUtils.StackSizeToPos(node.GetSizeSave());
                this.stack.Destruct(removesize, savestart, savesize, this.subdata);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutACopyTopBpCommand(ACopyTopBpCommand node)
        {
            if (!this.skipdeadcode)
            {
                VarStruct varstruct = null;
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy > 1)
                {
                    varstruct = this.subdata.GetGlobalStack().Structify(loc - copy + 1, copy, this.subdata);
                }

                this.state.TransformCopyTopBp(node);
                if (copy > 1)
                {
                    this.stack.Push(varstruct);
                }
                else
                {
                    for (int i = 0; i < copy; ++i)
                    {
                        Variable varItem = (Variable)this.subdata.GetGlobalStack().Get(loc);
                        this.stack.Push(varItem);
                        --loc;
                    }
                }

                //Variable var = null;
                varstruct = null;
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutACopyDownBpCommand(ACopyDownBpCommand node)
        {
            if (!this.skipdeadcode)
            {
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy > 1)
                {
                    this.subdata.GetGlobalStack().Structify(loc - copy + 1, copy, this.subdata);
                }

                this.state.TransformCopyDownBp(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAStoreStateCommand(AStoreStateCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformStoreState(node);
                this.backupstack = null;
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAStackCommand(AStackCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformStack(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAReturn(AReturn node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformReturn(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutASubroutine(ASubroutine node)
        {
        }

        public override void OutAProgram(AProgram node)
        {
        }

        public override void DefaultIn(Node node)
        {
            this.RestoreStackState(node);
            this.CheckOrigins(node);
            if (NodeUtils.IsCommandNode(node))
            {
                this.skipdeadcode = !this.nodedata.ProcessCode(node);
            }
        }

        private StackEntry RemoveFromStack()
        {
            StackEntry entry = this.stack.Remove();
            if (entry is Variable && ((Variable)entry).IsPlaceholder(this.stack))
            {
                this.state.TransformPlaceholderVariableRemoved((Variable)entry);
            }

            return entry;
        }

        private void StoreStackState(Node node, bool isdead)
        {
            if (NodeUtils.IsStoreStackNode(node))
            {
                this.nodedata.SetStack(node, (LocalStack)this.stack.Clone(), false);
            }
        }

        private void RestoreStackState(Node node)
        {
            LocalVarStack restore = (LocalVarStack)this.nodedata.GetStack(node);
            if (restore != null)
            {
                this.stack.DoneWithStack();
                this.stack = restore;
                this.state.SetStack(this.stack);
                if (this.backupstack != null)
                {
                    this.backupstack.DoneWithStack();
                }

                this.backupstack = null;
            }

            restore = null;
        }

        private void CheckOrigins(Node node)
        {
            Node origin;
            while ((origin = this.GetNextOrigin(node)) != null)
            {
                this.state.TransformOriginFound(node, origin);
            }

            origin = null;
        }

        private Node GetNextOrigin(Node node)
        {
            return this.nodedata.RemoveLastOrigin(node);
        }
    }
}




