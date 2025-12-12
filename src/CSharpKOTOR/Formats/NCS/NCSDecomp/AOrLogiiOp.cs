// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AOrLogiiOp : PLogiiOp
    {
        private TLogorii _logorii_;
        public AOrLogiiOp()
        {
        }

        public AOrLogiiOp(TLogorii _logorii_)
        {
            this.SetLogorii(_logorii_);
        }

        public override object Clone()
        {
            return new AOrLogiiOp((TLogorii)this.CloneNode(this._logorii_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAOrLogiiOp(this);
        }

        public TLogorii GetLogorii()
        {
            return this._logorii_;
        }

        public void SetLogorii(TLogorii node)
        {
            if (this._logorii_ != null)
            {
                this._logorii_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._logorii_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._logorii_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._logorii_ == child)
            {
                this._logorii_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._logorii_ == oldChild)
            {
                this.SetLogorii((TLogorii)newChild);
            }
        }
    }
}




