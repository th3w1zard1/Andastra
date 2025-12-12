// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TBlank : Token
    {
        public TBlank(string text)
        {
            this.SetText(text);
        }

        public TBlank(string text, int line, int pos)
        {
            this.SetText(text);
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TBlank(this.GetText(), this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTBlank(this);
        }
    }
}




