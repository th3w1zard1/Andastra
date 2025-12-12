//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class X2PSubroutine : XPSubroutine
    {
        private PSubroutine _pSubroutine_;
        public X2PSubroutine()
        {
        }

        public X2PSubroutine(PSubroutine _pSubroutine_)
        {
            this.SetPSubroutine(_pSubroutine_);
        }

        public override object Clone()
        {
            throw new Exception("Unsupported Operation");
        }
        public override void Apply(Switch sw)
        {
            throw new Exception("Switch not supported.");
        }

        public PSubroutine GetPSubroutine()
        {
            return this._pSubroutine_;
        }

        public void SetPSubroutine(PSubroutine node)
        {
            if (this._pSubroutine_ != null)
            {
                this._pSubroutine_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._pSubroutine_ = node;
        }

        public override void RemoveChild(Node child)
        {
            if (this._pSubroutine_ == child)
            {
                this._pSubroutine_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._pSubroutine_)).ToString();
        }
    }
}




