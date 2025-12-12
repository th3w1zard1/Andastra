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
    public class ObjectConst : Const
    {
        private int value;
        public ObjectConst(object value)
        {
            this.type = new UtilsType((byte)6);
            this.value = (int)value;
            this.size = 1;
        }

        public virtual int Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            if (this.value == 0)
            {
                return "OBJECT_SELF";
            }

            if (this.value == 1)
            {
                return "OBJECT_INVALID";
            }

            return this.value.ToString();
        }
    }
}




