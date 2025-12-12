// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ACodeBlock : ScriptRootNode
    {
        public ACodeBlock(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs.ToString() + "{" + this.newline);
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            buff.Append(this.tabs.ToString() + "}" + this.newline);
            return buff.ToString();
        }
    }
}




