// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StringConst.java:13-30
// Original: public class StringConst extends Const { private String value; public StringConst(String value) { this.type = new Type((byte)5); this.value = value; this.size = 1; } public String value() { return this.value; } @Override public String toString() { return this.value.toString(); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Utils;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.Stack
{
    public class StringConst : Const
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StringConst.java:14-20
        // Original: private String value; public StringConst(String value) { this.type = new Type((byte)5); this.value = value; this.size = 1; }
        private string value;
        public StringConst(string value)
        {
            this.type = new Utils.Type((byte)5);
            this.value = value;
            this.size = 1;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StringConst.java:22-24
        // Original: public String value() { return this.value; }
        public virtual string Value()
        {
            return this.value;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StringConst.java:26-29
        // Original: return this.value.toString();
        public override string ToString()
        {
            return this.value.ToString();
        }
    }
}




