// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;
using ScriptNodeNS = CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:10-14
    // Original: public class AUnaryModExp extends ScriptNode implements AExpression { private AVarRef varref; private String op; private boolean prefix; private StackEntry stackentry; }
    public class AUnaryModExp : ScriptNode, AExpression
    {
        private ScriptNodeNS.AVarRef varref;
        private string op;
        private bool prefix;
        private StackEntry stackentry;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:16-20
        // Original: public AUnaryModExp(AVarRef varref, String op, boolean prefix) { this.varRef(varref); this.op = op; this.prefix = prefix; }
        public AUnaryModExp(ScriptNodeNS.AVarRef varref, string op, bool prefix)
        {
            this.VarRef(varref);
            this.op = op;
            this.prefix = prefix;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:22-25
        // Original: protected void varRef(AVarRef varref) { this.varref = varref; varref.parent(this); }
        protected virtual void VarRef(ScriptNodeNS.AVarRef varref)
        {
            this.varref = varref;
            varref.Parent((ScriptNodeNS.ScriptNode)(object)this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:27-29
        // Original: public AVarRef varRef() { return this.varref; }
        public virtual ScriptNodeNS.AVarRef VarRef()
        {
            return this.varref;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:31-33
        // Original: public String op() { return this.op; }
        public virtual string Op()
        {
            return this.op;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:35-37
        // Original: public boolean prefix() { return this.prefix; }
        public virtual bool Prefix()
        {
            return this.prefix;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:39-42
        // Original: @Override public String toString() { return ExpressionFormatter.format(this); }
        public override string ToString()
        {
            return ExpressionFormatter.Format(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:44-47
        // Original: @Override public StackEntry stackentry() { return this.stackentry; }
        public virtual StackEntry Stackentry()
        {
            return this.stackentry;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:49-52
        // Original: @Override public void stackentry(StackEntry stackentry) { this.stackentry = stackentry; }
        public virtual void Stackentry(StackEntry stackentry)
        {
            this.stackentry = stackentry;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AUnaryModExp.java:54-67
        // Original: @Override public void close() { ... }
        public override void Close()
        {
            base.Close();
            if (this.varref != null)
            {
                this.varref.Close();
            }

            this.varref = null;
            if (this.stackentry != null)
            {
                this.stackentry.Close();
            }

            this.stackentry = null;
        }
    }
}




