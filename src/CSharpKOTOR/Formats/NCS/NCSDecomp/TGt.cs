// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TGt : Token
    {
        public TGt()
        {
            base.SetText("GT");
        }

        public TGt(int line, int pos)
        {
            base.SetText("GT");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TGt(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTGt(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TGt text.");
        }
    }
}




