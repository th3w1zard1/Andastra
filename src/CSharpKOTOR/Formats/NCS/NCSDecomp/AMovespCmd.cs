// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AMovespCmd : PCmd
    {
        private PMoveSpCommand _moveSpCommand_;
        public AMovespCmd()
        {
        }

        public AMovespCmd(PMoveSpCommand _moveSpCommand_)
        {
            this.SetMoveSpCommand(_moveSpCommand_);
        }

        public override object Clone()
        {
            return new AMovespCmd((PMoveSpCommand)this.CloneNode(this._moveSpCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAMovespCmd(this);
        }

        public PMoveSpCommand GetMoveSpCommand()
        {
            return this._moveSpCommand_;
        }

        public void SetMoveSpCommand(PMoveSpCommand node)
        {
            if (this._moveSpCommand_ != null)
            {
                this._moveSpCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._moveSpCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._moveSpCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._moveSpCommand_ == child)
            {
                this._moveSpCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._moveSpCommand_ == oldChild)
            {
                this.SetMoveSpCommand((PMoveSpCommand)newChild);
            }
        }
    }
}




