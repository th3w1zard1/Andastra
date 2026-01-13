using System.Collections.Generic;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode
{
    // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ACodeBlock.java:7-10
    // Original: public class ACodeBlock extends ScriptRootNode { public ACodeBlock(int start, int end) { super(start, end); } }
    public class ACodeBlock : ScriptRootNode
    {
        public ACodeBlock(int start, int end) : base(start, end)
        {
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/ScriptNode/ACodeBlock.java:12-23
        // Original: @Override public String toString() { ... }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs + "{" + this.newline);

            for (int i = 0; i < this.children.Count; i++)
            {
                buff.Append(this.children[i].ToString());
            }

            buff.Append(this.tabs + "}" + this.newline);
            return buff.ToString();
        }
    }
}





