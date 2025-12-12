//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ASubroutine : PSubroutine
    {
        private PCommandBlock _commandBlock_;
        private PReturn _return_;
        private int _id;
        public ASubroutine()
        {
            this._id = 0;
        }

        public ASubroutine(PCommandBlock _commandBlock_, PReturn _return_)
        {
            this.SetCommandBlock(_commandBlock_);
            this.SetReturn(_return_);
            this._id = 0;
        }

        public override object Clone()
        {
            return new ASubroutine((PCommandBlock)this.CloneNode(this._commandBlock_), (PReturn)this.CloneNode(this._return_));
        }
        public override void Apply(Switch sw)
        {
            JavaSystem.@out.Println($"DEBUG ASubroutine(root).Apply: sw type={sw.GetType().Name}, sw is IAnalysis={sw is IAnalysis}");
            if (sw is IAnalysis ia)
            {
                JavaSystem.@out.Println($"DEBUG ASubroutine(root).Apply: calling ia.CaseASubroutine");
                ia.CaseASubroutine(this);
            }
            else
            {
                JavaSystem.@out.Println($"DEBUG ASubroutine(root).Apply: sw is not IAnalysis, skipping");
            }
        }

        public int GetId()
        {
            return this._id;
        }

        public void SetId(int subId)
        {
            this._id = subId;
        }

        public PCommandBlock GetCommandBlock()
        {
            return this._commandBlock_;
        }

        public void SetCommandBlock(PCommandBlock node)
        {
            if (this._commandBlock_ != null)
            {
                this._commandBlock_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._commandBlock_ = node;
        }

        public PReturn GetReturn()
        {
            return this._return_;
        }

        public void SetReturn(PReturn node)
        {
            if (this._return_ != null)
            {
                this._return_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._return_ = node;
        }

        public override string ToString()
        {
            return this.ToString(this._commandBlock_) + this.ToString(this._return_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._commandBlock_ == child)
            {
                this._commandBlock_ = null;
                return;
            }

            if (this._return_ == child)
            {
                this._return_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._commandBlock_ == oldChild)
            {
                this.SetCommandBlock((PCommandBlock)newChild);
                return;
            }

            if (this._return_ == oldChild)
            {
                this.SetReturn((PReturn)newChild);
            }
        }
    }
}




