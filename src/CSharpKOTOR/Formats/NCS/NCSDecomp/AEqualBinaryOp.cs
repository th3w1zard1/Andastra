// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AEqualBinaryOp : PBinaryOp
    {
        private TEqual _equal_;
        public AEqualBinaryOp()
        {
        }

        public AEqualBinaryOp(TEqual _equal_)
        {
            this.SetEqual(_equal_);
        }

        public override object Clone()
        {
            return new AEqualBinaryOp((TEqual)this.CloneNode(this._equal_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAEqualBinaryOp(this);
        }

        public TEqual GetEqual()
        {
            return this._equal_;
        }

        public void SetEqual(TEqual node)
        {
            if (this._equal_ != null)
            {
                this._equal_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._equal_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._equal_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._equal_ == child)
            {
                this._equal_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._equal_ == oldChild)
            {
                this.SetEqual((TEqual)newChild);
            }
        }
    }
}




