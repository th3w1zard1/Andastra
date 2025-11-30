namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class ScriptNode
    {
        private ScriptNode _parent;
        private string _tabs = "";
        private readonly string _newline = System.Environment.NewLine;

        public ScriptNode()
        {
        }

        public ScriptNode Parent()
        {
            return _parent;
        }

        public void SetParent(ScriptNode parent)
        {
            _parent = parent;
            if (parent != null)
            {
                _tabs = parent._tabs + "\t";
            }
        }

        public string GetTabs()
        {
            return _tabs;
        }

        public void SetTabs(string tabs)
        {
            _tabs = tabs;
        }

        public string GetNewline()
        {
            return _newline;
        }

        public virtual void Close()
        {
            _parent = null;
        }
    }
}

