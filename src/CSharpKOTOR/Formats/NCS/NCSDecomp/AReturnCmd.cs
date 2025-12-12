// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AReturnCmd : PCmd
    {
        private PReturn _return_;
        public AReturnCmd()
        {
        }

        public AReturnCmd(PReturn _return_)
        {
            this.SetReturn(_return_);
        }

        public override object Clone()
        {
            return new AReturnCmd((PReturn)this.CloneNode(this._return_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAReturnCmd(this);
        }

        public PReturn GetReturn()
        {
            return this._return_;
        }

        public void SetReturn(PReturn node)
        {
            if (this._return_ != null)
            {
                this._return_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._return_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._return_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._return_ == child)
            {
                this._return_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._return_ == oldChild)
            {
                this.SetReturn((PReturn)newChild);
            }
        }
    }
}




