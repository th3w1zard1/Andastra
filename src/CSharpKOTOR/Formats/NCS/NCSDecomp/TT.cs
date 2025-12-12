//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TT : Token
    {
        public TT()
        {
            base.SetText("T");
        }

        public TT(int line, int pos)
        {
            base.SetText("T");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TT(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTT(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TT text.");
        }
    }
}




