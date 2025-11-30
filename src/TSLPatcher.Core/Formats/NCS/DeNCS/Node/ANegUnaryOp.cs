namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class ANegUnaryOp : PUnaryOp
    {
        private TNeg _neg;

        public ANegUnaryOp()
        {
        }

        public ANegUnaryOp(TNeg neg)
        {
            SetNeg(neg);
        }

        public override object Clone()
        {
            return new ANegUnaryOp((TNeg)CloneNode(_neg));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TNeg GetNeg()
        {
            return _neg;
        }

        public void SetNeg(TNeg node)
        {
            if (_neg != null)
            {
                _neg.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _neg = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_neg == child)
            {
                _neg = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_neg == oldChild)
            {
                SetNeg((TNeg)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_neg);
        }
    }
}

