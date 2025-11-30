using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AActionArgExp : AExpression
    {
        public AActionArgExp()
        {
        }

        public override StackEntry StackEntry()
        {
            return null;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            // Do nothing
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }
}

