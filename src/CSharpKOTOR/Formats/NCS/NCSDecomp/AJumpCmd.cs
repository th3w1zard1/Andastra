// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AJumpCmd : PCmd
    {
        private PJumpCommand _jumpCommand_;
        public AJumpCmd()
        {
        }

        public AJumpCmd(PJumpCommand _jumpCommand_)
        {
            this.SetJumpCommand(_jumpCommand_);
        }

        public override object Clone()
        {
            return new AJumpCmd((PJumpCommand)this.CloneNode(this._jumpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAJumpCmd(this);
        }

        public PJumpCommand GetJumpCommand()
        {
            return this._jumpCommand_;
        }

        public void SetJumpCommand(PJumpCommand node)
        {
            if (this._jumpCommand_ != null)
            {
                this._jumpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._jumpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._jumpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._jumpCommand_ == child)
            {
                this._jumpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._jumpCommand_ == oldChild)
            {
                this.SetJumpCommand((PJumpCommand)newChild);
            }
        }
    }
}




