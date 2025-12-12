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
    public class SetDeadCode : PrunedDepthFirstAdapter
    {
        private const byte STATE_NORMAL = 0;
        private const byte STATE_JZ1_CP = 1;
        private const byte STATE_JZ2_JZ = 2;
        private const byte STATE_JZ3_CP2 = 3;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private int actionarg;
        private Dictionary<object, object> origins;
        private Dictionary<object, object> deadorigins;
        private byte deadstate;
        private byte state;
        public SetDeadCode(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, Dictionary<object, object> origins)
        {
            this.nodedata = nodedata;
            this.origins = origins;
            this.subdata = subdata;
            this.actionarg = 0;
            this.deadstate = 0;
            this.state = 0;
            this.deadorigins = new Dictionary<object, object>();
        }

        public virtual void Done()
        {
            this.nodedata = null;
            this.subdata = null;
            this.origins = null;
            this.deadorigins = null;
        }

        public override void DefaultIn(Node node)
        {
            if (this.actionarg > 0 && this.origins.ContainsKey(node))
            {
                --this.actionarg;
            }

            if (this.origins.ContainsKey(node))
            {
                this.deadstate = 0;
            }
            else if (this.deadorigins.ContainsKey(node))
            {
                this.deadstate = 3;
            }

            if (NodeUtils.IsCommandNode(node))
            {
                this.nodedata.SetCodeState(node, this.deadstate);
            }
        }

        public override void DefaultOut(Node node)
        {
            if (NodeUtils.IsCommandNode(node))
            {
                this.state = 0;
            }
        }

        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            if (this.deadstate == 1)
            {
                this.RemoveDestination(node, this.nodedata.GetDestination(node));
            }
            else if (this.deadstate == 3)
            {
                this.TransferDestination(node, this.nodedata.GetDestination(node));
            }

            if (NodeUtils.IsJz(node))
            {
                if (this.state == 1)
                {
                    ++this.state;
                    return;
                }

                if (this.state == 3)
                {
                    this.nodedata.LogOrCode(node, true);
                }
            }

            this.state = 0;
        }

        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            if (this.state == 0 || this.state == 2)
            {
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy == 1 && loc == 1)
                {
                    ++this.state;
                }
                else
                {
                    this.state = 0;
                }
            }
            else
            {
                this.state = 0;
            }
        }

        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (this.deadstate == 1)
            {
                this.RemoveDestination(node, this.nodedata.GetDestination(node));
            }
            else if (this.deadstate == 3)
            {
                this.TransferDestination(node, this.nodedata.GetDestination(node));
            }

            if (this.actionarg == 0)
            {
                this.deadstate = 3;
            }

            this.DefaultOut(node);
        }

        public override void OutAStoreStateCommand(AStoreStateCommand node)
        {
            ++this.actionarg;
            this.DefaultOut(node);
        }

        public virtual bool IsJumpToReturn(AJumpCommand node)
        {
            Node dest = this.nodedata.GetDestination(node);
            return typeof(AReturn).IsInstanceOfType(dest);
        }

        private void RemoveDestination(Node origin, Node destination)
        {
            this.RemoveDestination(origin, destination, this.origins);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:145-153
        // Original: private void removeDestination(Node origin, Node destination, Hashtable<Node, ArrayList<Node>> hash)
        private void RemoveDestination(Node origin, Node destination, Dictionary<object, object> hash)
        {
            object originListObj = hash.ContainsKey(destination) ? hash[destination] : null;
            List<object> originList = originListObj as List<object>;
            if (originList != null)
            {
                originList.Remove(origin);
                if (originList.Count == 0)
                {
                    hash.Remove(destination);
                }
            }
        }

        private void TransferDestination(Node origin, Node destination)
        {
            this.RemoveDestination(origin, destination, this.origins);
            this.AddDestination(origin, destination, this.deadorigins);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SetDeadCode.java:160-168
        // Original: private void addDestination(Node origin, Node destination, Hashtable<Node, ArrayList<Node>> hash)
        private void AddDestination(Node origin, Node destination, Dictionary<object, object> hash)
        {
            object originsListObj = hash.ContainsKey(destination) ? hash[destination] : null;
            List<object> originsList = originsListObj as List<object>;
            if (originsList == null)
            {
                originsList = new List<object>(1);
                hash[destination] = originsList;
            }

            originsList.Add(origin);
        }
    }
}




