// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ARsaddCommand : PRsaddCommand
    {
        private TRsadd _rsadd_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _type_;
        private TSemi _semi_;
        public ARsaddCommand()
        {
        }

        public ARsaddCommand(TRsadd _rsadd_, TIntegerConstant _pos_, TIntegerConstant _type_, TSemi _semi_)
        {
            this.SetRsadd(_rsadd_);
            this.SetPos(_pos_);
            this.SetType(_type_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new ARsaddCommand((TRsadd)this.CloneNode(this._rsadd_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._type_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseARsaddCommand(this);
        }

        public TRsadd GetRsadd()
        {
            return this._rsadd_;
        }

        public void SetRsadd(TRsadd node)
        {
            if (this._rsadd_ != null)
            {
                this._rsadd_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._rsadd_ = node;
        }

        public TIntegerConstant GetPos()
        {
            return this._pos_;
        }

        public void SetPos(TIntegerConstant node)
        {
            if (this._pos_ != null)
            {
                this._pos_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._pos_ = node;
        }

        public new TIntegerConstant GetType()
        {
            return this._type_;
        }

        public void SetType(TIntegerConstant node)
        {
            if (this._type_ != null)
            {
                this._type_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._type_ = node;
        }

        public TSemi GetSemi()
        {
            return this._semi_;
        }

        public void SetSemi(TSemi node)
        {
            if (this._semi_ != null)
            {
                this._semi_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._semi_ = node;
        }

        public override string ToString()
        {
            return this.ToString(this._rsadd_) + this.ToString(this._pos_) + this.ToString(this._type_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._rsadd_ == child)
            {
                this._rsadd_ = null;
                return;
            }

            if (this._pos_ == child)
            {
                this._pos_ = null;
                return;
            }

            if (this._type_ == child)
            {
                this._type_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._rsadd_ == oldChild)
            {
                this.SetRsadd((TRsadd)newChild);
                return;
            }

            if (this._pos_ == oldChild)
            {
                this.SetPos((TIntegerConstant)newChild);
                return;
            }

            if (this._type_ == oldChild)
            {
                this.SetType((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




