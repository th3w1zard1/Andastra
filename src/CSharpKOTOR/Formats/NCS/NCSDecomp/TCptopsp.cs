// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TCptopsp : Token
    {
        public TCptopsp()
        {
            base.SetText("CPTOPSP");
        }

        public TCptopsp(int line, int pos)
        {
            base.SetText("CPTOPSP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TCptopsp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTCptopsp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TCptopsp text.");
        }
    }
}




