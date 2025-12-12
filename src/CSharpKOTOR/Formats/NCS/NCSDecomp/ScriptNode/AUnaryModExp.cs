using System;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AUnaryModExp : ScriptNode, AExpression
    {
        private AVarRef _varRef;
        private string _op;
        private bool _prefix;
        private StackEntry _stackEntry;

        public AUnaryModExp(AVarRef varRef, string op, bool prefix)
        {
            SetVarRef(varRef);
            _op = op;
            _prefix = prefix;
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
                ((AExpression)varRef).Parent((Scriptnode.ScriptNode)(object)this);
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

        public bool IsPrefix()
        {
            return _prefix;
        }

        public bool GetPrefix()
        {
            return _prefix;
        }

        public void SetPrefix(bool prefix)
        {
            _prefix = prefix;
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:41
        // Original: return ExpressionFormatter.format(this);
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public override void Close()
        {
            if (_varRef != null)
            {
                _varRef.Close();
            }
            _varRef = null;
            if (_stackEntry != null && _stackEntry is IDisposable disposableStackEntry)
            {
                disposableStackEntry.Dispose();
            }
            _stackEntry = null;
        }
    }
}





