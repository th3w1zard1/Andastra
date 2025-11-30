using System.Collections.Generic;
using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;
using NodeType = TSLPatcher.Core.Formats.NCS.DeNCS.Node.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public class NodeAnalysisData
    {
        private Dictionary<NodeType, NodeData> nodedatahash;

        public NodeAnalysisData()
        {
            this.nodedatahash = new Dictionary<NodeType, NodeData>(1);
        }

        public void Close()
        {
            if (this.nodedatahash != null)
            {
                foreach (NodeData data in this.nodedatahash.Values)
                {
                    data.Close();
                }
                this.nodedatahash = null;
            }
        }

        public void SetPos(NodeType node, int pos)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                this.nodedatahash[node] = new NodeData(pos);
            }
            else
            {
                this.nodedatahash[node].pos = pos;
            }
        }

        public int GetPos(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to read position on a node not in the hashtable.");
            }
            return this.nodedatahash[node].pos;
        }

        public void SetDestination(NodeType jump, NodeType destination)
        {
            if (!this.nodedatahash.ContainsKey(jump))
            {
                NodeData data = new NodeData();
                data.jumpDestination = destination;
                this.nodedatahash[jump] = data;
            }
            else
            {
                this.nodedatahash[jump].jumpDestination = destination;
            }
        }

        public NodeType GetDestination(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to read destination on a node not in the hashtable.");
            }
            return this.nodedatahash[node].jumpDestination;
        }

        public void SetCodeState(NodeType node, byte state)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                NodeData data = new NodeData();
                data.state = state;
                this.nodedatahash[node] = data;
            }
            else
            {
                this.nodedatahash[node].state = state;
            }
        }

        public void DeadCode(NodeType node, bool deadcode)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to set status on a node not in the hashtable.");
            }
            if (deadcode)
            {
                this.nodedatahash[node].state = 1;
            }
            else
            {
                this.nodedatahash[node].state = 0;
            }
        }

        public bool DeadCode(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to read status on a node not in the hashtable.");
            }
            return this.nodedatahash[node].state == 1 || this.nodedatahash[node].state == 3;
        }

        public bool ProcessCode(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to read status on a node not in the hashtable.");
            }
            return this.nodedatahash[node].state != 1;
        }

        public void LogOrCode(NodeType node, bool logor)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to set status on a node not in the hashtable.");
            }
            if (logor)
            {
                this.nodedatahash[node].state = 2;
            }
            else
            {
                this.nodedatahash[node].state = 0;
            }
        }

        public bool LogOrCode(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to read status on a node not in the hashtable.");
            }
            return this.nodedatahash[node].state == 2;
        }

        public void AddOrigin(NodeType node, NodeType origin)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                NodeData data = new NodeData();
                data.AddOrigin(origin);
                this.nodedatahash[node] = data;
            }
            else
            {
                this.nodedatahash[node].AddOrigin(origin);
            }
        }

        public NodeType RemoveLastOrigin(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                throw new RuntimeException("Attempted to read origin on a node not in the hashtable.");
            }
            NodeData data = this.nodedatahash[node];
            if (data.origins == null || data.origins.Count == 0)
            {
                return null;
            }
            NodeType result = data.origins[data.origins.Count - 1];
            data.origins.RemoveAt(data.origins.Count - 1);
            return result;
        }

        public void SetStack(NodeType node, LocalStack stack, bool overwrite)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                NodeData data = new NodeData();
                data.stack = stack;
                this.nodedatahash[node] = data;
            }
            else if (this.nodedatahash[node].stack == null || overwrite)
            {
                this.nodedatahash[node].stack = stack;
            }
        }

        public LocalStack GetStack(NodeType node)
        {
            if (!this.nodedatahash.ContainsKey(node))
            {
                return null;
            }
            return this.nodedatahash[node].stack;
        }

        public void ClearProtoData()
        {
            foreach (NodeData data in this.nodedatahash.Values)
            {
                data.stack = null;
            }
        }

        public class NodeData
        {
            public const byte STATE_NORMAL = 0;
            public const byte STATE_DEAD = 1;
            public const byte STATE_LOGOR = 2;
            public const byte STATE_DEAD_PROCESS = 3;

            public int pos;
            public NodeType jumpDestination;
            public LocalStack stack;
            public byte state;
            public List<NodeType> origins;

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

            public void AddOrigin(NodeType origin)
            {
                if (this.origins == null)
                {
                    this.origins = new List<NodeType>();
                }
                this.origins.Add(origin);
            }

            public void Close()
            {
                this.jumpDestination = null;
                this.stack = null;
                this.origins = null;
            }
        }
    }
}

