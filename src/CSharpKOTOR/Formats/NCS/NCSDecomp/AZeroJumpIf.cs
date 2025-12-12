// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AZeroJumpIf : PJumpIf
    {
        private TJz _jz_;
        public AZeroJumpIf()
        {
        }

        public AZeroJumpIf(TJz _jz_)
        {
            this.SetJz(_jz_);
        }

        public override object Clone()
        {
            return new AZeroJumpIf((TJz)this.CloneNode(this._jz_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAZeroJumpIf(this);
        }

        public TJz GetJz()
        {
            return this._jz_;
        }

        public void SetJz(TJz node)
        {
            if (this._jz_ != null)
            {
                this._jz_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._jz_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._jz_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._jz_ == child)
            {
                this._jz_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._jz_ == oldChild)
            {
                this.SetJz((TJz)newChild);
            }
        }
    }
}




