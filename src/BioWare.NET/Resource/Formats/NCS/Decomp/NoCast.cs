// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:10-25
// Original: public class NoCast implements Cast<Object>
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp
{
    public class NoCast : ICast
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:11
        // Original: public static final NoCast instance = new NoCast();
        public static readonly NoCast instance;
        static NoCast()
        {
            instance = new NoCast();
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:13-15
        // Original: private NoCast() { }
        private NoCast()
        {
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/NoCast.java:17-23
        // Original: @Override public Object cast(Object o) { return o; }
        public virtual object Cast(object o)
        {
            return o;
        }
    }
}




