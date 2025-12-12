// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TJz : Token
    {
        public TJz()
        {
            base.SetText("JZ");
        }

        public TJz(int line, int pos)
        {
            base.SetText("JZ");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TJz(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTJz(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TJz text.");
        }
    }
}




