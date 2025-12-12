// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AConditionalExp : ScriptNode, AExpression
    {
        private AExpression left;
        private AExpression right;
        private string op;
        private StackEntry stackentry;
        public AConditionalExp(AExpression left, AExpression right, string op)
        {
            this.Left(left);
            this.Right(right);
            this.op = op;
        }

        protected virtual void Left(AExpression left)
        {
            (this.left = left).Parent(this);
        }

        protected virtual void Right(AExpression right)
        {
            (this.right = right).Parent(this);
        }

        public virtual AExpression Left()
        {
            return this.left;
        }

        public virtual AExpression Right()
        {
            return this.right;
        }

        public override string ToString()
        {
            return "(" + this.left.ToString() + " " + this.op + " " + this.right.ToString() + ")";
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
            if (this.left != null)
            {
                ((ScriptNode)this.left).Dispose();
                this.left = null;
            }

            if (this.right != null)
            {
                ((ScriptNode)this.right).Dispose();
                this.right = null;
            }

            if (this.stackentry != null)
            {
                this.stackentry.Dispose();
            }

            this.stackentry = null;
        }
    }
}




