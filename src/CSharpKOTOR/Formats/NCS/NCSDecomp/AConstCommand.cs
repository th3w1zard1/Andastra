// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AConstCommand : PConstCommand
    {
        private TConst _const_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _type_;
        private PConstant _constant_;
        private TSemi _semi_;
        public AConstCommand()
        {
        }

        public AConstCommand(TConst _const_, TIntegerConstant _pos_, TIntegerConstant _type_, PConstant _constant_, TSemi _semi_)
        {
            this.SetConst(_const_);
            this.SetPos(_pos_);
            this.SetType(_type_);
            this.SetConstant(_constant_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new AConstCommand((TConst)this.CloneNode(this._const_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._type_), (PConstant)this.CloneNode(this._constant_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAConstCommand(this);
        }

        public TConst GetConst()
        {
            return this._const_;
        }

        public void SetConst(TConst node)
        {
            if (this._const_ != null)
            {
                this._const_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._const_ = node;
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

        public PConstant GetConstant()
        {
            return this._constant_;
        }

        public void SetConstant(PConstant node)
        {
            if (this._constant_ != null)
            {
                this._constant_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._constant_ = node;
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
            return this.ToString(this._const_) + this.ToString(this._pos_) + this.ToString(this._type_) + this.ToString(this._constant_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._const_ == child)
            {
                this._const_ = null;
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

            if (this._constant_ == child)
            {
                this._constant_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._const_ == oldChild)
            {
                this.SetConst((TConst)newChild);
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

            if (this._constant_ == oldChild)
            {
                this.SetConstant((PConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




