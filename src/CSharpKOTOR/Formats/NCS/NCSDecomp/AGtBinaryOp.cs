// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AGtBinaryOp : PBinaryOp
    {
        private TGt _gt_;
        public AGtBinaryOp()
        {
        }

        public AGtBinaryOp(TGt _gt_)
        {
            this.SetGt(_gt_);
        }

        public override object Clone()
        {
            return new AGtBinaryOp((TGt)this.CloneNode(this._gt_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAGtBinaryOp(this);
        }

        public TGt GetGt()
        {
            return this._gt_;
        }

        public void SetGt(TGt node)
        {
            if (this._gt_ != null)
            {
                this._gt_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._gt_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._gt_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._gt_ == child)
            {
                this._gt_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._gt_ == oldChild)
            {
                this.SetGt((TGt)newChild);
            }
        }
    }
}




