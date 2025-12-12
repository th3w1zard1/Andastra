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
    public class SubroutinePathFinder : PrunedDepthFirstAdapter
    {
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private SubroutineState state;
        private bool pathfailed;
        private bool forcejump;
        private HashMap destinationcommands;
        private bool limitretries;
        private int maxretry;
        private int retry;
        public SubroutinePathFinder(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, int pass)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = state;
            this.pathfailed = false;
            this.forcejump = false;
            this.limitretries = (pass < 3);
            switch (pass)
            {
                case 0:
                    {
                        this.maxretry = 10;
                        break;
                    }

                case 1:
                    {
                        this.maxretry = 15;
                        break;
                    }

                case 2:
                    {
                        this.maxretry = 25;
                        break;
                    }

                default:
                    {
                        this.maxretry = 9999;
                        break;
                    }
            }

            this.retry = 0;
        }

        public virtual void Done()
        {
            this.nodedata = null;
            this.subdata = null;
            this.state = null;
            this.destinationcommands = null;
        }

        public override void InASubroutine(ASubroutine node)
        {
            this.state.StartPrototyping();
        }

        public override void CaseACommandBlock(ACommandBlock node)
        {
            this.InACommandBlock(node);
            TypedLinkedList commands = node.GetCmd();
            this.SetupDestinationCommands(commands, node);
            for (int i = 0; i < commands.Count; ++i)
            {
                if (this.forcejump)
                {
                    int nextPos = this.state.GetCurrentDestination();
                    i = (int)this.destinationcommands[nextPos];
                    this.forcejump = false;
                }
                else if (this.pathfailed)
                {
                    int nextPos = this.state.SwitchDecision();
                    if (nextPos == -1 || (this.limitretries && this.retry > this.maxretry))
                    {
                        this.state.StopPrototyping(false);
                        return;
                    }

                    i = (int)this.destinationcommands[nextPos];
                    this.pathfailed = false;
                    ++this.retry;
                }

                if (i < commands.Count)
                {
                    ((Node)commands[i]).Apply(this);
                }
            }

            commands = null;
            this.OutACommandBlock(node);
        }

        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            NodeUtils.GetNextCommand(node, this.nodedata);
            if (!this.nodedata.LogOrCode(node))
            {
                this.state.AddDecision(node, NodeUtils.GetJumpDestinationPos(node));
            }
        }

        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (NodeUtils.GetJumpDestinationPos(node) < this.nodedata.GetPos(node))
            {
                this.pathfailed = true;
            }
            else
            {
                this.state.AddJump(node, NodeUtils.GetJumpDestinationPos(node));
                this.forcejump = true;
            }
        }

        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            if (!this.subdata.IsPrototyped(NodeUtils.GetJumpDestinationPos(node), true))
            {
                this.pathfailed = true;
            }
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

        private void SetupDestinationCommands(TypedLinkedList commands, Node ast)
        {
            this.destinationcommands = new HashMap();
            ast.Apply(new AnonymousPrunedDepthFirstAdapter(this, commands));
        }

        private sealed class AnonymousPrunedDepthFirstAdapter : PrunedDepthFirstAdapter
        {
            public AnonymousPrunedDepthFirstAdapter(SubroutinePathFinder parent, TypedLinkedList commands)
            {
                this.parent = parent;
                this.commands = commands;
            }

            private readonly SubroutinePathFinder parent;
            private readonly TypedLinkedList commands;
            public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
            {
                int pos = NodeUtils.GetJumpDestinationPos(node);
                this.parent.destinationcommands.Put(pos, this.parent.GetCommandIndexByPos(pos, this.commands));
            }

            public override void OutAJumpCommand(AJumpCommand node)
            {
                int pos = NodeUtils.GetJumpDestinationPos(node);
                this.parent.destinationcommands.Put(pos, this.parent.GetCommandIndexByPos(pos, this.commands));
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

        private int GetCommandIndexByPos(int pos, TypedLinkedList commands)
        {
            Node node;
            int i;
            for (node = (Node)commands[0], i = 1; i < commands.Count && this.nodedata.GetPos(node) < pos; ++i)
            {
                node = (Node)commands[i];
                if (this.nodedata.GetPos(node) == pos)
                {
                    break;
                }
            }

            if (this.nodedata.GetPos(node) > pos)
            {
                throw new Exception("Unable to locate a command with position " + pos);
            }

            return i;
        }
    }
}




