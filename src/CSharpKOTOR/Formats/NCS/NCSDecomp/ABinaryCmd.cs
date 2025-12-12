// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ABinaryCmd : PCmd
    {
        private PBinaryCommand _binaryCommand_;
        public ABinaryCmd()
        {
        }

        public ABinaryCmd(PBinaryCommand _binaryCommand_)
        {
            this.SetBinaryCommand(_binaryCommand_);
        }

        public override object Clone()
        {
            return new ABinaryCmd((PBinaryCommand)this.CloneNode(this._binaryCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseABinaryCmd(this);
        }

        public PBinaryCommand GetBinaryCommand()
        {
            return this._binaryCommand_;
        }

        public void SetBinaryCommand(PBinaryCommand node)
        {
            if (this._binaryCommand_ != null)
            {
                this._binaryCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._binaryCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._binaryCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._binaryCommand_ == child)
            {
                this._binaryCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._binaryCommand_ == oldChild)
            {
                this.SetBinaryCommand((PBinaryCommand)newChild);
            }
        }
    }
}




