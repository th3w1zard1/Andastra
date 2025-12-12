// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ABpCmd : PCmd
    {
        private PBpCommand _bpCommand_;
        public ABpCmd()
        {
        }

        public ABpCmd(PBpCommand _bpCommand_)
        {
            this.SetBpCommand(_bpCommand_);
        }

        public override object Clone()
        {
            return new ABpCmd((PBpCommand)this.CloneNode(this._bpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseABpCmd(this);
        }

        public PBpCommand GetBpCommand()
        {
            return this._bpCommand_;
        }

        public void SetBpCommand(PBpCommand node)
        {
            if (this._bpCommand_ != null)
            {
                this._bpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._bpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._bpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._bpCommand_ == child)
            {
                this._bpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._bpCommand_ == oldChild)
            {
                this.SetBpCommand((PBpCommand)newChild);
            }
        }
    }
}




