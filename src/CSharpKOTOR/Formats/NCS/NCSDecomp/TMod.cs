// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TMod : Token
    {
        public TMod()
        {
            base.SetText("MOD");
        }

        public TMod(int line, int pos)
        {
            base.SetText("MOD");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TMod(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTMod(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TMod text.");
        }
    }
}




