using System.Text;
using Scriptnode = CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AWhileLoop : AControlLoop
    {
        public AWhileLoop() : this(0, 0)
        {
        }

        public AWhileLoop(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            var condition = GetCondition();
            var tabs = GetTabs();
            var newline = GetNewline();
            buff.Append(tabs + "while (" + (condition != null ? condition.ToString() : "") + ") {" + newline);
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            buff.Append(tabs + "}" + newline);
            return buff.ToString();
        }

        private new string GetTabs()
        {
            return new string('\t', GetDepth());
        }

        private new string GetNewline()
        {
            return System.Environment.NewLine;
        }

        private int GetDepth()
        {
            int depth = 0;
            Scriptnode.ScriptNode node = this;
            while (node.Parent() != null)
            {
                depth++;
                node = node.Parent();
            }
            return depth;
        }
    }
}





