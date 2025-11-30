namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class AUnaryCmd : PCmd
    {
        private PUnaryCommand _unaryCommand;

        public AUnaryCmd()
        {
        }

        public AUnaryCmd(PUnaryCommand unaryCommand)
        {
            SetUnaryCommand(unaryCommand);
        }

        public override object Clone()
        {
            return new AUnaryCmd((PUnaryCommand)CloneNode(_unaryCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PUnaryCommand GetUnaryCommand()
        {
            return _unaryCommand;
        }

        public void SetUnaryCommand(PUnaryCommand node)
        {
            if (_unaryCommand != null)
            {
                _unaryCommand.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _unaryCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_unaryCommand == child)
            {
                _unaryCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_unaryCommand == oldChild)
            {
                SetUnaryCommand((PUnaryCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_unaryCommand);
        }
    }
}

