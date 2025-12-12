//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TSemi : Token
    {
        public TSemi()
        {
            base.SetText(";");
        }

        public TSemi(int line, int pos)
        {
            base.SetText(";");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TSemi(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTSemi(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TSemi text.");
        }
    }
}




