// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AIf : AControlLoop
    {
        public AIf(int start, int end, AExpression condition) : base(start, end)
        {
            this.Condition(condition);
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs.ToString() + "if (" + this.condition.ToString() + ") {" + this.newline);
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            buff.Append(this.tabs.ToString() + "}" + this.newline);
            return buff.ToString();
        }
    }
}




