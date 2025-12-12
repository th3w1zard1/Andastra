// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;
using ScriptNodeNS = CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:10-12
    // Original: public class AModifyExp extends ScriptNode implements AExpression { private AVarRef varref; private AExpression exp; }
    public class AModifyExp : ScriptNode, AExpression
    {
        private ScriptNodeNS.AVarRef varref;
        private AExpression exp;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:14-17
        // Original: public AModifyExp(AVarRef varref, AExpression exp) { this.varRef(varref); this.expression(exp); }
        public AModifyExp(ScriptNodeNS.AVarRef varref, AExpression exp)
        {
            this.VarRef(varref);
            this.Expression(exp);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:19-22
        // Original: protected void varRef(AVarRef varref) { this.varref = varref; varref.parent(this); }
        protected virtual void VarRef(ScriptNodeNS.AVarRef varref)
        {
            this.varref = varref;
            varref.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:24-27
        // Original: protected void expression(AExpression exp) { this.exp = exp; exp.parent(this); }
        protected virtual void Expression(AExpression exp)
        {
            this.exp = exp;
            exp.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:29-31
        // Original: public AExpression expression() { return this.exp; }
        public virtual AExpression Expression()
        {
            return this.exp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:33-35
        // Original: public AVarRef varRef() { return this.varref; }
        public virtual ScriptNodeNS.AVarRef VarRef()
        {
            return this.varref;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:37-40
        // Original: @Override public String toString() { return ExpressionFormatter.format(this); }
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:42-45
        // Original: @Override public StackEntry stackentry() { return this.varref.var(); }
        public virtual StackEntry Stackentry()
        {
            return this.varref.Var();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:47-50
        // Original: @Override public void stackentry(StackEntry stackentry) { }
        public virtual void Stackentry(StackEntry stackentry)
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AModifyExp.java:52-64
        // Original: @Override public void close() { ... }
        public override void Close()
        {
            base.Close();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Close();
            }

            this.exp = null;
            if (this.varref != null)
            {
                this.varref.Close();
            }

            this.varref = null;
        }
    }
}




