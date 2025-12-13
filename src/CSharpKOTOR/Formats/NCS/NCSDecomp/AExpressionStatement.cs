// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java:8-40
// Original: public class AExpressionStatement extends ScriptNode
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AExpressionStatement : ScriptNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java:9
        // Original: private AExpression exp;
        private AExpression exp;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java:11-14
        // Original: public AExpressionStatement(AExpression exp) { exp.parent(this); this.exp = exp; }
        public AExpressionStatement(AExpression exp)
        {
            exp.Parent(this);
            this.exp = exp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java:16-18
        // Original: public AExpression exp() { return this.exp; }
        public virtual AExpression Exp()
        {
            return this.exp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java:20-23
        // Original: @Override public String toString() { return this.tabs + this.exp.toString() + ";" + this.newline; }
        public override string ToString()
        {
            return this.tabs + this.exp.ToString() + ";" + this.newline;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java:25-29
        // Original: @Override public void parent(ScriptNode parent) { super.parent(parent); this.exp.parent(this); }
        public override void Parent(ScriptNode parent)
        {
            base.Parent(parent);
            this.exp.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Close();
            }

            this.exp = null;
        }
    }
}




