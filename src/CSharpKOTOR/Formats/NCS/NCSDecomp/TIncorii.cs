// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TIncorii : Token
    {
        public TIncorii()
        {
            base.SetText("INCORII");
        }

        public TIncorii(int line, int pos)
        {
            base.SetText("INCORII");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TIncorii(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTIncorii(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TIncorii text.");
        }
    }
}




