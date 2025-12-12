// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;

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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryExp.java:20-23
        // Original: protected void exp(AExpression exp) { this.exp = exp; exp.parent(this); }
        protected virtual void Exp(AExpression exp)
        {
            this.exp = exp;
            exp.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryExp.java:25-27
        // Original: public AExpression exp() { return this.exp; }
        public virtual AExpression Exp()
        {
            return this.exp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryExp.java:29-31
        // Original: public String op() { return this.op; }
        public virtual string Op()
        {
            return this.op;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryExp.java:33-36
        // Original: @Override public String toString() { return ExpressionFormatter.format(this); }
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        public virtual StackEntry Stackentry()
        {
            return this.stackentry;
        }

        public virtual void Stackentry(StackEntry stackentry)
        {
            this.stackentry = stackentry;
        }

        public override void Close()
        {
            base.Close();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Close();
            }

            this.exp = null;
            if (this.stackentry != null)
            {
                this.stackentry.Close();
            }

            this.stackentry = null;
        }
    }
}




