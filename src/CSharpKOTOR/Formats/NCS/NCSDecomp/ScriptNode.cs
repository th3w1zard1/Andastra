// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public abstract class ScriptNode
    {
        private ScriptNode parent;
        protected string tabs;
        protected string newline;
        public ScriptNode()
        {
            this.newline = JavaSystem.GetProperty("line.separator");
        }

        public virtual ScriptNode Parent()
        {
            return this.parent;
        }

        public virtual void Parent(ScriptNode parent)
        {
            this.parent = parent;
            if (parent != null)
            {
                this.tabs = parent.tabs.ToString() + "\t";
            }
        }

        public virtual void Dispose()
        {
            this.parent = null;
        }

        public virtual string GetTabs()
        {
            return this.tabs;
        }

        public virtual string GetNewline()
        {
            return this.newline;
        }
    }
}




