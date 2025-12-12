// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AMulBinaryOp : PBinaryOp
    {
        private TMul _mul_;
        public AMulBinaryOp()
        {
        }

        public AMulBinaryOp(TMul _mul_)
        {
            this.SetMul(_mul_);
        }

        public override object Clone()
        {
            return new AMulBinaryOp((TMul)this.CloneNode(this._mul_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAMulBinaryOp(this);
        }

        public TMul GetMul()
        {
            return this._mul_;
        }

        public void SetMul(TMul node)
        {
            if (this._mul_ != null)
            {
                this._mul_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._mul_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._mul_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._mul_ == child)
            {
                this._mul_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._mul_ == oldChild)
            {
                this.SetMul((TMul)newChild);
            }
        }
    }
}




