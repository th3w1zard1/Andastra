namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AContinueStatement : ScriptNode
    {
        public AContinueStatement()
        {
        }

        public override string ToString()
        {
            return GetTabs() + "continue;" + GetNewline();
        }
    }
}

