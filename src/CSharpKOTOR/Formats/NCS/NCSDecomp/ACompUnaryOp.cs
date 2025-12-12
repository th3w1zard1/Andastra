// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACompUnaryOp : PUnaryOp
    {
        private TComp _comp_;
        public ACompUnaryOp()
        {
        }

        public ACompUnaryOp(TComp _comp_)
        {
            this.SetComp(_comp_);
        }

        public override object Clone()
        {
            return new ACompUnaryOp((TComp)this.CloneNode(this._comp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseACompUnaryOp(this);
        }

        public TComp GetComp()
        {
            return this._comp_;
        }

        public void SetComp(TComp node)
        {
            if (this._comp_ != null)
            {
                this._comp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._comp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._comp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._comp_ == child)
            {
                this._comp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._comp_ == oldChild)
            {
                this.SetComp((TComp)newChild);
            }
        }
    }
}




