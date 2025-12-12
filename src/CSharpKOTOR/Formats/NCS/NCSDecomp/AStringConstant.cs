// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AStringConstant : PConstant
    {
        private TStringLiteral _stringLiteral_;
        public AStringConstant()
        {
        }

        public AStringConstant(TStringLiteral _stringLiteral_)
        {
            this.SetStringLiteral(_stringLiteral_);
        }

        public override object Clone()
        {
            return new AStringConstant((TStringLiteral)this.CloneNode(this._stringLiteral_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAStringConstant(this);
        }

        public TStringLiteral GetStringLiteral()
        {
            return this._stringLiteral_;
        }

        public void SetStringLiteral(TStringLiteral node)
        {
            if (this._stringLiteral_ != null)
            {
                this._stringLiteral_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._stringLiteral_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._stringLiteral_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._stringLiteral_ == child)
            {
                this._stringLiteral_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._stringLiteral_ == oldChild)
            {
                this.SetStringLiteral((TStringLiteral)newChild);
            }
        }
    }
}




