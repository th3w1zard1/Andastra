// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACopytopbpCmd : PCmd
    {
        private PCopyTopBpCommand _copyTopBpCommand_;
        public ACopytopbpCmd()
        {
        }

        public ACopytopbpCmd(PCopyTopBpCommand _copyTopBpCommand_)
        {
            this.SetCopyTopBpCommand(_copyTopBpCommand_);
        }

        public override object Clone()
        {
            return new ACopytopbpCmd((PCopyTopBpCommand)this.CloneNode(this._copyTopBpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseACopytopbpCmd(this);
        }

        public PCopyTopBpCommand GetCopyTopBpCommand()
        {
            return this._copyTopBpCommand_;
        }

        public void SetCopyTopBpCommand(PCopyTopBpCommand node)
        {
            if (this._copyTopBpCommand_ != null)
            {
                this._copyTopBpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._copyTopBpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._copyTopBpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._copyTopBpCommand_ == child)
            {
                this._copyTopBpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._copyTopBpCommand_ == oldChild)
            {
                this.SetCopyTopBpCommand((PCopyTopBpCommand)newChild);
            }
        }
    }
}




