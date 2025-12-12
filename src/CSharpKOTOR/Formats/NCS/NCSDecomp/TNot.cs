// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TNot : Token
    {
        public TNot()
        {
            base.SetText("NOT");
        }

        public TNot(int line, int pos)
        {
            base.SetText("NOT");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TNot(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTNot(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TNot text.");
        }
    }
}




