// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACopydownspCmd : PCmd
    {
        private PCopyDownSpCommand _copyDownSpCommand_;
        public ACopydownspCmd()
        {
        }

        public ACopydownspCmd(PCopyDownSpCommand _copyDownSpCommand_)
        {
            this.SetCopyDownSpCommand(_copyDownSpCommand_);
        }

        public override object Clone()
        {
            return new ACopydownspCmd((PCopyDownSpCommand)this.CloneNode(this._copyDownSpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseACopydownspCmd(this);
        }

        public PCopyDownSpCommand GetCopyDownSpCommand()
        {
            return this._copyDownSpCommand_;
        }

        public void SetCopyDownSpCommand(PCopyDownSpCommand node)
        {
            if (this._copyDownSpCommand_ != null)
            {
                this._copyDownSpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._copyDownSpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._copyDownSpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._copyDownSpCommand_ == child)
            {
                this._copyDownSpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._copyDownSpCommand_ == oldChild)
            {
                this.SetCopyDownSpCommand((PCopyDownSpCommand)newChild);
            }
        }
    }
}




