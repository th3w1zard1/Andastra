//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TSavebp : Token
    {
        public TSavebp()
        {
            base.SetText("SAVEBP");
        }

        public TSavebp(int line, int pos)
        {
            base.SetText("SAVEBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TSavebp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTSavebp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TSavebp text.");
        }
    }
}




