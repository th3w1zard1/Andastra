// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AExclOrLogiiOp : PLogiiOp
    {
        private TExcorii _excorii_;
        public AExclOrLogiiOp()
        {
        }

        public AExclOrLogiiOp(TExcorii _excorii_)
        {
            this.SetExcorii(_excorii_);
        }

        public override object Clone()
        {
            return new AExclOrLogiiOp((TExcorii)this.CloneNode(this._excorii_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAExclOrLogiiOp(this);
        }

        public TExcorii GetExcorii()
        {
            return this._excorii_;
        }

        public void SetExcorii(TExcorii node)
        {
            if (this._excorii_ != null)
            {
                this._excorii_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._excorii_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._excorii_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._excorii_ == child)
            {
                this._excorii_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._excorii_ == oldChild)
            {
                this.SetExcorii((TExcorii)newChild);
            }
        }
    }
}




