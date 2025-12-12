// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public abstract class ScriptRootNode : ScriptNode
    {
        protected LinkedList children;
        protected int start;
        protected int end;
        protected ScriptRootNode(int start, int end)
        {
            this.children = new LinkedList();
            this.start = start;
            this.end = end;
        }

        public virtual void AddChild(ScriptNode child)
        {
            this.children.Add(child);
            child.Parent(this);
        }

        public virtual void AddChildren(List<object> children)
        {
            foreach (object child in children)
            {
                this.AddChild((ScriptNode)child);
            }
        }

        public virtual List<object> RemoveChildren(int first, int last)
        {
            List<object> removed = new List<object>(last - first + 1);
            for (int i = 0; i <= last - first; ++i)
            {
                removed.Add(this.RemoveChild(first));
            }

            return removed;
        }

        public virtual List<object> RemoveChildren(int first)
        {
            return this.RemoveChildren(first, this.children.Count - 1);
        }

        public virtual List<object> RemoveChildren()
        {
            return this.RemoveChildren(0, this.children.Count - 1);
        }

        public virtual ScriptNode RemoveLastChild()
        {
            if (this.children.Count == 0)
                return null;
            var lastNode = this.children[this.children.Count - 1];
            this.children.RemoveAt(this.children.Count - 1);
            return (ScriptNode)lastNode;
        }

        public virtual void RemoveChild(ScriptNode child)
        {
            bool removed = this.children.Remove(child);
            if (removed)
            {
                child.Parent(null);
            }
        }

        public virtual ScriptNode RemoveChild(int index)
        {
            ScriptNode child = (ScriptNode)this.children[index];
            this.children.RemoveAt(index);
            child.Parent(null);
            return child;
        }

        public virtual ScriptNode GetLastChild()
        {
            if (this.children.Count == 0)
                return null;
            return (ScriptNode)this.children[this.children.Count - 1];
        }

        public virtual ScriptNode GetPreviousChild(int pos)
        {
            if (this.children.Count < pos)
            {
                return null;
            }

            return (ScriptNode)this.children[this.children.Count - pos];
        }

        public virtual bool HasChildren()
        {
            return this.children.Count > 0;
        }

        public virtual int GetEnd()
        {
            return this.end;
        }

        public virtual int GetStart()
        {
            return this.start;
        }

        public virtual LinkedList GetChildren()
        {
            return this.children;
        }

        public virtual int GetChildLocation(ScriptNode child)
        {
            for (int i = 0; i < this.children.Count; i++)
            {
                if (this.children[i] == child)
                    return i;
            }
            return -1;
        }

        public virtual void ReplaceChild(ScriptNode oldchild, ScriptNode newchild)
        {
            int index = this.GetChildLocation(oldchild);
            this.children[index] = newchild;
            newchild.Parent(this);
            oldchild.Parent(null);
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            return buff.ToString();
        }

        public virtual int Size()
        {
            return this.children.Count;
        }

        public override void Dispose()
        {
            base.Dispose();
            foreach (object child in this.children)
            {
                if (child is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            this.children = null;
        }
    }
}




