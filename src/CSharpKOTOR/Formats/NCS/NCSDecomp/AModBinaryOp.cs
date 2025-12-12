// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AModBinaryOp : PBinaryOp
    {
        private TMod _mod_;
        public AModBinaryOp()
        {
        }

        public AModBinaryOp(TMod _mod_)
        {
            this.SetMod(_mod_);
        }

        public override object Clone()
        {
            return new AModBinaryOp((TMod)this.CloneNode(this._mod_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAModBinaryOp(this);
        }

        public TMod GetMod()
        {
            return this._mod_;
        }

        public void SetMod(TMod node)
        {
            if (this._mod_ != null)
            {
                this._mod_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._mod_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._mod_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._mod_ == child)
            {
                this._mod_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._mod_ == oldChild)
            {
                this.SetMod((TMod)newChild);
            }
        }
    }
}




