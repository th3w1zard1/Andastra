// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ABreakStatement : ScriptNode
    {
        public override string ToString()
        {
            return this.tabs.ToString() + "break;" + this.newline;
        }
    }
}




