// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AShleftBinaryOp : PBinaryOp
    {
        private TShleft _shleft_;
        public AShleftBinaryOp()
        {
        }

        public AShleftBinaryOp(TShleft _shleft_)
        {
            this.SetShleft(_shleft_);
        }

        public override object Clone()
        {
            return new AShleftBinaryOp((TShleft)this.CloneNode(this._shleft_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAShleftBinaryOp(this);
        }

        public TShleft GetShleft()
        {
            return this._shleft_;
        }

        public void SetShleft(TShleft node)
        {
            if (this._shleft_ != null)
            {
                this._shleft_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._shleft_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._shleft_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._shleft_ == child)
            {
                this._shleft_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._shleft_ == oldChild)
            {
                this.SetShleft((TShleft)newChild);
            }
        }
    }
}




