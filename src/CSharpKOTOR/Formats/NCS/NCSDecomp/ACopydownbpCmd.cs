// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACopydownbpCmd : PCmd
    {
        private PCopyDownBpCommand _copyDownBpCommand_;
        public ACopydownbpCmd()
        {
        }

        public ACopydownbpCmd(PCopyDownBpCommand _copyDownBpCommand_)
        {
            this.SetCopyDownBpCommand(_copyDownBpCommand_);
        }

        public override object Clone()
        {
            return new ACopydownbpCmd((PCopyDownBpCommand)this.CloneNode(this._copyDownBpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseACopydownbpCmd(this);
        }

        public PCopyDownBpCommand GetCopyDownBpCommand()
        {
            return this._copyDownBpCommand_;
        }

        public void SetCopyDownBpCommand(PCopyDownBpCommand node)
        {
            if (this._copyDownBpCommand_ != null)
            {
                this._copyDownBpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._copyDownBpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._copyDownBpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._copyDownBpCommand_ == child)
            {
                this._copyDownBpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._copyDownBpCommand_ == oldChild)
            {
                this.SetCopyDownBpCommand((PCopyDownBpCommand)newChild);
            }
        }
    }
}




