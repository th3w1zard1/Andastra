using System.Text;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AReturnStatement : ScriptNode
    {
        private AExpression _returnExp;

        public AReturnStatement() : this(null)
        {
        }

        public AReturnStatement(AExpression returnExp)
        {
            if (returnExp != null)
            {
                SetReturnExp(returnExp);
            }
        }

        public AExpression GetReturnExp()
        {
            return _returnExp;
        }

        public AExpression GetExp()
        {
            return _returnExp;
        }

        public void SetReturnExp(AExpression returnExp)
        {
            if (_returnExp != null)
            {
                _returnExp.SetParent(null);
            }
            if (returnExp != null)
            {
                returnExp.SetParent(this);
            }
            _returnExp = returnExp;
        }

        public override string ToString()
        {
            if (_returnExp == null)
            {
                return GetTabs() + "return;" + GetNewline();
            }
            return GetTabs() + "return " + _returnExp.ToString() + ";" + GetNewline();
        }

        public override void Close()
        {
            base.Close();
            if (_returnExp != null)
            {
                _returnExp.Close();
            }
            _returnExp = null;
        }
    }
}

