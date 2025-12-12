// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TLt : Token
    {
        public TLt()
        {
            base.SetText("LT");
        }

        public TLt(int line, int pos)
        {
            base.SetText("LT");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TLt(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTLt(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TLt text.");
        }
    }
}




