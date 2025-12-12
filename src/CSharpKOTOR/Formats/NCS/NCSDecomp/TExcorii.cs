// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TExcorii : Token
    {
        public TExcorii()
        {
            base.SetText("EXCORII");
        }

        public TExcorii(int line, int pos)
        {
            base.SetText("EXCORII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TExcorii(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTExcorii(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TExcorii text.");
        }
    }
}




