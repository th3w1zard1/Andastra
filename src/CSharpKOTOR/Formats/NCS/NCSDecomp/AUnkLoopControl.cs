// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AUnkLoopControl : ScriptNode
    {
        protected int dest;
        public AUnkLoopControl(int dest)
        {
            this.dest = dest;
        }

        public virtual int GetDestination()
        {
            return this.dest;
        }

        public override string ToString()
        {
            return "BREAK or CONTINUE undetermined";
        }
    }
}




