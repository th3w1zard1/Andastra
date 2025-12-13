// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:24-122
// Original: public class DoGlobalVars extends MainPass
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class DoGlobalVars : MainPass
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:25
        // Original: private boolean freezeStack;
        private bool freezeStack;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:27-31
        // Original: public DoGlobalVars(NodeAnalysisData nodedata, SubroutineAnalysisData subdata) { super(nodedata, subdata); this.state.setVarPrefix("GLOB_"); this.freezeStack = false; }
        public DoGlobalVars(NodeAnalysisData nodedata, SubroutineAnalysisData subdata) : base(nodedata, subdata)
        {
            this.state.SetVarPrefix("GLOB_");
            this.freezeStack = false;
        }

        // Override DefaultIn to ensure skipdeadcode is set correctly for globals

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:33-36
        // Original: @Override public String getCode() { return this.state.toStringGlobals(); }
        public override string GetCode()
        {
            return this.state.ToStringGlobals();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:38-41
        // Original: @Override public void outABpCommand(ABpCommand node) { this.freezeStack = true; }
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:43-46
        // Original: @Override public void outAJumpToSubroutine(AJumpToSubroutine node) { this.freezeStack = true; }
        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.freezeStack = true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:49-58
        // Original: public void outAMoveSpCommand(AMoveSpCommand node) { if (!this.freezeStack) { this.state.transformMoveSp(node); int remove = NodeUtils.stackOffsetToPos(node.getOffset()); for (int i = 0; i < remove; i++) { this.stack.remove(); } } }
        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.freezeStack)
            {
                this.state.TransformMoveSp(node);
                int remove = NodeUtils.StackOffsetToPos(node.GetOffset());
                for (int i = 0; i < remove; i++)
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




