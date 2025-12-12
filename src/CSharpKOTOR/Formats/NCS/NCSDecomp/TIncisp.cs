// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TIncisp : Token
    {
        public TIncisp()
        {
            base.SetText("INCISP");
        }

        public TIncisp(int line, int pos)
        {
            base.SetText("INCISP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TIncisp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTIncisp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TIncisp text.");
        }
    }
}




