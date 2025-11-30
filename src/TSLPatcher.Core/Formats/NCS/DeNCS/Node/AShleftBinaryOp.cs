namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class AShleftBinaryOp : PBinaryOp
    {
        private TShleft _shleft;

        public AShleftBinaryOp()
        {
        }

        public AShleftBinaryOp(TShleft shleft)
        {
            SetShleft(shleft);
        }

        public override object Clone()
        {
            return new AShleftBinaryOp((TShleft)CloneNode(_shleft));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TShleft GetShleft()
        {
            return _shleft;
        }

        public void SetShleft(TShleft node)
        {
            if (_shleft != null)
            {
                _shleft.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _shleft = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_shleft == child)
            {
                _shleft = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_shleft == oldChild)
            {
                SetShleft((TShleft)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_shleft);
        }
    }
}

