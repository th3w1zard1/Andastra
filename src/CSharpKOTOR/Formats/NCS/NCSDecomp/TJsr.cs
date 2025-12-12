// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TJsr : Token
    {
        public TJsr()
        {
            base.SetText("JSR");
        }

        public TJsr(int line, int pos)
        {
            base.SetText("JSR");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TJsr(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTJsr(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TJsr text.");
        }
    }
}




