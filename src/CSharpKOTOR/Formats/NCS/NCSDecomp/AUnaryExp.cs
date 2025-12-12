// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AUnaryExp : ScriptNode, AExpression
    {
        private AExpression exp;
        private string op;
        private StackEntry stackentry;
        public AUnaryExp(AExpression exp, string op)
        {
            this.Exp(exp);
            this.op = op;
        }

        protected virtual void Exp(AExpression exp)
        {
            (this.exp = exp).Parent(this);
        }

        public override string ToString()
        {
            return "(" + this.op + this.exp.ToString() + ")";
        }

        public virtual StackEntry Stackentry()
        {
            return this.stackentry;
        }

        public virtual void Stackentry(StackEntry stackentry)
        {
            this.stackentry = stackentry;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Dispose();
            }

            this.exp = null;
            if (this.stackentry != null)
            {
                this.stackentry.Dispose();
            }

            this.stackentry = null;
        }
    }
}




