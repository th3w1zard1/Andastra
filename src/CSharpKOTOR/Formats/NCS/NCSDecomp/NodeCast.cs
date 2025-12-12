// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class NodeCast : ICast
    {
        public static readonly NodeCast instance;
        static NodeCast()
        {
            instance = new NodeCast();
        }

        private NodeCast()
        {
        }

        public virtual object Cast(object o)
        {
            return o;
        }
    }
}




