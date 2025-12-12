// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TCpdownsp : Token
    {
        public TCpdownsp()
        {
            base.SetText("CPDOWNSP");
        }

        public TCpdownsp(int line, int pos)
        {
            base.SetText("CPDOWNSP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TCpdownsp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTCpdownsp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TCpdownsp text.");
        }
    }
}




