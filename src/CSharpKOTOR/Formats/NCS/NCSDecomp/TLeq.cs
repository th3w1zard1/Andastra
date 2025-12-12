// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TLeq : Token
    {
        public TLeq()
        {
            base.SetText("LEQ");
        }

        public TLeq(int line, int pos)
        {
            base.SetText("LEQ");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TLeq(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTLeq(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TLeq text.");
        }
    }
}




