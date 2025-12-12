// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AIntConstant : PConstant
    {
        private TIntegerConstant _integerConstant_;
        public AIntConstant()
        {
        }

        public AIntConstant(TIntegerConstant _integerConstant_)
        {
            this.SetIntegerConstant(_integerConstant_);
        }

        public override object Clone()
        {
            return new AIntConstant((TIntegerConstant)this.CloneNode(this._integerConstant_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAIntConstant(this);
        }

        public TIntegerConstant GetIntegerConstant()
        {
            return this._integerConstant_;
        }

        public void SetIntegerConstant(TIntegerConstant node)
        {
            if (this._integerConstant_ != null)
            {
                this._integerConstant_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._integerConstant_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._integerConstant_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._integerConstant_ == child)
            {
                this._integerConstant_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._integerConstant_ == oldChild)
            {
                this.SetIntegerConstant((TIntegerConstant)newChild);
            }
        }
    }
}




