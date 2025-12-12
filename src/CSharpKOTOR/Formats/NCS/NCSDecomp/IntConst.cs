// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    public class IntConst : Const
    {
        private long value;
        public IntConst(object value)
        {
            this.type = new UtilsType((byte)3);
            this.value = (long)value;
            this.size = 1;
        }

        public virtual long Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            if (this.value == Long.ParseLong("FFFFFFFF", 16))
            {
                return "0xFFFFFFFF";
            }

            return this.value.ToString();
        }
    }
}




