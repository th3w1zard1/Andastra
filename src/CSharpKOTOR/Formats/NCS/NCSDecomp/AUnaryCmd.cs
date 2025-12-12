// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AUnaryCmd : PCmd
    {
        private PUnaryCommand _unaryCommand_;
        public AUnaryCmd()
        {
        }

        public AUnaryCmd(PUnaryCommand _unaryCommand_)
        {
            this.SetUnaryCommand(_unaryCommand_);
        }

        public override object Clone()
        {
            return new AUnaryCmd((PUnaryCommand)this.CloneNode(this._unaryCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAUnaryCmd(this);
        }

        public PUnaryCommand GetUnaryCommand()
        {
            return this._unaryCommand_;
        }

        public void SetUnaryCommand(PUnaryCommand node)
        {
            if (this._unaryCommand_ != null)
            {
                this._unaryCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._unaryCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._unaryCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._unaryCommand_ == child)
            {
                this._unaryCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._unaryCommand_ == oldChild)
            {
                this.SetUnaryCommand((PUnaryCommand)newChild);
            }
        }
    }
}




