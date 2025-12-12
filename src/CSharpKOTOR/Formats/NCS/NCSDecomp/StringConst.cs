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
    public class StringConst : Const
    {
        private string value;
        public StringConst(object value)
        {
            this.type = new UtilsType((byte)5);
            this.value = (string)value;
            this.size = 1;
        }

        public virtual string Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            return this.value.ToString();
        }
    }
}




