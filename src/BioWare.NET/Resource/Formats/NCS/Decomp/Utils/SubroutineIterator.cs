using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.Utils
{
    public class SubroutineIterator
    {
        private List<ASubroutine> subs;
        private int index;

        public SubroutineIterator(List<ASubroutine> subs)
        {
            this.subs = subs ?? new List<ASubroutine>();
            this.index = 0;
        }

        public bool HasNext()
        {
            return index < subs.Count;
        }

        public ASubroutine Next()
        {
            if (!HasNext())
            {
                throw new InvalidOperationException("No more subroutines");
            }
            ASubroutine result = subs[index];
            index++;
            return result;
        }
    }
}





