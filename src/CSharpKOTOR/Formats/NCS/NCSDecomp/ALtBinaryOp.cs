// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ALtBinaryOp : PBinaryOp
    {
        private TLt _lt_;
        public ALtBinaryOp()
        {
        }

        public ALtBinaryOp(TLt _lt_)
        {
            this.SetLt(_lt_);
        }

        public override object Clone()
        {
            return new ALtBinaryOp((TLt)this.CloneNode(this._lt_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseALtBinaryOp(this);
        }

        public TLt GetLt()
        {
            return this._lt_;
        }

        public void SetLt(TLt node)
        {
            if (this._lt_ != null)
            {
                this._lt_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._lt_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._lt_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._lt_ == child)
            {
                this._lt_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._lt_ == oldChild)
            {
                this.SetLt((TLt)newChild);
            }
        }
    }
}




