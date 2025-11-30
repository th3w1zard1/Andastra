namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class AEqualBinaryOp : PBinaryOp
    {
        private TEqual _equal;

        public AEqualBinaryOp()
        {
        }

        public AEqualBinaryOp(TEqual equal)
        {
            SetEqual(equal);
        }

        public override object Clone()
        {
            return new AEqualBinaryOp((TEqual)CloneNode(_equal));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TEqual GetEqual()
        {
            return _equal;
        }

        public void SetEqual(TEqual node)
        {
            if (_equal != null)
            {
                _equal.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _equal = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_equal == child)
            {
                _equal = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_equal == oldChild)
            {
                SetEqual((TEqual)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_equal);
        }
    }
}

