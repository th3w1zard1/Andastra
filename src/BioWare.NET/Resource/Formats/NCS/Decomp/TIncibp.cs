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
    public sealed class TIncibp : Token
    {
        public TIncibp()
        {
            base.SetText("INCIBP");
        }

        public TIncibp(int line, int pos)
        {
            base.SetText("INCIBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TIncibp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTIncibp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TIncibp text.");
        }
    }
}




