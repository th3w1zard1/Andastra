// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
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

        public virtual void Done()
        {
            this.nodedata = null;
        }

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
            this.nodedata.SetPos(node, this.currentPos);
        }
    }
}




