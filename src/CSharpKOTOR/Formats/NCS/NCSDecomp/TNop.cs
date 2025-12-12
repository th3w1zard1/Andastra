// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TNop : Token
    {
        public TNop()
        {
            base.SetText("NOP");
        }

        public TNop(int line, int pos)
        {
            base.SetText("NOP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TNop(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTNop(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TNop text.");
        }
    }
}




