namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class AActionCmd : PCmd
    {
        private PActionCommand _actionCommand;

        public AActionCmd()
        {
        }

        public AActionCmd(PActionCommand actionCommand)
        {
            SetActionCommand(actionCommand);
        }

        public override object Clone()
        {
            return new AActionCmd((PActionCommand)CloneNode(_actionCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PActionCommand GetActionCommand()
        {
            return _actionCommand;
        }

        public void SetActionCommand(PActionCommand node)
        {
            if (_actionCommand != null)
            {
                _actionCommand.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _actionCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_actionCommand == child)
            {
                _actionCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_actionCommand == oldChild)
            {
                SetActionCommand((PActionCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_actionCommand);
        }
    }
}

