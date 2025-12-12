// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACopytopspCmd : PCmd
    {
        private PCopyTopSpCommand _copyTopSpCommand_;
        public ACopytopspCmd()
        {
        }

        public ACopytopspCmd(PCopyTopSpCommand _copyTopSpCommand_)
        {
            this.SetCopyTopSpCommand(_copyTopSpCommand_);
        }

        public override object Clone()
        {
            return new ACopytopspCmd((PCopyTopSpCommand)this.CloneNode(this._copyTopSpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseACopytopspCmd(this);
        }

        public PCopyTopSpCommand GetCopyTopSpCommand()
        {
            return this._copyTopSpCommand_;
        }

        public void SetCopyTopSpCommand(PCopyTopSpCommand node)
        {
            if (this._copyTopSpCommand_ != null)
            {
                this._copyTopSpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._copyTopSpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._copyTopSpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._copyTopSpCommand_ == child)
            {
                this._copyTopSpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._copyTopSpCommand_ == oldChild)
            {
                this.SetCopyTopSpCommand((PCopyTopSpCommand)newChild);
            }
        }
    }
}




