//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TRetn : Token
    {
        public TRetn()
        {
            base.SetText("RETN");
        }

        public TRetn(int line, int pos)
        {
            base.SetText("RETN");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TRetn(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTRetn(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TRetn text.");
        }
    }
}




