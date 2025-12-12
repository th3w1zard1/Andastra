// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ALogiiCmd : PCmd
    {
        private PLogiiCommand _logiiCommand_;
        public ALogiiCmd()
        {
        }

        public ALogiiCmd(PLogiiCommand _logiiCommand_)
        {
            this.SetLogiiCommand(_logiiCommand_);
        }

        public override object Clone()
        {
            return new ALogiiCmd((PLogiiCommand)this.CloneNode(this._logiiCommand_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseALogiiCmd(this);
        }

        public PLogiiCommand GetLogiiCommand()
        {
            return this._logiiCommand_;
        }

        public void SetLogiiCommand(PLogiiCommand node)
        {
            if (this._logiiCommand_ != null)
            {
                this._logiiCommand_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._logiiCommand_ = node;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(this.ToString(this._logiiCommand_)).ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._logiiCommand_ == child)
            {
                this._logiiCommand_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._logiiCommand_ == oldChild)
            {
                this.SetLogiiCommand((PLogiiCommand)newChild);
            }
        }
    }
}




