// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TJmp : Token
    {
        public TJmp()
        {
            base.SetText("JMP");
        }

        public TJmp(int line, int pos)
        {
            base.SetText("JMP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TJmp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTJmp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TJmp text.");
        }
    }
}




