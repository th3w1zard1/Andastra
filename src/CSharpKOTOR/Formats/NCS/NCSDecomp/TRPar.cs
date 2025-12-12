//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TRPar : Token
    {
        public TRPar()
        {
            base.SetText(")");
        }

        public TRPar(int line, int pos)
        {
            base.SetText(")");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TRPar(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTRPar(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TRPar text.");
        }
    }
}




