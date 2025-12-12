// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ASavebpBpOp : PBpOp
    {
        private TSavebp _savebp_;
        public ASavebpBpOp()
        {
        }

        public ASavebpBpOp(TSavebp _savebp_)
        {
            this.SetSavebp(_savebp_);
        }

        public override object Clone()
        {
            return new ASavebpBpOp((TSavebp)this.CloneNode(this._savebp_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseASavebpBpOp(this);
        }

        public TSavebp GetSavebp()
        {
            return this._savebp_;
        }

        public void SetSavebp(TSavebp node)
        {
            if (this._savebp_ != null)
            {
                this._savebp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._savebp_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._savebp_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._savebp_ == child)
            {
                this._savebp_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._savebp_ == oldChild)
            {
                this.SetSavebp((TSavebp)newChild);
            }
        }
    }
}




