// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Analysis;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp
{
    public sealed class TDecibp : Token
    {
        public TDecibp()
        {
            base.SetText("DECIBP");
        }

        public TDecibp(int line, int pos)
        {
            base.SetText("DECIBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TDecibp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTDecibp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TDecibp text.");
        }
    }
}




