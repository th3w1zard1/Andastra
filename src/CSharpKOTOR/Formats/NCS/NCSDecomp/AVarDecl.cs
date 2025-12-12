// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AVarDecl : ScriptNode
    {
        private Variable var;
        private AExpression exp;
        private bool isFcnReturn;
        public AVarDecl(Variable var)
        {
            this.Var(var);
            this.exp = null;
            this.isFcnReturn = false;
        }

        public virtual void Var(Variable var)
        {
            this.var = var;
        }

        public virtual Variable Var()
        {
            return this.var;
        }

        public virtual void IsFcnReturn(bool @is)
        {
            this.isFcnReturn = @is;
        }

        public virtual bool IsFcnReturn()
        {
            return this.isFcnReturn;
        }

        public virtual UtilsType Type()
        {
            return this.var.Type();
        }

        public virtual void InitializeExp(AExpression exp)
        {
            exp.Parent(this);
            this.exp = exp;
        }

        public virtual AExpression RemoveExp()
        {
            AExpression aexp = this.exp;
            this.exp.Parent(null);
            this.exp = null;
            return aexp;
        }

        public virtual AExpression Exp()
        {
            return this.exp;
        }

        public override string ToString()
        {
            if (this.exp == null)
            {
                return this.tabs.ToString() + this.var.ToDeclString() + ";" + this.newline;
            }

            return this.tabs.ToString() + this.var.ToDeclString() + " = " + this.exp.ToString() + ";" + this.newline;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Dispose();
            }

            this.exp = null;
            if (this.var != null)
            {
                this.var.Dispose();
            }

            this.var = null;
        }
    }
}




