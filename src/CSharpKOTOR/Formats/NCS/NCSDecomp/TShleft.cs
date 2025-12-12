// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TShleft : Token
    {
        public TShleft()
        {
            base.SetText("SHLEFTII");
        }

        public TShleft(int line, int pos)
        {
            base.SetText("SHLEFTII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TShleft(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTShleft(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TShleft text.");
        }
    }
}




