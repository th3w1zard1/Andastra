// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class NoCast : ICast
    {
        public static readonly NoCast instance;
        static NoCast()
        {
            instance = new NoCast();
        }

        private NoCast()
        {
        }

        public virtual object Cast(object o)
        {
            return o;
        }
    }
}




