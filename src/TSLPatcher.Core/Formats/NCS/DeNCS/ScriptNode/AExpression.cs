using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public abstract class AExpression : ScriptNode
    {
        public abstract StackEntry StackEntry();

        public abstract void SetStackEntry(StackEntry stackEntry);
    }
}

