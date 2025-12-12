using System;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AVarRef : ScriptNode, AExpression
    {
        private Variable var;

        public AVarRef(Variable var)
        {
            this.var = var;
        }

        public AVarRef(VarStruct structVar)
        {
            this.var = structVar;
        }

        public UtilsType Type()
        {
            return this.var.Type();
        }

        public Variable Var()
        {
            return this.var;
        }

        public void Var(Variable var)
        {
            this.var = var;
        }

        public void ChooseStructElement(Variable var)
        {
            if (this.var is VarStruct varStruct && varStruct.Contains(var))
            {
                this.var = var;
                return;
            }
            throw new System.Exception("Attempted to select a struct element not in struct");
        }

        public override string ToString()
        {
            return this.var.ToString();
        }

        public StackEntry StackEntry()
        {
            return this.var;
        }

        public void StackEntry(StackEntry stackentry)
        {
            this.var = (Variable)stackentry;
        }

        public StackEntry Stackentry()
        {
            return this.var;
        }

        public void Stackentry(StackEntry p0)
        {
            this.var = (Variable)p0;
        }

        Scriptnode.ScriptNode AExpression.Parent() => (Scriptnode.ScriptNode)(object)base.Parent();
        void AExpression.Parent(Scriptnode.ScriptNode p0) => base.Parent((ScriptNode)(object)p0);

        public override void Close()
        {
            base.Close();
            if (this.var != null)
            {
                // Variable may have Close() method, but we check for it dynamically
                var varType = this.var.GetType();
                var closeMethod = varType.GetMethod("Close", System.Type.EmptyTypes);
                if (closeMethod != null)
                {
                    closeMethod.Invoke(this.var, null);
                }
            }
            this.var = null;
        }
    }
}




