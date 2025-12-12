// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TDot : Token
    {
        public TDot()
        {
            base.SetText(".");
        }

        public TDot(int line, int pos)
        {
            base.SetText(".");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TDot(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTDot(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TDot text.");
        }
    }
}




