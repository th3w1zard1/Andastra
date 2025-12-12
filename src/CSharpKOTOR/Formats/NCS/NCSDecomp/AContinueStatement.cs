// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AContinueStatement : ScriptNode
    {
        public override string ToString()
        {
            return this.tabs.ToString() + "continue;" + this.newline;
        }
    }
}




