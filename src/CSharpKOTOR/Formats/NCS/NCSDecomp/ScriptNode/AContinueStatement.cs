// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AContinueStatement.java:7-12
// Original: public class AContinueStatement extends ScriptNode
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AContinueStatement : Scriptnode.ScriptNode
    {
        public AContinueStatement()
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AContinueStatement.java:8-11
        // Original: @Override public String toString() { return this.tabs + "continue;" + this.newline; }
        public override string ToString()
        {
            return this.tabs + "continue;" + this.newline;
        }
    }
}





