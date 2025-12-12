// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AStoreStateCommand : PStoreStateCommand
    {
        private TStorestate _storestate_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _offset_;
        private TIntegerConstant _sizeBp_;
        private TIntegerConstant _sizeSp_;
        private TSemi _semi_;
        public AStoreStateCommand()
        {
        }

        public AStoreStateCommand(TStorestate _storestate_, TIntegerConstant _pos_, TIntegerConstant _offset_, TIntegerConstant _sizeBp_, TIntegerConstant _sizeSp_, TSemi _semi_)
        {
            this.SetStorestate(_storestate_);
            this.SetPos(_pos_);
            this.SetOffset(_offset_);
            this.SetSizeBp(_sizeBp_);
            this.SetSizeSp(_sizeSp_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new AStoreStateCommand((TStorestate)this.CloneNode(this._storestate_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._offset_), (TIntegerConstant)this.CloneNode(this._sizeBp_), (TIntegerConstant)this.CloneNode(this._sizeSp_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAStoreStateCommand(this);
        }

        public TStorestate GetStorestate()
        {
            return this._storestate_;
        }

        public void SetStorestate(TStorestate node)
        {
            if (this._storestate_ != null)
            {
                this._storestate_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._storestate_ = node;
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

        public TIntegerConstant GetSizeBp()
        {
            return this._sizeBp_;
        }

        public void SetSizeBp(TIntegerConstant node)
        {
            if (this._sizeBp_ != null)
            {
                this._sizeBp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._sizeBp_ = node;
        }

        public TIntegerConstant GetSizeSp()
        {
            return this._sizeSp_;
        }

        public void SetSizeSp(TIntegerConstant node)
        {
            if (this._sizeSp_ != null)
            {
                this._sizeSp_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._sizeSp_ = node;
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
            return this.ToString(this._storestate_) + this.ToString(this._pos_) + this.ToString(this._offset_) + this.ToString(this._sizeBp_) + this.ToString(this._sizeSp_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._storestate_ == child)
            {
                this._storestate_ = null;
                return;
            }

            if (this._pos_ == child)
            {
                this._pos_ = null;
                return;
            }

            if (this._offset_ == child)
            {
                this._offset_ = null;
                return;
            }

            if (this._sizeBp_ == child)
            {
                this._sizeBp_ = null;
                return;
            }

            if (this._sizeSp_ == child)
            {
                this._sizeSp_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._storestate_ == oldChild)
            {
                this.SetStorestate((TStorestate)newChild);
                return;
            }

            if (this._pos_ == oldChild)
            {
                this.SetPos((TIntegerConstant)newChild);
                return;
            }

            if (this._offset_ == oldChild)
            {
                this.SetOffset((TIntegerConstant)newChild);
                return;
            }

            if (this._sizeBp_ == oldChild)
            {
                this.SetSizeBp((TIntegerConstant)newChild);
                return;
            }

            if (this._sizeSp_ == oldChild)
            {
                this.SetSizeSp((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




