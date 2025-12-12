//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class NodeAnalysisData
    {
        private Dictionary<object, object> nodedatahash;
        public NodeAnalysisData()
        {
            this.nodedatahash = new Dictionary<object, object>();
        }

        private NodeData GetOrCreateNodeData(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                NodeData created = new NodeData();
                this.nodedatahash[node] = created;
                return created;
            }

            return (NodeData)existing;
        }

        public virtual void Dispose()
        {
            if (this.nodedatahash != null)
            {
                foreach (NodeData data in this.nodedatahash.Values)
                {
                    data.Dispose();
                }

                this.nodedatahash = null;
            }
        }

        public virtual void SetPos(Node node, int pos)
        {
            object existing;
            NodeData data;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                data = new NodeData(pos);
                this.nodedatahash[node] = data;
            }
            else
            {
                data = (NodeData)existing;
                data.pos = pos;
            }
        }

        public virtual int GetPos(Node node)
        {
            if (node == null)
            {
                return -1;
            }

            object existing;
            if (this.nodedatahash.TryGetValue(node, out existing))
            {
                return ((NodeData)existing).pos;
            }

            return -1;
        }

        public virtual void SetDestination(Node jump, Node destination)
        {
            NodeData data = this.GetOrCreateNodeData(jump);
            data.jumpDestination = destination;
        }

        public virtual Node GetDestination(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return null;
            }

            return ((NodeData)existing).jumpDestination;
        }

        public virtual void SetCodeState(Node node, byte state)
        {
            NodeData data = this.GetOrCreateNodeData(node);
            data.state = state;
        }

        public virtual void DeadCode(Node node, bool deadcode)
        {
            NodeData data = this.GetOrCreateNodeData(node);
            data.state = deadcode ? (byte)1 : (byte)0;
        }

        public virtual bool DeadCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return false;
            }

            NodeData data = (NodeData)existing;
            return data.state == 1 || data.state == 3;
        }

        public virtual bool ProcessCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return false;
            }

            NodeData data = (NodeData)existing;
            return data.state != 1;
        }

        public virtual void LogOrCode(Node node, bool logor)
        {
            NodeData data = this.GetOrCreateNodeData(node);
            data.state = logor ? (byte)2 : (byte)0;
        }

        public virtual bool LogOrCode(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return false;
            }

            return ((NodeData)existing).state == 2;
        }

        public virtual void AddOrigin(Node node, Node origin)
        {
            NodeData data = this.GetOrCreateNodeData(node);
            data.AddOrigin(origin);
        }

        public virtual Node RemoveLastOrigin(Node node)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                return null;
            }

            NodeData data = (NodeData)existing;
            if (data.origins == null || data.origins.Count == 0)
            {
                return null;
            }

            object removed = data.origins[data.origins.Count - 1];
            data.origins.RemoveAt(data.origins.Count - 1);
            return (Node)removed;
        }

        public virtual void SetStack(Node node, LocalStack stack, bool overwrite)
        {
            object existing;
            if (!this.nodedatahash.TryGetValue(node, out existing))
            {
                NodeData data = new NodeData();
                data.stack = stack;
                this.nodedatahash[node] = data;
            }
            else
            {
                NodeData data = (NodeData)existing;
                if (data.stack == null || overwrite)
                {
                    data.stack = stack;
                }
            }
        }

        public virtual LocalStack GetStack(Node node)
        {
            object existing;
            if (this.nodedatahash.TryGetValue(node, out existing))
            {
                return ((NodeData)existing).stack;
            }

            return null;
        }

        public virtual void ClearProtoData()
        {
            foreach (NodeData e in this.nodedatahash.Values)
            {
                e.stack = null;
            }
        }

        public class NodeData
        {
            public static readonly byte STATE_NORMAL = 0;
            public static readonly byte STATE_DEAD = 1;
            public static readonly byte STATE_LOGOR = 2;
            public static readonly byte STATE_DEAD_PROCESS = 3;
            public int pos;
            public Node jumpDestination;
            public LocalStack stack;
            public byte state;
            public List<object> origins;
            public NodeData()
            {
                this.pos = -1;
                this.jumpDestination = null;
                this.stack = null;
                this.state = 0;
            }

            public NodeData(int pos)
            {
                this.jumpDestination = null;
                this.pos = pos;
                this.stack = null;
                this.state = 0;
            }

            public virtual void AddOrigin(Node origin)
            {
                if (this.origins == null)
                {
                    this.origins = new List<object>();
                }

                this.origins.Add(origin);
            }

            public virtual void Dispose()
            {
                this.jumpDestination = null;
                this.stack = null;
                this.origins = null;
            }
        }
    }
}




