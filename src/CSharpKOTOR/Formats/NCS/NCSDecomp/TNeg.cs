// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TNeg : Token
    {
        public TNeg()
        {
            base.SetText("NEG");
        }

        public TNeg(int line, int pos)
        {
            base.SetText("NEG");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TNeg(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTNeg(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TNeg text.");
        }
    }
}




