namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class ABreakStatement : ScriptNode
    {
        public ABreakStatement()
        {
        }

        public override string ToString()
        {
            return GetTabs() + "break;" + GetNewline();
        }
    }
}

