// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ASize : PSize
    {
        private TT _t_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _integerConstant_;
        private TSemi _semi_;
        public ASize()
        {
        }

        public ASize(TT _t_, TIntegerConstant _pos_, TIntegerConstant _integerConstant_, TSemi _semi_)
        {
            this.SetT(_t_);
            this.SetPos(_pos_);
            this.SetIntegerConstant(_integerConstant_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new ASize((TT)this.CloneNode(this._t_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._integerConstant_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseASize(this);
        }

        public TT GetT()
        {
            return this._t_;
        }

        public void SetT(TT node)
        {
            if (this._t_ != null)
            {
                this._t_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._t_ = node;
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

        public TIntegerConstant GetIntegerConstant()
        {
            return this._integerConstant_;
        }

        public void SetIntegerConstant(TIntegerConstant node)
        {
            if (this._integerConstant_ != null)
            {
                this._integerConstant_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._integerConstant_ = node;
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
            return this.ToString(this._t_) + this.ToString(this._pos_) + this.ToString(this._integerConstant_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._t_ == child)
            {
                this._t_ = null;
                return;
            }

            if (this._pos_ == child)
            {
                this._pos_ = null;
                return;
            }

            if (this._integerConstant_ == child)
            {
                this._integerConstant_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._t_ == oldChild)
            {
                this.SetT((TT)newChild);
                return;
            }

            if (this._pos_ == oldChild)
            {
                this.SetPos((TIntegerConstant)newChild);
                return;
            }

            if (this._integerConstant_ == oldChild)
            {
                this.SetIntegerConstant((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




