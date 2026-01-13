// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/TIncisp.java:9-33
// Original: public final class TIncisp extends Token { public TIncisp() { super.setText("INCISP"); } ... public void setText(String text) { throw new RuntimeException("Cannot change TIncisp text."); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Analysis;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp
{
    public sealed class TIncisp : Token
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/TIncisp.java:10-23
        // Original: public TIncisp() { super.setText("INCISP"); } public TIncisp(int line, int pos) { super.setText("INCISP"); ... }
        public TIncisp()
        {
            base.SetText("INCISP");
        }

        public TIncisp(int line, int pos)
        {
            base.SetText("INCISP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TIncisp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTIncisp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TIncisp text.");
        }
    }
}




