//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TUnright : Token
    {
        public TUnright()
        {
            base.SetText("USHRIGHTII");
        }

        public TUnright(int line, int pos)
        {
            base.SetText("USHRIGHTII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TUnright(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTUnright(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TUnright text.");
        }
    }
}




