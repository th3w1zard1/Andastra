//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TRsadd : Token
    {
        public TRsadd()
        {
            base.SetText("RSADD");
        }

        public TRsadd(int line, int pos)
        {
            base.SetText("RSADD");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TRsadd(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTRsadd(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TRsadd text.");
        }
    }
}




