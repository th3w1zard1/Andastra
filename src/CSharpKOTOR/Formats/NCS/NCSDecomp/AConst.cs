// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java:10-37
// Original: public class AConst extends ScriptNode implements AExpression
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AConst : ScriptNode, AExpression
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java:11
        // Original: private Const theconst;
        private Const theconst;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java:13-15
        // Original: public AConst(Const theconst) { this.theconst = theconst; }
        public AConst(Const theconst)
        {
            this.theconst = theconst;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java:17-20
        // Original: @Override public String toString() { return this.theconst.toString(); }
        public override string ToString()
        {
            return this.theconst.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java:22-25
        // Original: @Override public StackEntry stackentry() { return this.theconst; }
        public virtual StackEntry Stackentry()
        {
            return this.theconst;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java:27-30
        // Original: @Override public void stackentry(StackEntry stackentry) { this.theconst = (Const)stackentry; }
        public virtual void Stackentry(StackEntry stackentry)
        {
            this.theconst = (Const)stackentry;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AConst.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            this.theconst = null;
        }
    }
}




