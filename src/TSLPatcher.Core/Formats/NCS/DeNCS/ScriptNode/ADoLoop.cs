using System.Text;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class ADoLoop : AControlLoop
    {
        public ADoLoop() : this(0, 0)
        {
        }

        public ADoLoop(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            var condition = GetCondition();
            buff.Append(GetTabs());
            buff.Append("do {" + GetNewline());
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            buff.Append(GetTabs() + "} while (" + (condition != null ? condition.ToString() : "") + ");" + GetNewline());
            return buff.ToString();
        }
    }
}

