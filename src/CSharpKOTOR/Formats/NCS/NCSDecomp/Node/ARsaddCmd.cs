namespace CSharpKOTOR.Formats.NCS.NCSDecomp.AST
{
    public sealed class ARsaddCmd : PCmd
    {
        private PRsaddCommand _rsaddCommand;

        public ARsaddCmd()
        {
        }

        public ARsaddCmd(PRsaddCommand rsaddCommand)
        {
            SetRsaddCommand(rsaddCommand);
        }

        public override object Clone()
        {
            return new ARsaddCmd((PRsaddCommand)CloneNode(_rsaddCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PRsaddCommand GetRsaddCommand()
        {
            return _rsaddCommand;
        }

        public void SetRsaddCommand(PRsaddCommand node)
        {
            if (_rsaddCommand != null)
            {
                _rsaddCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _rsaddCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_rsaddCommand == child)
            {
                _rsaddCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_rsaddCommand == oldChild)
            {
                SetRsaddCommand((PRsaddCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_rsaddCommand);
        }
    }
}





