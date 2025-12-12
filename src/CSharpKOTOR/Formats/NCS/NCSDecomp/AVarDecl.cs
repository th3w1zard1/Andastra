// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AVarDecl.java:58-63
        // Original: @Override public String toString() { return this.exp == null ? this.tabs + this.var.toDeclString() + ";" + this.newline : this.tabs + this.var.toDeclString() + " = " + ExpressionFormatter.formatValue(this.exp) + ";" + this.newline; }
        public override string ToString()
        {
            return this.exp == null
                ? this.tabs + this.var.ToDeclString() + ";" + this.newline
                : this.tabs + this.var.ToDeclString() + " = " + ExpressionFormatter.FormatValue(this.exp) + ";" + this.newline;
        }

        public override void Close()
        {
            base.Close();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Close();
            }

            this.exp = null;
            if (this.var != null)
            {
                this.var.Close();
            }

            this.var = null;
        }
    }
}




