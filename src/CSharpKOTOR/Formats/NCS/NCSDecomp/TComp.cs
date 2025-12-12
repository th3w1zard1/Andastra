// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TComp : Token
    {
        public TComp()
        {
            base.SetText("COMP");
        }

        public TComp(int line, int pos)
        {
            base.SetText("COMP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TComp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTComp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TComp text.");
        }
    }
}




