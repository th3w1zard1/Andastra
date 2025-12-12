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
    public class SetDestinations : PrunedDepthFirstAdapter
    {
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private Node destination;
#pragma warning disable CS0414
        private readonly int currentPos;
#pragma warning restore CS0414
        private Node ast;
#pragma warning disable CS0414
        private readonly int actionarg;
#pragma warning restore CS0414
        private Dictionary<object, object> origins;
        //private bool deadcode;
        public SetDestinations(Node ast, NodeAnalysisData nodedata, SubroutineAnalysisData subdata)
        {
            this.nodedata = nodedata;
            this.currentPos = 0;
            this.ast = ast;
            this.subdata = subdata;
            this.actionarg = 0;
            this.origins = new Dictionary<object, object>();
        }

        public virtual void Done()
        {
            this.nodedata = null;
            this.subdata = null;
            this.destination = null;
            this.ast = null;
            this.origins = null;
        }

        public virtual Dictionary<object, object> GetOrigins()
        {
            return this.origins;
        }

        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            int pos = NodeUtils.GetJumpDestinationPos(node);
            this.LookForPos(pos, true);
            if (this.destination == null)
            {
                throw new Exception("wasn't able to find dest for " + node + " at pos " + pos);
            }

            this.nodedata.SetDestination(node, this.destination);
            this.AddDestination(node, this.destination);
        }

        public override void OutAJumpCommand(AJumpCommand node)
        {
            int pos = NodeUtils.GetJumpDestinationPos(node);
            this.LookForPos(pos, true);
            if (this.destination == null)
            {
                throw new Exception("wasn't able to find dest for " + node + " at pos " + pos);
            }

            this.nodedata.SetDestination(node, this.destination);
            if (pos < this.nodedata.GetPos(node))
            {
                Node dest = NodeUtils.GetCommandChild(this.destination);
                this.nodedata.AddOrigin(dest, node);
            }

            this.AddDestination(node, this.destination);
        }

        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            int pos = NodeUtils.GetJumpDestinationPos(node);
            this.LookForPos(pos, false);
            if (this.destination == null)
            {
                throw new Exception("wasn't able to find dest for " + node + " at pos " + pos);
            }

            this.nodedata.SetDestination(node, this.destination);
            this.AddDestination(node, this.destination);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDestinations.java:100-108
        // Original: private void addDestination(Node origin, Node destination)
        private void AddDestination(Node origin, Node destination)
        {
            object originsListObj = this.origins.ContainsKey(destination) ? this.origins[destination] : null;
            List<object> originsList = originsListObj as List<object>;
            if (originsList == null)
            {
                originsList = new List<object>(1);
                this.origins[destination] = originsList;
            }

            originsList.Add(origin);
        }

        private int GetPos(Node node)
        {
            return this.nodedata.GetPos(node);
        }

        private void LookForPos(int pos, bool needcommand)
        {
            this.destination = null;
            this.ast.Apply(new AnonymousPrunedDepthFirstAdapter(this, pos, needcommand));
        }

        private sealed class AnonymousPrunedDepthFirstAdapter : PrunedDepthFirstAdapter
        {
            public AnonymousPrunedDepthFirstAdapter(SetDestinations parent, int pos, bool needcommand)
            {
                this.parent = parent;
                this.pos = pos;
                this.needcommand = needcommand;
            }

            private readonly SetDestinations parent;
            private int pos;
            private bool needcommand;
            public override void DefaultIn(Node node)
            {
                if (this.parent.GetPos(node) == this.pos && this.parent.destination == null && (!this.needcommand || NodeUtils.IsCommandNode(node)))
                {
                    this.parent.destination = node;
                }
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDestinations.java:127-159
            // Original: @Override public void caseAProgram(AProgram node)
            public override void CaseAProgram(AProgram node)
            {
                this.InAProgram(node);
                if (node.GetReturn() != null)
                {
                    node.GetReturn().Apply(this);
                }

                Object[] temp = node.GetSubroutine().ToArray();
                int cur = temp.Length / 2;
                int min = 0;
                int max = temp.Length - 1;
                // Matching Java for loop: for (boolean done = ...; !done; done = done || ...)
                bool done = this.parent.destination != null || cur >= temp.Length;
                while (!done)
                {
                    PSubroutine sub = (PSubroutine)temp[cur];
                    if (this.parent.GetPos(sub) > this.pos)
                    {
                        max = cur;
                        cur = (min + cur) / 2;
                    }
                    else if (this.parent.GetPos(sub) == this.pos)
                    {
                        sub.Apply(this);
                        done = true;
                    }
                    else if (cur >= max - 1)
                    {
                        sub.Apply(this);
                        ++cur;
                    }
                    else
                    {
                        min = cur;
                        cur = (cur + max) / 2;
                    }
                    // Update expression from Java for loop: done = done || SetDestinations.this.destination != null || cur > max
                    done = done || this.parent.destination != null || cur > max;
                }

                this.OutAProgram(node);
            }

            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDestinations.java:162-188
            // Original: @Override public void caseACommandBlock(ACommandBlock node)
            public override void CaseACommandBlock(ACommandBlock node)
            {
                this.InACommandBlock(node);
                Object[] temp = node.GetCmd().ToArray();
                int cur = temp.Length / 2;
                int min = 0;
                int max = temp.Length - 1;
                // Matching Java for loop: for (boolean done = ...; !done; done = done || ...)
                bool done = this.parent.destination != null || cur >= temp.Length;
                while (!done)
                {
                    PCmd cmd = (PCmd)temp[cur];
                    if (this.parent.GetPos(cmd) > this.pos)
                    {
                        max = cur;
                        cur = (min + cur) / 2;
                    }
                    else if (this.parent.GetPos(cmd) == this.pos)
                    {
                        cmd.Apply(this);
                        done = true;
                    }
                    else if (cur >= max - 1)
                    {
                        cmd.Apply(this);
                        ++cur;
                    }
                    else
                    {
                        min = cur;
                        cur = (cur + max) / 2;
                    }
                    // Update expression from Java for loop: done = done || SetDestinations.this.destination != null || cur > max
                    done = done || this.parent.destination != null || cur > max;
                }
            }
        }

    }
}




