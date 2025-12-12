// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TEqual : Token
    {
        public TEqual()
        {
            base.SetText("EQUAL");
        }

        public TEqual(int line, int pos)
        {
            base.SetText("EQUAL");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TEqual(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTEqual(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TEqual text.");
        }
    }
}




