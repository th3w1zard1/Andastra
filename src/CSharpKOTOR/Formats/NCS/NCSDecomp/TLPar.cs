// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TLPar : Token
    {
        public TLPar()
        {
            base.SetText("(");
        }

        public TLPar(int line, int pos)
        {
            base.SetText("(");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TLPar(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTLPar(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TLPar text.");
        }
    }
}




