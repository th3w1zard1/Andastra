//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using AST = CSharpKOTOR.Formats.NCS.NCSDecomp.AST;

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

        public override void CaseABpCommand(ABpCommand node)
        {
            // Matching DeNCS implementation: explicitly call InABpCommand
            this.InABpCommand(node);
        }

        // Handle AST.ABpCommand as well (from NcsToAstConverter)
        public override void CaseABpCommand(AST.ABpCommand node)
        {
            // Matching DeNCS implementation: set isGlobals directly
            this.isGlobals = true;
        }

        public override void CaseABpCmd(ABpCmd node)
        {
            // Matching DeNCS implementation: traverse into ABpCmd to reach ABpCommand
            this.InABpCmd(node);
            if (node.GetBpCommand() != null)
            {
                node.GetBpCommand().Apply(this);
            }
            if (!this.isGlobals)
            {
                this.OutABpCmd(node);
            }
        }

        // Handle AST.ABpCmd as well (from NcsToAstConverter)
        public override void CaseABpCmd(AST.ABpCmd node)
        {
            // Traverse into AST.ABpCmd to reach ABpCommand
            if (node.GetBpCommand() != null)
            {
                node.GetBpCommand().Apply(this);
            }
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

        // Handle AST.ACommandBlock as well (from NcsToAstConverter)
        public override void CaseACommandBlock(AST.ACommandBlock node)
        {
            // Traverse into AST.ACommandBlock to find ABpCommand
            Object[] temp = node.GetCmd().ToArray();
            for (int i = temp.Length - 1; i >= 0; --i)
            {
                ((PCmd)temp[i]).Apply(this);
                if (this.isGlobals)
                {
                    return;
                }
            }
        }


        public virtual bool GetIsGlobals()
        {
            return this.isGlobals;
        }
    }
}




