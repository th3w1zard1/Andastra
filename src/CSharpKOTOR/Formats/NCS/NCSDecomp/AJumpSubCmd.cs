// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AJumpSubCmd : PCmd
    {
        private PJumpToSubroutine _jumpToSubroutine_;
        public AJumpSubCmd()
        {
        }

        public AJumpSubCmd(PJumpToSubroutine _jumpToSubroutine_)
        {
            this.SetJumpToSubroutine(_jumpToSubroutine_);
        }

        public override object Clone()
        {
            return new AJumpSubCmd((PJumpToSubroutine)this.CloneNode(this._jumpToSubroutine_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAJumpSubCmd(this);
        }

        public PJumpToSubroutine GetJumpToSubroutine()
        {
            return this._jumpToSubroutine_;
        }

        public void SetJumpToSubroutine(PJumpToSubroutine node)
        {
            if (this._jumpToSubroutine_ != null)
            {
                this._jumpToSubroutine_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._jumpToSubroutine_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._jumpToSubroutine_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._jumpToSubroutine_ == child)
            {
                this._jumpToSubroutine_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._jumpToSubroutine_ == oldChild)
            {
                this.SetJumpToSubroutine((PJumpToSubroutine)newChild);
            }
        }
    }
}




