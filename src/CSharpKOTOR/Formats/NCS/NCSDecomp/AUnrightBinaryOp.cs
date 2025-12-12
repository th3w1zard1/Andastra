// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AUnrightBinaryOp : PBinaryOp
    {
        private TUnright _unright_;
        public AUnrightBinaryOp()
        {
        }

        public AUnrightBinaryOp(TUnright _unright_)
        {
            this.SetUnright(_unright_);
        }

        public override object Clone()
        {
            return new AUnrightBinaryOp((TUnright)this.CloneNode(this._unright_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAUnrightBinaryOp(this);
        }

        public TUnright GetUnright()
        {
            return this._unright_;
        }

        public void SetUnright(TUnright node)
        {
            if (this._unright_ != null)
            {
                this._unright_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._unright_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._unright_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._unright_ == child)
            {
                this._unright_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._unright_ == oldChild)
            {
                this.SetUnright((TUnright)newChild);
            }
        }
    }
}




