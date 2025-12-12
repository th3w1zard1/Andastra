//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TShright : Token
    {
        public TShright()
        {
            base.SetText("SHRIGHTII");
        }

        public TShright(int line, int pos)
        {
            base.SetText("SHRIGHTII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TShright(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTShright(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TShright text.");
        }
    }
}




