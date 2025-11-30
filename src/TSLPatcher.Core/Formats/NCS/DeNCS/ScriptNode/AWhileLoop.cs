using System.Text;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
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
            buff.Append(GetTabs() + "while (" + (condition != null ? condition.ToString() : "") + ") {" + GetNewline());
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            buff.Append(GetTabs() + "}" + GetNewline());
            return buff.ToString();
        }
    }
}

