// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TConst : Token
    {
        public TConst()
        {
            base.SetText("CONST");
        }

        public TConst(int line, int pos)
        {
            base.SetText("CONST");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TConst(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTConst(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TConst text.");
        }
    }
}




