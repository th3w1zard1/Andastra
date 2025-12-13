// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:14-39
// Original: public class SetPositions extends PrunedReversedDepthFirstAdapter
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class SetPositions : PrunedReversedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:15
        // Original: private NodeAnalysisData nodedata;
        private NodeAnalysisData nodedata;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:16
        // Original: private int currentPos;
        private int currentPos;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:18-21
        // Original: public SetPositions(NodeAnalysisData nodedata) { this.nodedata = nodedata; this.currentPos = 0; }
        public SetPositions(NodeAnalysisData nodedata)
        {
            this.nodedata = nodedata;
            this.currentPos = 0;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:23-25
        // Original: public void done() { this.nodedata = null; }
        public virtual void Done()
        {
            this.nodedata = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:27-33
        // Original: @Override public void defaultIn(Node node) { int pos = NodeUtils.getCommandPos(node); if (pos > 0) { this.currentPos = pos; } }
        public override void DefaultIn(Node node)
        {
            int pos = NodeUtils.GetCommandPos(node);
            if (pos > 0)
            {
                this.currentPos = pos;
            }
        }

        public override void DefaultOut(Node node)
        {
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetPositions.java:36-38
            // Original: this.nodedata.setPos(node, this.currentPos);
            // Always add node to hashtable, even if GetCommandPos returned -1
            // This ensures ProcessCode can find the node later
            // SetPos will create NodeData if it doesn't exist, so all nodes will be in hashtable
            this.nodedata.SetPos(node, this.currentPos);
        }
    }
}




