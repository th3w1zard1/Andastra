using System.Collections.Generic;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ACodeBlock : ScriptRootNode
    {
        public ACodeBlock() : this(0, 0)
        {
        }

        public ACodeBlock(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            buff.Append(GetTabs() + "{" + GetNewline());
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            buff.Append(GetTabs() + "}" + GetNewline());
            return buff.ToString();
        }
    }
}





