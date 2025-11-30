namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AExpressionStatement : ScriptNode
    {
        private AExpression _exp;

        public AExpressionStatement(AExpression exp)
        {
            SetExp(exp);
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        public void SetExp(AExpression exp)
        {
            if (_exp != null)
            {
                _exp.SetParent(null);
            }
            if (exp != null)
            {
                exp.SetParent(this);
            }
            _exp = exp;
        }

        public override string ToString()
        {
            return GetTabs() + (_exp != null ? _exp.ToString() : "") + ";" + GetNewline();
        }

        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                _exp.Close();
            }
            _exp = null;
        }
    }
}

