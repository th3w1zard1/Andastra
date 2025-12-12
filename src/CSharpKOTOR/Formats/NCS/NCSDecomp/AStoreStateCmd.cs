// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AStoreStateCmd : PCmd
    {
        private PStoreStateCommand _storeStateCommand_;
        public AStoreStateCmd()
        {
        }

        public AStoreStateCmd(PStoreStateCommand _storeStateCommand_)
        {
            this.SetStoreStateCommand(_storeStateCommand_);
        }

        public override object Clone()
        {
            return new AStoreStateCmd((PStoreStateCommand)this.CloneNode(this._storeStateCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAStoreStateCmd(this);
        }

        public PStoreStateCommand GetStoreStateCommand()
        {
            return this._storeStateCommand_;
        }

        public void SetStoreStateCommand(PStoreStateCommand node)
        {
            if (this._storeStateCommand_ != null)
            {
                this._storeStateCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._storeStateCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._storeStateCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._storeStateCommand_ == child)
            {
                this._storeStateCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._storeStateCommand_ == oldChild)
            {
                this.SetStoreStateCommand((PStoreStateCommand)newChild);
            }
        }
    }
}




