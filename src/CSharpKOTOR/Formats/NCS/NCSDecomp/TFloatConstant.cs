// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TFloatConstant : Token
    {
        public TFloatConstant(string text)
        {
            this.SetText(text);
        }

        public TFloatConstant(string text, int line, int pos)
        {
            this.SetText(text);
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TFloatConstant(this.GetText(), this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTFloatConstant(this);
        }
    }
}




