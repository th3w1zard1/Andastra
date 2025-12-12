// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ANonzeroJumpIf : PJumpIf
    {
        private TJnz _jnz_;
        public ANonzeroJumpIf()
        {
        }

        public ANonzeroJumpIf(TJnz _jnz_)
        {
            this.SetJnz(_jnz_);
        }

        public override object Clone()
        {
            return new ANonzeroJumpIf((TJnz)this.CloneNode(this._jnz_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseANonzeroJumpIf(this);
        }

        public TJnz GetJnz()
        {
            return this._jnz_;
        }

        public void SetJnz(TJnz node)
        {
            if (this._jnz_ != null)
            {
                this._jnz_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._jnz_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._jnz_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._jnz_ == child)
            {
                this._jnz_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._jnz_ == oldChild)
            {
                this.SetJnz((TJnz)newChild);
            }
        }
    }
}




