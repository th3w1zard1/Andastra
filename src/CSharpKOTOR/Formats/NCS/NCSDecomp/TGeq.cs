// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TGeq : Token
    {
        public TGeq()
        {
            base.SetText("GEQ");
        }

        public TGeq(int line, int pos)
        {
            base.SetText("GEQ");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TGeq(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTGeq(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TGeq text.");
        }
    }
}




