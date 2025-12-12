// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using CSharpKOTOR.Formats.NCS.NCSDecomp.AST;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class FlattenSub : PrunedDepthFirstAdapter
    {
        private ASubroutine sub;
        private bool actionjumpfound;
        private int i;
        private TypedLinkedList commands;
        private NodeAnalysisData nodedata;
        public FlattenSub(ASubroutine sub, NodeAnalysisData nodedata)
        {
            this.SetSub(sub);
            this.actionjumpfound = false;
            this.nodedata = nodedata;
        }

        public virtual void Done()
        {
            this.sub = null;
            this.commands = null;
            this.nodedata = null;
        }

        public virtual void SetSub(ASubroutine sub)
        {
            this.sub = sub;
        }

        public override void CaseACommandBlock(ACommandBlock node)
        {
            this.commands = node.GetCmd();
            this.i = 0;
            while (this.i < this.commands.Count)
            {
                ((Node)this.commands[this.i]).Apply(this);
                if (this.actionjumpfound)
                {
                    this.actionjumpfound = false;
                }
                else
                {
                    ++this.i;
                }
            }
        }

        public override void CaseAActionJumpCmd(AActionJumpCmd node)
        {
            AStoreStateCommand sscommand = (AStoreStateCommand)node.GetStoreStateCommand();
            AJumpCommand jmpcommand = (AJumpCommand)node.GetJumpCommand();
            ACommandBlock cmdblock = (ACommandBlock)node.GetCommandBlock();
            AReturn rtn = (AReturn)node.GetReturn();
            AStoreStateCmd sscmd = new AStoreStateCmd(sscommand);
            AJumpCmd jmpcmd = new AJumpCmd(jmpcommand);
            AReturnCmd rtncmd = new AReturnCmd(rtn);
            this.nodedata.SetPos(sscmd, this.nodedata.GetPos(sscommand));
            this.nodedata.SetPos(jmpcmd, this.nodedata.GetPos(jmpcommand));
            this.nodedata.SetPos(rtncmd, this.nodedata.GetPos(rtn));
            int j = this.i;
            this.commands[j++] = sscmd;
            this.commands.Add(j++, jmpcmd);
            TypedLinkedList subcmds = cmdblock.GetCmd();
            while (subcmds.Count > 0)
            {
                this.commands.Add(j++, subcmds.Remove(0));
            }

            this.commands.Add(j++, rtncmd);
            subcmds = null;
        }

        public override void CaseAAddVarCmd(AAddVarCmd node)
        {
        }

        public override void CaseAConstCmd(AConstCmd node)
        {
        }

        public override void CaseACopydownspCmd(ACopydownspCmd node)
        {
        }

        public override void CaseACopytopspCmd(ACopytopspCmd node)
        {
        }

        public override void CaseACopydownbpCmd(ACopydownbpCmd node)
        {
        }

        public override void CaseACopytopbpCmd(ACopytopbpCmd node)
        {
        }

        public override void CaseAMovespCmd(AMovespCmd node)
        {
        }

        public override void CaseALogiiCmd(ALogiiCmd node)
        {
        }

        public override void CaseAUnaryCmd(AUnaryCmd node)
        {
        }

        public override void CaseABinaryCmd(ABinaryCmd node)
        {
        }

        public override void CaseADestructCmd(ADestructCmd node)
        {
        }

        public override void CaseABpCmd(ABpCmd node)
        {
        }

        public override void CaseAActionCmd(AActionCmd node)
        {
        }

        public override void CaseAStackOpCmd(AStackOpCmd node)
        {
        }
    }
}




