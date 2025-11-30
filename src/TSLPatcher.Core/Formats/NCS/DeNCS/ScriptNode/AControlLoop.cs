using TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
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
                _condition.SetParent(null);
            }
            if (condition != null)
            {
                condition.SetParent(this);
            }
            _condition = condition;
        }

        public override void Close()
        {
            base.Close();
            if (_condition != null)
            {
                _condition.Close();
            }
            _condition = null;
        }
    }
}

