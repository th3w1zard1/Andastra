// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/TBlank.java:9-28
// Original: public final class TBlank extends Token { ... public void apply(Switch sw) { ((Analysis)sw).caseTBlank(this); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Analysis;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp
{
    public sealed class TBlank : Token
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/node/TBlank.java:10-18
        // Original: public TBlank(String text) { this.setText(text); } public TBlank(String text, int line, int pos) { ... }
        public TBlank(string text)
        {
            this.SetText(text);
        }

        public TBlank(string text, int line, int pos)
        {
            this.SetText(text);
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TBlank(this.GetText(), this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTBlank(this);
        }
    }
}




