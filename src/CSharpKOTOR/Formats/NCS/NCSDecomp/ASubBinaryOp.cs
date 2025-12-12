// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ASubBinaryOp : PBinaryOp
    {
        private TSub _sub_;
        public ASubBinaryOp()
        {
        }

        public ASubBinaryOp(TSub _sub_)
        {
            this.SetSub(_sub_);
        }

        public override object Clone()
        {
            return new ASubBinaryOp((TSub)this.CloneNode(this._sub_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseASubBinaryOp(this);
        }

        public TSub GetSub()
        {
            return this._sub_;
        }

        public void SetSub(TSub node)
        {
            if (this._sub_ != null)
            {
                this._sub_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._sub_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._sub_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._sub_ == child)
            {
                this._sub_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._sub_ == oldChild)
            {
                this.SetSub((TSub)newChild);
            }
        }
    }
}




