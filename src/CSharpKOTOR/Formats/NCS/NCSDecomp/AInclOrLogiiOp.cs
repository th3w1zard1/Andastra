// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AInclOrLogiiOp : PLogiiOp
    {
        private TIncorii _incorii_;
        public AInclOrLogiiOp()
        {
        }

        public AInclOrLogiiOp(TIncorii _incorii_)
        {
            this.SetIncorii(_incorii_);
        }

        public override object Clone()
        {
            return new AInclOrLogiiOp((TIncorii)this.CloneNode(this._incorii_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAInclOrLogiiOp(this);
        }

        public TIncorii GetIncorii()
        {
            return this._incorii_;
        }

        public void SetIncorii(TIncorii node)
        {
            if (this._incorii_ != null)
            {
                this._incorii_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._incorii_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._incorii_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._incorii_ == child)
            {
                this._incorii_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._incorii_ == oldChild)
            {
                this.SetIncorii((TIncorii)newChild);
            }
        }
    }
}




