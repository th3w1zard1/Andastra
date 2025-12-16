using System;
using System.Collections.Generic;
using BioWareCSharp.Common.Formats.NCS.NCSDecomp.AST;

namespace BioWareCSharp.Common.Formats.NCS.NCSDecomp.Utils
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





