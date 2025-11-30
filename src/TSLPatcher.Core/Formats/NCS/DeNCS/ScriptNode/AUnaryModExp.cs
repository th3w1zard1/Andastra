using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AUnaryModExp : AExpression
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
                varRef.SetParent(this);
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

        public void SetPrefix(bool prefix)
        {
            _prefix = prefix;
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
            if (_prefix)
            {
                return "(" + _op + (_varRef != null ? _varRef.ToString() : "") + ")";
            }
            return "(" + (_varRef != null ? _varRef.ToString() : "") + _op + ")";
        }

        public override void Close()
        {
            base.Close();
            if (_varRef != null)
            {
                _varRef.Close();
            }
            _varRef = null;
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}

