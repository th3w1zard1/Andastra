// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ADoLoop : AControlLoop
    {
        public ADoLoop(int start, int end) : base(start, end)
        {
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs);
            buff.Append("do {" + this.newline);
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            if (this.condition != null)
            {
                buff.Append(this.tabs.ToString() + "} while (" + this.condition.ToString() + ");" + this.newline);
            }
            else
            {
                buff.Append(this.tabs.ToString() + "} while (1);" + this.newline);
            }

            return buff.ToString();
        }
    }
}




