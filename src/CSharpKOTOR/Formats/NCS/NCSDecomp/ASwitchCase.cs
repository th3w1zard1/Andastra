// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ASwitchCase : ScriptRootNode
    {
        protected AConst val;
        public ASwitchCase(int start, AConst val) : base(start, -1)
        {
            this.Val(val);
        }

        public ASwitchCase(int start) : base(start, -1)
        {
        }

        public virtual void End(int end)
        {
            this.end = end;
        }

        private void Val(AConst val)
        {
            val.Parent(this);
            this.val = val;
        }

        public virtual List<object> GetUnknowns()
        {
            List<object> unks = new List<object>();
            foreach (ScriptNode node in this.children)
            {
                if (node is AUnkLoopControl)
                {
                    unks.Add(node);
                }
            }

            return unks;
        }

        public virtual void ReplaceUnknown(AUnkLoopControl unk, ScriptNode newnode)
        {
            newnode.Parent(this);
            int index = -1;
            for (int i = 0; i < this.children.Count; i++)
            {
                if (this.children[i] == unk)
                {
                    index = i;
                    break;
                }
            }
            if (index >= 0)
            {
                this.children[index] = newnode;
            }
            unk.Parent(null);
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            if (this.val == null)
            {
                buff.Append(this.tabs.ToString() + "default:" + this.newline);
            }
            else
            {
                buff.Append(this.tabs.ToString() + "case " + this.val.ToString() + ":" + this.newline);
            }

            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            return buff.ToString();
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.val != null)
            {
                this.val.Dispose();
            }

            this.val = null;
        }
    }
}




