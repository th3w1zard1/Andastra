using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;
using TSLPatcher.Core.Formats.NCS.DeNCS.Utils;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AVarRef : AExpression
    {
        private StackEntry _var;

        public AVarRef(Variable var)
        {
            SetVarVar(var);
        }

        public AVarRef(VarStruct var)
        {
            SetVarVar(var);
        }

        public Utils.Type GetType()
        {
            var varVar = GetVarVar();
            if (varVar != null)
            {
                return varVar.Type();
            }
            return new Utils.Type(0);
        }

        public StackEntry GetVarVar()
        {
            return _var;
        }

        public void SetVarVar(StackEntry var)
        {
            _var = var;
        }

        public void ChooseStructElement(Variable var)
        {
            if (_var is VarStruct varStruct && varStruct.Contains(var))
            {
                _var = var;
                return;
            }
            throw new System.Exception("Attempted to select a struct element not in struct");
        }

        public override string ToString()
        {
            return _var != null ? _var.ToString() : "";
        }

        public override StackEntry StackEntry()
        {
            return _var;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            SetVarVar(stackEntry);
        }

        public override void Close()
        {
            base.Close();
            if (_var != null)
            {
                _var.Close();
            }
            _var = null;
        }
    }
}

