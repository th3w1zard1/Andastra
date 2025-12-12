// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ARestorebpBpOp : PBpOp
    {
        private TRestorebp _restorebp_;
        public ARestorebpBpOp()
        {
        }

        public ARestorebpBpOp(TRestorebp _restorebp_)
        {
            this.SetRestorebp(_restorebp_);
        }

        public override object Clone()
        {
            return new ARestorebpBpOp((TRestorebp)this.CloneNode(this._restorebp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseARestorebpBpOp(this);
        }

        public TRestorebp GetRestorebp()
        {
            return this._restorebp_;
        }

        public void SetRestorebp(TRestorebp node)
        {
            if (this._restorebp_ != null)
            {
                this._restorebp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._restorebp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._restorebp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._restorebp_ == child)
            {
                this._restorebp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._restorebp_ == oldChild)
            {
                this.SetRestorebp((TRestorebp)newChild);
            }
        }
    }
}




