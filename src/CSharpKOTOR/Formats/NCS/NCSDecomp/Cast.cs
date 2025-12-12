// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public interface ICast
    {
        object Cast(object p0);
    }

    public abstract class Cast : ICast
    {
        object ICast.Cast(object p0)
        {
            return CastInternal(p0);
        }

        public abstract object CastInternal(object p0);
    }
}




