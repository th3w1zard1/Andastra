// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TAdd : Token
    {
        public TAdd()
        {
            base.SetText("ADD");
        }

        public TAdd(int line, int pos)
        {
            base.SetText("ADD");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TAdd(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTAdd(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TAdd text.");
        }
    }
}




