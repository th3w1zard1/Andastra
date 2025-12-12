// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class DoGlobalVars : MainPass
    {
        private bool freezeStack;
        public DoGlobalVars(NodeAnalysisData nodedata, SubroutineAnalysisData subdata) : base(nodedata, subdata)
        {
            this.state.SetVarPrefix("GLOB_");
            this.freezeStack = false;
        }

        public override string GetCode()
        {
            return this.state.ToStringGlobals();
        }

        public override void OutABpCommand(ABpCommand node)
        {
            this.freezeStack = true;
        }

        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.freezeStack = true;
        }

        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.freezeStack)
            {
                this.state.TransformMoveSp(node);
                for (int remove = NodeUtils.StackOffsetToPos(node.GetOffset()), i = 0; i < remove; ++i)
                {
                    this.stack.Remove();
                }
            }
        }

        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            if (!this.freezeStack)
            {
                this.state.TransformCopyDownSp(node);
            }
        }

        public override void OutARsaddCommand(ARsaddCommand node)
        {
            if (!this.freezeStack)
            {
                Variable var = new Variable(NodeUtils.GetType(node));
                this.stack.Push(var);
                this.state.TransformRSAdd(node);
                var = null;
            }
        }

        public virtual LocalVarStack GetStack()
        {
            return this.stack;
        }
    }
}




