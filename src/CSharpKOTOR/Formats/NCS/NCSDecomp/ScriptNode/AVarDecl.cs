using System;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
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

        public new Utils.Type GetType()
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
                _exp.Parent(null);
            }
            if (exp != null)
            {
                exp.Parent((Scriptnode.ScriptNode)(object)this);
            }
            _exp = exp;
        }

        public AExpression RemoveExp()
        {
            var aexp = _exp;
            if (_exp != null)
            {
                _exp.Parent(null);
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
                if (_exp is Scriptnode.ScriptNode expNode)
                {
                    expNode.Dispose();
                }
                _exp = null;
            }
            if (_var != null && _var is IDisposable disposableVar)
            {
                disposableVar.Dispose();
            }
            _var = null;
        }
    }
}





