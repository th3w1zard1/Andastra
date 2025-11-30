using System;
using System.Collections.Generic;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public abstract class Node
    {
        private Node _parent;

        public Node Parent()
        {
            return _parent;
        }

        internal void SetParent(Node parent)
        {
            _parent = parent;
        }

        public abstract void RemoveChild(Node child);

        public abstract void ReplaceChild(Node oldChild, Node newChild);

        public void ReplaceBy(Node node)
        {
            if (_parent != null)
            {
                _parent.ReplaceChild(this, node);
            }
        }

        protected string ToString(Node node)
        {
            return node != null ? node.ToString() : "";
        }

        protected string ToString(List<Node> list)
        {
            if (list == null)
            {
                return "";
            }
            var sb = new System.Text.StringBuilder();
            foreach (var item in list)
            {
                if (item != null)
                {
                    sb.Append(item.ToString());
                }
            }
            return sb.ToString();
        }

        protected Node CloneNode(Node node)
        {
            if (node != null)
            {
                return (Node)node.Clone();
            }
            return null;
        }

        protected List<T> CloneList<T>(List<T> list) where T : Node
        {
            if (list == null)
            {
                return null;
            }
            var clone = new List<T>();
            foreach (var item in list)
            {
                if (item != null)
                {
                    clone.Add((T)item.Clone());
                }
            }
            return clone;
        }

        public abstract object Clone();

        public abstract void Apply(Analysis.AnalysisAdapter sw);

        public override string ToString()
        {
            return "";
        }
    }
}

