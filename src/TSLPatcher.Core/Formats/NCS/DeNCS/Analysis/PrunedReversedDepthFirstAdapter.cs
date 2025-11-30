using TSLPatcher.Core.Formats.NCS.DeNCS.Node;
using NodeType = TSLPatcher.Core.Formats.NCS.DeNCS.Node.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Analysis
{
    public abstract class PrunedReversedDepthFirstAdapter : AnalysisAdapter
    {
        public virtual void InStart(Start node)
        {
            DefaultIn(node);
        }

        public virtual void OutStart(Start node)
        {
            DefaultOut(node);
        }

        public virtual void CaseStart(Start node)
        {
            InStart(node);
            node.GetPProgram().Apply(this);
            OutStart(node);
        }

        public override void DefaultIn(NodeType node)
        {
            base.DefaultIn(node);
        }

        public override void DefaultOut(NodeType node)
        {
            base.DefaultOut(node);
        }

        public virtual void InACommandBlock(ACommandBlock node)
        {
            DefaultIn(node);
        }

        public virtual void OutACommandBlock(ACommandBlock node)
        {
            DefaultOut(node);
        }
    }
}

