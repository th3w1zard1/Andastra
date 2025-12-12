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

        // Override DefaultIn to ensure skipdeadcode is set correctly for globals
        // Matching DeNCS implementation: DoGlobalVars inherits MainPass.DefaultIn behavior
        // which sets skipdeadcode based on ProcessCode, but OutARsaddCommand checks freezeStack instead
        public override void DefaultIn(Node node)
        {
            // Call base to set skipdeadcode (though we check freezeStack in OutARsaddCommand)
            base.DefaultIn(node);
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
        // This is called from PrunedDepthFirstAdapter.CaseABpCommand overload
        public void OutABpCommand(AST.ABpCommand node)
        {
            // Treat AST.ABpCommand the same as root namespace ABpCommand
            // Set freezeStack to true when we encounter SAVEBP/RESTOREBP
            // This prevents stack mutations after BP ops, but commands before SAVEBP should still be processed
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
        // Override MainPass.OutARsaddCommand(AST.ARsaddCommand) to use freezeStack instead of skipdeadcode
        public override void OutARsaddCommand(AST.ARsaddCommand node)
        {
            // Treat AST.ARsaddCommand the same as root namespace ARsaddCommand
            // Use freezeStack check instead of skipdeadcode (matching DoGlobalVars pattern)
            JavaSystem.@out.Println($"DEBUG DoGlobalVars.OutARsaddCommand(AST): freezeStack={this.freezeStack}, skipdeadcode={this.skipdeadcode}");
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
                JavaSystem.@out.Println($"DEBUG DoGlobalVars.OutARsaddCommand(AST): calling TransformRSAdd, stack size={this.stack.Size()}");
                this.state.TransformRSAdd(node);
                JavaSystem.@out.Println($"DEBUG DoGlobalVars.OutARsaddCommand(AST): after TransformRSAdd, root children count={this.state.GetRoot().GetChildren().Count}");
                var = null;
            }
            else
            {
                JavaSystem.@out.Println($"DEBUG DoGlobalVars.OutARsaddCommand(AST): skipping because freezeStack=true");
            }
        }

        public virtual LocalVarStack GetStack()
        {
            return this.stack;
        }

    }
}




