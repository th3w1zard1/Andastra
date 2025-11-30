using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AUnaryExp : AExpression
    {
        private AExpression _exp;
        private string _op;
        private StackEntry _stackEntry;

        public AUnaryExp(AExpression exp, string op)
        {
            SetExp(exp);
            _op = op;
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        public void SetExp(AExpression exp)
        {
            _exp = exp;
            if (exp != null)
            {
                exp.SetParent(this);
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
            return "(" + _op + (_exp != null ? _exp.ToString() : "") + ")";
        }

        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                _exp.Close();
                _exp = null;
            }
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}

