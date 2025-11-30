using NodeType = TSLPatcher.Core.Formats.NCS.DeNCS.Node.Node;
using TSLPatcher.Core.Formats.NCS.DeNCS.Analysis;
using TSLPatcher.Core.Formats.NCS.DeNCS.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public class SetPositions : PrunedReversedDepthFirstAdapter
    {
        private NodeAnalysisData nodedata;
        private int currentPos;

        public SetPositions(NodeAnalysisData nodedata)
        {
            this.nodedata = nodedata;
            this.currentPos = 0;
        }

        public void Done()
        {
            this.nodedata = null;
        }

        public override void DefaultIn(NodeType node)
        {
            int pos = NodeUtils.GetCommandPos(node);
            if (pos > 0)
            {
                this.currentPos = pos;
            }
        }

        public override void DefaultOut(NodeType node)
        {
            this.nodedata.SetPos(node, this.currentPos);
        }
    }
}

