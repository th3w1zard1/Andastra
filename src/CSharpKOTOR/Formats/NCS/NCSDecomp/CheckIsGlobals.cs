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
    public class CheckIsGlobals : PrunedReversedDepthFirstAdapter
    {
        private bool isGlobals;
        public CheckIsGlobals()
        {
            this.isGlobals = false;
        }

        public override void InABpCommand(ABpCommand node)
        {
            this.isGlobals = true;
        }

        public override void CaseACommandBlock(ACommandBlock node)
        {
            this.InACommandBlock(node);
            Object[] temp = node.GetCmd().ToArray();
            for (int i = temp.Length - 1; i >= 0; --i)
            {
                ((PCmd)temp[i]).Apply(this);
                if (this.isGlobals)
                {
                    return;
                }
            }

            this.OutACommandBlock(node);
        }

        public virtual bool GetIsGlobals()
        {
            return this.isGlobals;
        }
    }
}




