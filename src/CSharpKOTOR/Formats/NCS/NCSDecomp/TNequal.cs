// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TNequal : Token
    {
        public TNequal()
        {
            base.SetText("NEQUAL");
        }

        public TNequal(int line, int pos)
        {
            base.SetText("NEQUAL");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TNequal(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTNequal(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TNequal text.");
        }
    }
}




