using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AConditionalExp : AExpression
    {
        private AExpression _left;
        private AExpression _right;
        private string _op;
        private StackEntry _stackEntry;

        public AConditionalExp(AExpression left, AExpression right, string op)
        {
            SetLeft(left);
            SetRight(right);
            _op = op;
        }

        public AExpression GetLeft()
        {
            return _left;
        }

        public void SetLeft(AExpression left)
        {
            _left = left;
            if (left != null)
            {
                left.SetParent(this);
            }
        }

        public AExpression GetRight()
        {
            return _right;
        }

        public void SetRight(AExpression right)
        {
            _right = right;
            if (right != null)
            {
                right.SetParent(this);
            }
        }

        public string GetOp()
        {
            return _op;
        }

        public void SetOp(string op)
        {
            _op = op;
        }

        public override StackEntry StackEntry()
        {
            return _stackEntry;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            _stackEntry = stackEntry;
        }

        public override string ToString()
        {
            return "(" + (_left != null ? _left.ToString() : "") + " " + _op + " " + (_right != null ? _right.ToString() : "") + ")";
        }

        public override void Close()
        {
            base.Close();
            if (_left != null)
            {
                _left.Close();
                _left = null;
            }
            if (_right != null)
            {
                _right.Close();
                _right = null;
            }
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}

