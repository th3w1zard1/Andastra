// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class EOF : Token
    {
        public EOF()
        {
            this.SetText("");
        }

        public EOF(int line, int pos)
        {
            this.SetText("");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new EOF(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseEOF(this);
        }
    }
}




