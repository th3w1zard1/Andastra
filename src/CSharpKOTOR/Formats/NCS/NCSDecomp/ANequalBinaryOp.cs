// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ANequalBinaryOp : PBinaryOp
    {
        private TNequal _nequal_;
        public ANequalBinaryOp()
        {
        }

        public ANequalBinaryOp(TNequal _nequal_)
        {
            this.SetNequal(_nequal_);
        }

        public override object Clone()
        {
            return new ANequalBinaryOp((TNequal)this.CloneNode(this._nequal_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseANequalBinaryOp(this);
        }

        public TNequal GetNequal()
        {
            return this._nequal_;
        }

        public void SetNequal(TNequal node)
        {
            if (this._nequal_ != null)
            {
                this._nequal_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._nequal_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._nequal_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._nequal_ == child)
            {
                this._nequal_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._nequal_ == oldChild)
            {
                this.SetNequal((TNequal)newChild);
            }
        }
    }
}




