using NodeType = TSLPatcher.Core.Formats.NCS.DeNCS.Node.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Analysis
{
    public abstract class AnalysisAdapter
    {
        public virtual void DefaultIn(NodeType node)
        {
        }

        public virtual void DefaultOut(NodeType node)
        {
        }
    }
}

