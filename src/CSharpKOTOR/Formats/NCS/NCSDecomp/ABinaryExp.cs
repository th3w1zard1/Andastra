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
    public class ABinaryExp : ScriptNode, AExpression
    {
        private AExpression left;
        private AExpression right;
        private string op;
        private StackEntry stackentry;
        public ABinaryExp(AExpression left, AExpression right, string op)
        {
            this.Left(left);
            this.Right(right);
            this.op = op;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABinaryExp.java:21-24
        // Original: protected void left(AExpression left) { this.left = left; left.parent(this); }
        protected virtual void Left(AExpression left)
        {
            this.left = left;
            left.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABinaryExp.java:26-29
        // Original: protected void right(AExpression right) { this.right = right; right.parent(this); }
        protected virtual void Right(AExpression right)
        {
            this.right = right;
            right.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABinaryExp.java:39-41
        // Original: public String op() { return this.op; }
        public virtual string Op()
        {
            return this.op;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABinaryExp.java:43-46
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABinaryExp.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (this.left != null)
            {
                ((ScriptNode)this.left).Close();
                this.left = null;
            }

            if (this.right != null)
            {
                ((ScriptNode)this.right).Close();
                this.right = null;
            }

            if (this.stackentry != null)
            {
                this.stackentry.Close();
            }

            this.stackentry = null;
        }
    }
}




