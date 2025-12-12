// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AFloatConstant : PConstant
    {
        private TFloatConstant _floatConstant_;
        public AFloatConstant()
        {
        }

        public AFloatConstant(TFloatConstant _floatConstant_)
        {
            this.SetFloatConstant(_floatConstant_);
        }

        public override object Clone()
        {
            return new AFloatConstant((TFloatConstant)this.CloneNode(this._floatConstant_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAFloatConstant(this);
        }

        public TFloatConstant GetFloatConstant()
        {
            return this._floatConstant_;
        }

        public void SetFloatConstant(TFloatConstant node)
        {
            if (this._floatConstant_ != null)
            {
                this._floatConstant_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._floatConstant_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._floatConstant_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._floatConstant_ == child)
            {
                this._floatConstant_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._floatConstant_ == oldChild)
            {
                this.SetFloatConstant((TFloatConstant)newChild);
            }
        }
    }
}




