// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Parser
{
    sealed class State
    {
        internal int state;
        internal object node;
        internal State(int state, object node)
        {
            this.state = state;
            this.node = node;
        }
    }
}




