// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ADivBinaryOp : PBinaryOp
    {
        private TDiv _div_;
        public ADivBinaryOp()
        {
        }

        public ADivBinaryOp(TDiv _div_)
        {
            this.SetDiv(_div_);
        }

        public override object Clone()
        {
            return new ADivBinaryOp((TDiv)this.CloneNode(this._div_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseADivBinaryOp(this);
        }

        public TDiv GetDiv()
        {
            return this._div_;
        }

        public void SetDiv(TDiv node)
        {
            if (this._div_ != null)
            {
                this._div_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._div_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._div_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._div_ == child)
            {
                this._div_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._div_ == oldChild)
            {
                this.SetDiv((TDiv)newChild);
            }
        }
    }
}




