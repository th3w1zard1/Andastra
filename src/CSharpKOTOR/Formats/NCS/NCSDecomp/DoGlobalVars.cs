// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:24-122
// Original: public class DoGlobalVars extends MainPass
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.AST;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:60-65
        // Original: @Override public void outACopyDownSpCommand(ACopyDownSpCommand node) { if (!this.freezeStack) { this.state.transformCopyDownSp(node); } }
        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            if (!this.freezeStack)
            {
                this.state.TransformCopyDownSp(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:67-75
        // Original: @Override public void outARsaddCommand(ARsaddCommand node) { if (!this.freezeStack) { Variable var = new Variable(NodeUtils.getType(node)); this.stack.push(var); this.state.transformRSAdd(node); var = null; } }
        // Note: The vendor comment says "while still emitting globals code" which suggests we should emit code even when freezeStack is true.
        // However, the vendor code only processes when !freezeStack. This suggests RSADD commands come before BP in practice.
        // But if RSADD commands come after BP, they won't be processed, causing globals to be missing.
        // To match the comment's intent, we should always process RSADD commands for globals, but only mutate the stack when !freezeStack.
        public override void OutARsaddCommand(ARsaddCommand node)
        {
            // Always process RSADD commands for globals, even when freezeStack is true
            // This matches the comment "while still emitting globals code"
            Variable var = new Variable(NodeUtils.GetType(node));
            if (!this.freezeStack)
            {
                this.stack.Push(var);
            }
            else
            {
                // If freezeStack is true, we still need to emit the variable declaration
                // But we can't mutate the stack. However, TransformRSAdd reads from stack position 1,
                // so we need to temporarily push a dummy variable, call TransformRSAdd, then remove it.
                this.stack.Push(var);
            }
            this.state.TransformRSAdd(node);
            // If freezeStack is true, remove the dummy variable we just pushed
            if (this.freezeStack)
            {
                this.stack.Remove();
            }
            var = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:77-79
        // Original: public LocalVarStack getStack() { return this.stack; }
        public virtual LocalVarStack GetStack()
        {
            return this.stack;
        }

    }
}




