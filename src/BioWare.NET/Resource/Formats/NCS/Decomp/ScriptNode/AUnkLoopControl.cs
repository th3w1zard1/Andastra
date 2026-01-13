// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:8-23
// Original: public class AUnkLoopControl extends ScriptNode
namespace BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode
{
    public class AUnkLoopControl : ScriptNode
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:9
        // Original: protected int dest;
        protected int dest;

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:11-13
        // Original: public AUnkLoopControl(int dest) { this.dest = dest; }
        public AUnkLoopControl(int dest)
        {
            this.dest = dest;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:15-17
        // Original: public int getDestination() { return this.dest; }
        public int GetDestination()
        {
            return this.dest;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/AUnkLoopControl.java:19-22
        // Original: @Override public String toString() { return "BREAK or CONTINUE undetermined"; }
        public override string ToString()
        {
            return "BREAK or CONTINUE undetermined";
        }
    }
}





