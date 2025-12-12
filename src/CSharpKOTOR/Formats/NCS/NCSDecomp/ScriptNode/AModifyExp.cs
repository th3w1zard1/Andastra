using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AModifyExp : ScriptNode, AExpression
    {
        private AVarRef _varRef;
        private AExpression _exp;
        private Scriptnode.ScriptNode _interfaceParent;

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
                ((AExpression)varRef).Parent((Scriptnode.ScriptNode)(AExpression)this);
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
                exp.Parent((Scriptnode.ScriptNode)(AExpression)this);
            }
        }

        public StackEntry StackEntry()
        {
            return _varRef != null ? _varRef.Var() : null;
        }

        public void SetStackEntry(StackEntry stackEntry)
        {
            // Do nothing - stackentry is derived from varref
        }

        public StackEntry Stackentry()
        {
            return _varRef != null ? _varRef.Var() : null;
        }

        public void Stackentry(StackEntry p0)
        {
            // Do nothing - stackentry is derived from varref
        }

        Scriptnode.ScriptNode AExpression.Parent() => _interfaceParent;
        void AExpression.Parent(Scriptnode.ScriptNode p0) => _interfaceParent = p0;

        public override string ToString()
        {
            return (_varRef != null ? _varRef.ToString() : "") + " = " + (_exp != null ? _exp.ToString() : "");
        }

        public override void Close()
        {
            if (_exp != null)
            {
                if (_exp is Scriptnode.ScriptNode scriptNode)
                {
                    scriptNode.Dispose();
                }
                else if (_exp is StackEntry expEntry)
                {
                    expEntry.Close();
                }
                _exp = null;
            }
            if (_varRef != null)
            {
                _varRef.Close();
            }
            _varRef = null;
            _interfaceParent = null;
            base.Close();
        }
    }
}






