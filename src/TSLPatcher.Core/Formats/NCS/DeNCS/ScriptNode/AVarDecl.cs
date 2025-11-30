using System.Text;
using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;
using TSLPatcher.Core.Formats.NCS.DeNCS.Utils;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AVarDecl : ScriptNode
    {
        private Variable _var;
        private AExpression _exp;
        private bool _isFcnReturn;

        public AVarDecl(Variable var)
        {
            SetVarVar(var);
            _isFcnReturn = false;
        }

        public Variable GetVarVar()
        {
            return _var;
        }

        public void SetVarVar(Variable var)
        {
            _var = var;
        }

        public bool IsFcnReturn()
        {
            return _isFcnReturn;
        }

        public void SetIsFcnReturn(bool isVal)
        {
            _isFcnReturn = isVal;
        }

        public Utils.Type GetType()
        {
            if (_var != null)
            {
                return _var.Type();
            }
            return null;
        }

        public void InitializeExp(AExpression exp)
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

        public AExpression RemoveExp()
        {
            var aexp = _exp;
            if (_exp != null)
            {
                _exp.SetParent(null);
            }
            _exp = null;
            return aexp;
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        public override string ToString()
        {
            if (_exp == null)
            {
                return GetTabs() + (_var != null ? _var.ToDeclString() : "") + ";" + GetNewline();
            }
            return GetTabs() + (_var != null ? _var.ToDeclString() : "") + " = " + _exp.ToString() + ";" + GetNewline();
        }

        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                _exp.Close();
                _exp = null;
            }
            if (_var != null)
            {
                _var.Close();
            }
            _var = null;
        }
    }
}

