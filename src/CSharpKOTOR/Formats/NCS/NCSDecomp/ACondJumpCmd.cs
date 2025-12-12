// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACondJumpCmd : PCmd
    {
        private PConditionalJumpCommand _conditionalJumpCommand_;
        public ACondJumpCmd()
        {
        }

        public ACondJumpCmd(PConditionalJumpCommand _conditionalJumpCommand_)
        {
            this.SetConditionalJumpCommand(_conditionalJumpCommand_);
        }

        public override object Clone()
        {
            return new ACondJumpCmd((PConditionalJumpCommand)this.CloneNode(this._conditionalJumpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseACondJumpCmd(this);
        }

        public PConditionalJumpCommand GetConditionalJumpCommand()
        {
            return this._conditionalJumpCommand_;
        }

        public void SetConditionalJumpCommand(PConditionalJumpCommand node)
        {
            if (this._conditionalJumpCommand_ != null)
            {
                this._conditionalJumpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._conditionalJumpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._conditionalJumpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._conditionalJumpCommand_ == child)
            {
                this._conditionalJumpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._conditionalJumpCommand_ == oldChild)
            {
                this.SetConditionalJumpCommand((PConditionalJumpCommand)newChild);
            }
        }
    }
}




