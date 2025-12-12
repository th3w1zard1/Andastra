// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ABinaryCommand : PBinaryCommand
    {
        private PBinaryOp _binaryOp_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _type_;
        private TIntegerConstant _size_;
        private TSemi _semi_;
        public ABinaryCommand()
        {
        }

        public ABinaryCommand(PBinaryOp _binaryOp_, TIntegerConstant _pos_, TIntegerConstant _type_, TIntegerConstant _size_, TSemi _semi_)
        {
            this.SetBinaryOp(_binaryOp_);
            this.SetPos(_pos_);
            this.SetType(_type_);
            this.SetSize(_size_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new ABinaryCommand((PBinaryOp)this.CloneNode(this._binaryOp_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._type_), (TIntegerConstant)this.CloneNode(this._size_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseABinaryCommand(this);
        }

        public PBinaryOp GetBinaryOp()
        {
            return this._binaryOp_;
        }

        public void SetBinaryOp(PBinaryOp node)
        {
            if (this._binaryOp_ != null)
            {
                this._binaryOp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._binaryOp_ = node;
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

        public TIntegerConstant GetSize()
        {
            return this._size_;
        }

        public void SetSize(TIntegerConstant node)
        {
            if (this._size_ != null)
            {
                this._size_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._size_ = node;
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
            return this.ToString(this._binaryOp_) + this.ToString(this._pos_) + this.ToString(this._type_) + this.ToString(this._size_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._binaryOp_ == child)
            {
                this._binaryOp_ = null;
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

            if (this._size_ == child)
            {
                this._size_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._binaryOp_ == oldChild)
            {
                this.SetBinaryOp((PBinaryOp)newChild);
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

            if (this._size_ == oldChild)
            {
                this.SetSize((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




