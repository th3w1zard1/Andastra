using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AElse : ScriptRootNode
    {
        public AElse() : this(0, 0)
        {
        }

        public AElse(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            buff.Append(GetTabs() + "else {" + GetNewline());
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            buff.Append(GetTabs() + "}" + GetNewline());
            return buff.ToString();
        }
    }
}





