// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TRestorebp : Token
    {
        public TRestorebp()
        {
            base.SetText("RESTOREBP");
        }

        public TRestorebp(int line, int pos)
        {
            base.SetText("RESTOREBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TRestorebp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTRestorebp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TRestorebp text.");
        }
    }
}




