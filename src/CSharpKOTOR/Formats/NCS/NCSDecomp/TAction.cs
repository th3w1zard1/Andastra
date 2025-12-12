// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TAction : Token
    {
        public TAction()
        {
            base.SetText("ACTION");
        }

        public TAction(int line, int pos)
        {
            base.SetText("ACTION");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TAction(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTAction(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TAction text.");
        }
    }
}




