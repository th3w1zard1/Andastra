// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ADestructCmd : PCmd
    {
        private PDestructCommand _destructCommand_;
        public ADestructCmd()
        {
        }

        public ADestructCmd(PDestructCommand _destructCommand_)
        {
            this.SetDestructCommand(_destructCommand_);
        }

        public override object Clone()
        {
            return new ADestructCmd((PDestructCommand)this.CloneNode(this._destructCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseADestructCmd(this);
        }

        public PDestructCommand GetDestructCommand()
        {
            return this._destructCommand_;
        }

        public void SetDestructCommand(PDestructCommand node)
        {
            if (this._destructCommand_ != null)
            {
                this._destructCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._destructCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._destructCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._destructCommand_ == child)
            {
                this._destructCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._destructCommand_ == oldChild)
            {
                this.SetDestructCommand((PDestructCommand)newChild);
            }
        }
    }
}




