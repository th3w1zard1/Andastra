// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TJnz : Token
    {
        public TJnz()
        {
            base.SetText("JNZ");
        }

        public TJnz(int line, int pos)
        {
            base.SetText("JNZ");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TJnz(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTJnz(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TJnz text.");
        }
    }
}




