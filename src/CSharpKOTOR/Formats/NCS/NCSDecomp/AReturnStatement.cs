// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:8-43
// Original: public class AReturnStatement extends ScriptNode
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AReturnStatement : ScriptNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:9
        // Original: protected AExpression returnexp;
        protected AExpression returnexp;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:11-12
        // Original: public AReturnStatement() { }
        public AReturnStatement()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:14-16
        // Original: public AReturnStatement(AExpression returnexp) { this.returnexp(returnexp); }
        public AReturnStatement(AExpression returnexp)
        {
            this.Returnexp(returnexp);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:18-21
        // Original: public void returnexp(AExpression returnexp) { returnexp.parent(this); this.returnexp = returnexp; }
        public virtual void Returnexp(AExpression returnexp)
        {
            returnexp.Parent(this);
            this.returnexp = returnexp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:23-25
        // Original: public AExpression exp() { return this.returnexp; }
        public virtual AExpression Exp()
        {
            return this.returnexp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:27-32
        // Original: @Override public String toString() { return this.returnexp == null ? this.tabs + "return;" + this.newline : this.tabs + "return " + ExpressionFormatter.formatValue(this.returnexp) + ";" + this.newline; }
        public override string ToString()
        {
            return this.returnexp == null
                ? this.tabs + "return;" + this.newline
                : this.tabs + "return " + ExpressionFormatter.FormatValue(this.returnexp) + ";" + this.newline;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:34-42
        // Original: @Override public void close() { super.close(); if (this.returnexp != null) { ((ScriptNode)this.returnexp).close(); } this.returnexp = null; }
        public override void Close()
        {
            base.Close();
            if (this.returnexp != null)
            {
                ((ScriptNode)this.returnexp).Close();
            }

            this.returnexp = null;
        }
    }
}




