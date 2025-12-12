//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TSub : Token
    {
        public TSub()
        {
            base.SetText("SUB");
        }

        public TSub(int line, int pos)
        {
            base.SetText("SUB");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TSub(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTSub(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TSub text.");
        }
    }
}




