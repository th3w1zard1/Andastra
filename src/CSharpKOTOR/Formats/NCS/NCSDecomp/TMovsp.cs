// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TMovsp : Token
    {
        public TMovsp()
        {
            base.SetText("MOVSP");
        }

        public TMovsp(int line, int pos)
        {
            base.SetText("MOVSP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TMovsp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTMovsp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TMovsp text.");
        }
    }
}




