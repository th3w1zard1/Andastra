using System.Text;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AIf : AControlLoop
    {
        public AIf() : this(0, 0, null)
        {
        }

        public AIf(int start, int end, AExpression condition) : base(start, end)
        {
            if (condition != null)
            {
                SetCondition(condition);
            }
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            var condition = GetCondition();
            buff.Append(GetTabs() + "if (" + (condition != null ? condition.ToString() : "") + ") {" + GetNewline());
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            buff.Append(GetTabs() + "}" + GetNewline());
            return buff.ToString();
        }
    }
}

