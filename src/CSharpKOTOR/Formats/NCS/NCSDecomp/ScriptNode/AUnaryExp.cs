using System;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AUnaryExp : ScriptNode, AExpression
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
                exp.Parent((Scriptnode.ScriptNode)(object)this);
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

        public StackEntry StackEntry()
        {
            return _stackEntry;
        }

        public void SetStackEntry(StackEntry stackEntry)
        {
            _stackEntry = stackEntry;
        }

        public StackEntry Stackentry()
        {
            return _stackEntry;
        }

        public void Stackentry(StackEntry p0)
        {
            _stackEntry = p0;
        }

        Scriptnode.ScriptNode AExpression.Parent() => (Scriptnode.ScriptNode)(object)base.Parent();
        void AExpression.Parent(Scriptnode.ScriptNode p0) => base.SetParent((ScriptNode)(object)p0);

        public override string ToString()
        {
            return "(" + _op + (_exp != null ? _exp.ToString() : "") + ")";
        }

        public override void Close()
        {
            if (_exp != null)
            {
                if (_exp is Scriptnode.ScriptNode expNode)
                {
                    expNode.Dispose();
                }
                _exp = null;
            }
            if (_stackEntry != null && _stackEntry is IDisposable disposableStackEntry)
            {
                disposableStackEntry.Dispose();
            }
            _stackEntry = null;
        }
    }
}





