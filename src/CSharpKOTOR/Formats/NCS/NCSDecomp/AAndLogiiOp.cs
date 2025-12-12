// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AAndLogiiOp : PLogiiOp
    {
        private TLogandii _logandii_;
        public AAndLogiiOp()
        {
        }

        public AAndLogiiOp(TLogandii _logandii_)
        {
            this.SetLogandii(_logandii_);
        }

        public override object Clone()
        {
            return new AAndLogiiOp((TLogandii)this.CloneNode(this._logandii_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAAndLogiiOp(this);
        }

        public TLogandii GetLogandii()
        {
            return this._logandii_;
        }

        public void SetLogandii(TLogandii node)
        {
            if (this._logandii_ != null)
            {
                this._logandii_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._logandii_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._logandii_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._logandii_ == child)
            {
                this._logandii_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._logandii_ == oldChild)
            {
                this.SetLogandii((TLogandii)newChild);
            }
        }
    }
}




