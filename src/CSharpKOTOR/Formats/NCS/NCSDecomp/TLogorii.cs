// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TLogorii : Token
    {
        public TLogorii()
        {
            base.SetText("LOGORII");
        }

        public TLogorii(int line, int pos)
        {
            base.SetText("LOGORII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TLogorii(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTLogorii(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TLogorii text.");
        }
    }
}




