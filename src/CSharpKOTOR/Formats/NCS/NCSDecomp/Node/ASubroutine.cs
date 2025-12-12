namespace CSharpKOTOR.Formats.NCS.NCSDecomp.AST
{
    public sealed class ASubroutine : PSubroutine
    {
        private PCommandBlock _commandBlock;
        private PReturn _return;
        private int _id;

        public ASubroutine()
        {
            _id = 0;
        }

        public ASubroutine(PCommandBlock commandBlock, PReturn returnNode)
        {
            SetCommandBlock(commandBlock);
            SetReturn(returnNode);
            _id = 0;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int subId)
        {
            _id = subId;
        }

        public override object Clone()
        {
            return new ASubroutine(
                (PCommandBlock)CloneNode(_commandBlock),
                (PReturn)CloneNode(_return));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // AST.ASubroutine is different from NCSDecomp.ASubroutine, so use DefaultIn
            sw.DefaultIn(this);
        }

        public PCommandBlock GetCommandBlock()
        {
            return _commandBlock;
        }

        public void SetCommandBlock(PCommandBlock node)
        {
            if (_commandBlock != null)
            {
                _commandBlock.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _commandBlock = node;
        }

        public PReturn GetReturn()
        {
            return _return;
        }

        public void SetReturn(PReturn node)
        {
            if (_return != null)
            {
                _return.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _return = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_commandBlock == child)
            {
                _commandBlock = null;
                return;
            }
            if (_return == child)
            {
                _return = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_commandBlock == oldChild)
            {
                SetCommandBlock((PCommandBlock)newChild);
                return;
            }
            if (_return == oldChild)
            {
                SetReturn((PReturn)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_commandBlock) + ToString(_return);
        }
    }
}





