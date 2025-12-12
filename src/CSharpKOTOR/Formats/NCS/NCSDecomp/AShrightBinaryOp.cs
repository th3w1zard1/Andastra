// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AShrightBinaryOp : PBinaryOp
    {
        private TShright _shright_;
        public AShrightBinaryOp()
        {
        }

        public AShrightBinaryOp(TShright _shright_)
        {
            this.SetShright(_shright_);
        }

        public override object Clone()
        {
            return new AShrightBinaryOp((TShright)this.CloneNode(this._shright_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAShrightBinaryOp(this);
        }

        public TShright GetShright()
        {
            return this._shright_;
        }

        public void SetShright(TShright node)
        {
            if (this._shright_ != null)
            {
                this._shright_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._shright_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._shright_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._shright_ == child)
            {
                this._shright_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._shright_ == oldChild)
            {
                this.SetShright((TShright)newChild);
            }
        }
    }
}




