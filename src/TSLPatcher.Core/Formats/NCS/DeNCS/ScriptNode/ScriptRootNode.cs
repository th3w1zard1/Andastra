using System.Collections.Generic;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class ScriptRootNode : ScriptNode
    {
        private List<ScriptNode> _children;
        private int _start;
        private int _end;

        public ScriptRootNode() : this(0, 0)
        {
        }

        public ScriptRootNode(int start, int end)
        {
            _children = new List<ScriptNode>();
            _start = start;
            _end = end;
        }

        public void AddChild(ScriptNode child)
        {
            child.SetParent(this);
            _children.Add(child);
        }

        public void AddChildren(List<ScriptNode> children)
        {
            foreach (var child in children)
            {
                AddChild(child);
            }
        }

        public void RemoveChild(ScriptNode child)
        {
            if (_children.Contains(child))
            {
                _children.Remove(child);
                child.SetParent(null);
            }
        }

        public List<ScriptNode> RemoveChildren()
        {
            var children = new List<ScriptNode>(_children);
            foreach (var child in children)
            {
                child.SetParent(null);
            }
            _children.Clear();
            return children;
        }

        public ScriptNode RemoveLastChild()
        {
            if (_children.Count == 0)
            {
                return null;
            }
            var child = _children[_children.Count - 1];
            _children.RemoveAt(_children.Count - 1);
            child.SetParent(null);
            return child;
        }

        public List<ScriptNode> GetChildren()
        {
            return _children;
        }

        public int Size()
        {
            return _children.Count;
        }

        public ScriptNode GetLastChild()
        {
            if (_children.Count == 0)
            {
                return null;
            }
            return _children[_children.Count - 1];
        }

        public int GetStart()
        {
            return _start;
        }

        public void SetStart(int start)
        {
            _start = start;
        }

        public int GetEnd()
        {
            return _end;
        }

        public void SetEnd(int end)
        {
            _end = end;
        }

        public override void Close()
        {
            base.Close();
            if (_children != null)
            {
                foreach (var child in _children)
                {
                    child.Close();
                }
            }
            _children = null;
        }
    }
}

