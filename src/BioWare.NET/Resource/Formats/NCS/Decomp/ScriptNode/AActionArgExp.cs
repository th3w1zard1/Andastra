// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:9-24
// Original: public class AActionArgExp extends ScriptRootNode implements AExpression
using BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode;
using BioWare.NET.Resource.Formats.NCS.Decomp.Stack;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode
{
    public class AActionArgExp : ScriptRootNode, AExpression
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:10-14
        // Original: public AActionArgExp(int start, int end) { super(start, end); this.start = start; this.end = end; }
        public AActionArgExp(int start, int end) : base(start, end)
        {
            this.start = start;
            this.end = end;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:16-19
        // Original: @Override public StackEntry stackentry() { return null; }
        public StackEntry Stackentry()
        {
            return null;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AActionArgExp.java:21-23
        // Original: @Override public void stackentry(StackEntry stackentry) { }
        public void Stackentry(StackEntry stackentry)
        {
        }
    }
}





