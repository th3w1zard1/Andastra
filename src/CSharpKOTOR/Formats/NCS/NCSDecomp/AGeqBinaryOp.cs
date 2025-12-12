// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AGeqBinaryOp : PBinaryOp
    {
        private TGeq _geq_;
        public AGeqBinaryOp()
        {
        }

        public AGeqBinaryOp(TGeq _geq_)
        {
            this.SetGeq(_geq_);
        }

        public override object Clone()
        {
            return new AGeqBinaryOp((TGeq)this.CloneNode(this._geq_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAGeqBinaryOp(this);
        }

        public TGeq GetGeq()
        {
            return this._geq_;
        }

        public void SetGeq(TGeq node)
        {
            if (this._geq_ != null)
            {
                this._geq_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._geq_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._geq_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._geq_ == child)
            {
                this._geq_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._geq_ == oldChild)
            {
                this.SetGeq((TGeq)newChild);
            }
        }
    }
}




