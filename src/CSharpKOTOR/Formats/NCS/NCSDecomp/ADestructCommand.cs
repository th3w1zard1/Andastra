// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ADestructCommand : PDestructCommand
    {
        private TDestruct _destruct_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _type_;
        private TIntegerConstant _sizeRem_;
        private TIntegerConstant _offset_;
        private TIntegerConstant _sizeSave_;
        private TSemi _semi_;
        public ADestructCommand()
        {
        }

        public ADestructCommand(TDestruct _destruct_, TIntegerConstant _pos_, TIntegerConstant _type_, TIntegerConstant _sizeRem_, TIntegerConstant _offset_, TIntegerConstant _sizeSave_, TSemi _semi_)
        {
            this.SetDestruct(_destruct_);
            this.SetPos(_pos_);
            this.SetType(_type_);
            this.SetSizeRem(_sizeRem_);
            this.SetOffset(_offset_);
            this.SetSizeSave(_sizeSave_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new ADestructCommand((TDestruct)this.CloneNode(this._destruct_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._type_), (TIntegerConstant)this.CloneNode(this._sizeRem_), (TIntegerConstant)this.CloneNode(this._offset_), (TIntegerConstant)this.CloneNode(this._sizeSave_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseADestructCommand(this);
        }

        public TDestruct GetDestruct()
        {
            return this._destruct_;
        }

        public void SetDestruct(TDestruct node)
        {
            if (this._destruct_ != null)
            {
                this._destruct_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._destruct_ = node;
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

        public TIntegerConstant GetSizeRem()
        {
            return this._sizeRem_;
        }

        public void SetSizeRem(TIntegerConstant node)
        {
            if (this._sizeRem_ != null)
            {
                this._sizeRem_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._sizeRem_ = node;
        }

        public TIntegerConstant GetOffset()
        {
            return this._offset_;
        }

        public void SetOffset(TIntegerConstant node)
        {
            if (this._offset_ != null)
            {
                this._offset_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._offset_ = node;
        }

        public TIntegerConstant GetSizeSave()
        {
            return this._sizeSave_;
        }

        public void SetSizeSave(TIntegerConstant node)
        {
            if (this._sizeSave_ != null)
            {
                this._sizeSave_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._sizeSave_ = node;
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
            return this.ToString(this._destruct_) + this.ToString(this._pos_) + this.ToString(this._type_) + this.ToString(this._sizeRem_) + this.ToString(this._offset_) + this.ToString(this._sizeSave_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._destruct_ == child)
            {
                this._destruct_ = null;
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

            if (this._sizeRem_ == child)
            {
                this._sizeRem_ = null;
                return;
            }

            if (this._offset_ == child)
            {
                this._offset_ = null;
                return;
            }

            if (this._sizeSave_ == child)
            {
                this._sizeSave_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._destruct_ == oldChild)
            {
                this.SetDestruct((TDestruct)newChild);
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

            if (this._sizeRem_ == oldChild)
            {
                this.SetSizeRem((TIntegerConstant)newChild);
                return;
            }

            if (this._offset_ == oldChild)
            {
                this.SetOffset((TIntegerConstant)newChild);
                return;
            }

            if (this._sizeSave_ == oldChild)
            {
                this.SetSizeSave((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




