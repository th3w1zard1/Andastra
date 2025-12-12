// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ANotUnaryOp : PUnaryOp
    {
        private TNot _not_;
        public ANotUnaryOp()
        {
        }

        public ANotUnaryOp(TNot _not_)
        {
            this.SetNot(_not_);
        }

        public override object Clone()
        {
            return new ANotUnaryOp((TNot)this.CloneNode(this._not_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseANotUnaryOp(this);
        }

        public TNot GetNot()
        {
            return this._not_;
        }

        public void SetNot(TNot node)
        {
            if (this._not_ != null)
            {
                this._not_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._not_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._not_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._not_ == child)
            {
                this._not_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._not_ == oldChild)
            {
                this.SetNot((TNot)newChild);
            }
        }
    }
}




