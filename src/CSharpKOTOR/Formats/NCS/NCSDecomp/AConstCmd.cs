// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AConstCmd : PCmd
    {
        private PConstCommand _constCommand_;
        public AConstCmd()
        {
        }

        public AConstCmd(PConstCommand _constCommand_)
        {
            this.SetConstCommand(_constCommand_);
        }

        public override object Clone()
        {
            return new AConstCmd((PConstCommand)this.CloneNode(this._constCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAConstCmd(this);
        }

        public PConstCommand GetConstCommand()
        {
            return this._constCommand_;
        }

        public void SetConstCommand(PConstCommand node)
        {
            if (this._constCommand_ != null)
            {
                this._constCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._constCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._constCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._constCommand_ == child)
            {
                this._constCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._constCommand_ == oldChild)
            {
                this.SetConstCommand((PConstCommand)newChild);
            }
        }
    }
}




