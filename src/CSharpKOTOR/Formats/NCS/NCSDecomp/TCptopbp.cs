// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TCptopbp : Token
    {
        public TCptopbp()
        {
            base.SetText("CPTOPBP");
        }

        public TCptopbp(int line, int pos)
        {
            base.SetText("CPTOPBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TCptopbp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTCptopbp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TCptopbp text.");
        }
    }
}




