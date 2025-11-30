using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AModifyExp : AExpression
    {
        private AVarRef _varRef;
        private AExpression _exp;

        public AModifyExp(AVarRef varRef, AExpression exp)
        {
            SetVarRef(varRef);
            SetExpression(exp);
        }

        public AVarRef GetVarRef()
        {
            return _varRef;
        }

        public void SetVarRef(AVarRef varRef)
        {
            _varRef = varRef;
            if (varRef != null)
            {
                varRef.SetParent(this);
            }
        }

        public AExpression GetExpression()
        {
            return _exp;
        }

        public void SetExpression(AExpression exp)
        {
            _exp = exp;
            if (exp != null)
            {
                exp.SetParent(this);
            }
        }

        public override StackEntry StackEntry()
        {
            return _varRef != null ? _varRef.GetVarVar() : null;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            // Do nothing - stackentry is derived from varref
        }

        public override string ToString()
        {
            return (_varRef != null ? _varRef.ToString() : "") + " = " + (_exp != null ? _exp.ToString() : "");
        }

        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                _exp.Close();
                _exp = null;
            }
            if (_varRef != null)
            {
                _varRef.Close();
            }
            _varRef = null;
        }
    }
}

