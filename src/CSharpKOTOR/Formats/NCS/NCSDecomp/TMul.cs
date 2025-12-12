// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TMul : Token
    {
        public TMul()
        {
            base.SetText("MUL");
        }

        public TMul(int line, int pos)
        {
            base.SetText("MUL");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TMul(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTMul(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TMul text.");
        }
    }
}




