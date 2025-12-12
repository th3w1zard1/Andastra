using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AControlLoop : ScriptRootNode
    {
        private AExpression _condition;

        public AControlLoop() : this(0, 0)
        {
        }

        public AControlLoop(int start, int end) : base(start, end)
        {
        }

        public AExpression GetCondition()
        {
            return _condition;
        }

        public void SetCondition(AExpression condition)
        {
            if (_condition != null)
            {
                _condition.Parent(null);
            }
            if (condition != null)
            {
                condition.Parent(this);
            }
            _condition = condition;
        }

        public virtual void Close()
        {
            if (_condition != null)
            {
                if (_condition is Scriptnode.ScriptNode condNode)
                {
                    condNode.Dispose();
                }
                else if (_condition is StackEntry condEntry)
                {
                    condEntry.Close();
                }
            }
            _condition = null;
        }
    }
}





