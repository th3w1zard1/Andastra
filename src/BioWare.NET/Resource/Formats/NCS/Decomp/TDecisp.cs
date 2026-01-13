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
    public sealed class TDecisp : Token
    {
        public TDecisp()
        {
            base.SetText("DECISP");
        }

        public TDecisp(int line, int pos)
        {
            base.SetText("DECISP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TDecisp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTDecisp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TDecisp text.");
        }
    }
}




