// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class TDestruct : Token
    {
        public TDestruct()
        {
            base.SetText("DESTRUCT");
        }

        public TDestruct(int line, int pos)
        {
            base.SetText("DESTRUCT");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TDestruct(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTDestruct(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TDestruct text.");
        }
    }
}




