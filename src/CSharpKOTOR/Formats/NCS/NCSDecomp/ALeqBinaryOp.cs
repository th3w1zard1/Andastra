// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ALeqBinaryOp : PBinaryOp
    {
        private TLeq _leq_;
        public ALeqBinaryOp()
        {
        }

        public ALeqBinaryOp(TLeq _leq_)
        {
            this.SetLeq(_leq_);
        }

        public override object Clone()
        {
            return new ALeqBinaryOp((TLeq)this.CloneNode(this._leq_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseALeqBinaryOp(this);
        }

        public TLeq GetLeq()
        {
            return this._leq_;
        }

        public void SetLeq(TLeq node)
        {
            if (this._leq_ != null)
            {
                this._leq_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._leq_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._leq_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._leq_ == child)
            {
                this._leq_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._leq_ == oldChild)
            {
                this.SetLeq((TLeq)newChild);
            }
        }
    }
}




