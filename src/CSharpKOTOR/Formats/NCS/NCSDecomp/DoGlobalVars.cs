//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

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

        // Handle AST.ABpCommand as well (from NcsToAstConverter)
        public void OutABpCommand(AST.ABpCommand node)
        {
            // Treat AST.ABpCommand the same as root namespace ABpCommand
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

        // Handle AST.ARsaddCommand as well (from NcsToAstConverter)
        // This is called from PrunedDepthFirstAdapter.CaseARsaddCommand overload
        public void OutARsaddCommand(AST.ARsaddCommand node)
        {
            // Treat AST.ARsaddCommand the same as root namespace ARsaddCommand
            if (!this.freezeStack)
            {
                // Extract type from AST.ARsaddCommand's GetType() which returns TIntegerConstant
                // Parse the type value from the TIntegerConstant's text
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




