// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AAddBinaryOp : PBinaryOp
    {
        private TAdd _add_;
        public AAddBinaryOp()
        {
        }

        public AAddBinaryOp(TAdd _add_)
        {
            this.SetAdd(_add_);
        }

        public override object Clone()
        {
            return new AAddBinaryOp((TAdd)this.CloneNode(this._add_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAAddBinaryOp(this);
        }

        public TAdd GetAdd()
        {
            return this._add_;
        }

        public void SetAdd(TAdd node)
        {
            if (this._add_ != null)
            {
                this._add_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._add_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._add_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._add_ == child)
            {
                this._add_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._add_ == oldChild)
            {
                this.SetAdd((TAdd)newChild);
            }
        }
    }
}




