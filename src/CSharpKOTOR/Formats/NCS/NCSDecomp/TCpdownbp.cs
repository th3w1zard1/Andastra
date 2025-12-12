// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TCpdownbp : Token
    {
        public TCpdownbp()
        {
            base.SetText("CPDOWNBP");
        }

        public TCpdownbp(int line, int pos)
        {
            base.SetText("CPDOWNBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TCpdownbp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTCpdownbp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TCpdownbp text.");
        }
    }
}




