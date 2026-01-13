// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AContinueStatement.java:7-12
// Original: public class AContinueStatement extends ScriptNode
using BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode
{
    public class AContinueStatement : ScriptNode
    {
        public AContinueStatement()
        {
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AContinueStatement.java:8-11
        // Original: @Override public String toString() { return this.tabs + "continue;" + this.newline; }
        public override string ToString()
        {
            return this.tabs + "continue;" + this.newline;
        }
    }
}





