// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TBoolandii : Token
    {
        public TBoolandii()
        {
            base.SetText("BOOLANDII");
        }

        public TBoolandii(int line, int pos)
        {
            base.SetText("BOOLANDII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TBoolandii(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTBoolandii(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TBoolandii text.");
        }
    }
}




