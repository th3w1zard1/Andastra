// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ANegUnaryOp : PUnaryOp
    {
        private TNeg _neg_;
        public ANegUnaryOp()
        {
        }

        public ANegUnaryOp(TNeg _neg_)
        {
            this.SetNeg(_neg_);
        }

        public override object Clone()
        {
            return new ANegUnaryOp((TNeg)this.CloneNode(this._neg_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseANegUnaryOp(this);
        }

        public TNeg GetNeg()
        {
            return this._neg_;
        }

        public void SetNeg(TNeg node)
        {
            if (this._neg_ != null)
            {
                this._neg_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._neg_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._neg_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._neg_ == child)
            {
                this._neg_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._neg_ == oldChild)
            {
                this.SetNeg((TNeg)newChild);
            }
        }
    }
}




