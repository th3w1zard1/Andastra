// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABreakStatement.java:7-12
// Original: public class ABreakStatement extends ScriptNode
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ABreakStatement : Scriptnode.ScriptNode
    {
        public ABreakStatement()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ABreakStatement.java:8-11
        // Original: @Override public String toString() { return this.tabs + "break;" + this.newline; }
        public override string ToString()
        {
            return this.tabs + "break;" + this.newline;
        }
    }
}





