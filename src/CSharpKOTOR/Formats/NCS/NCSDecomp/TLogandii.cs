// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TLogandii : Token
    {
        public TLogandii()
        {
            base.SetText("LOGANDII");
        }

        public TLogandii(int line, int pos)
        {
            base.SetText("LOGANDII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TLogandii(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTLogandii(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TLogandii text.");
        }
    }
}




