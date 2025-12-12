//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AActionCommand : PActionCommand
    {
        private TAction _action_;
        private TIntegerConstant _pos_;
        private TIntegerConstant _type_;
        private TIntegerConstant _id_;
        private TIntegerConstant _argCount_;
        private TSemi _semi_;
        public AActionCommand()
        {
        }

        public AActionCommand(TAction _action_, TIntegerConstant _pos_, TIntegerConstant _type_, TIntegerConstant _id_, TIntegerConstant _argCount_, TSemi _semi_)
        {
            this.SetAction(_action_);
            this.SetPos(_pos_);
            this.SetType(_type_);
            this.SetId(_id_);
            this.SetArgCount(_argCount_);
            this.SetSemi(_semi_);
        }

        public override object Clone()
        {
            return new AActionCommand((TAction)this.CloneNode(this._action_), (TIntegerConstant)this.CloneNode(this._pos_), (TIntegerConstant)this.CloneNode(this._type_), (TIntegerConstant)this.CloneNode(this._id_), (TIntegerConstant)this.CloneNode(this._argCount_), (TSemi)this.CloneNode(this._semi_));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAActionCommand(this);
        }

        public TAction GetAction()
        {
            return this._action_;
        }

        public void SetAction(TAction node)
        {
            if (this._action_ != null)
            {
                this._action_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._action_ = node;
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

        public TIntegerConstant GetId()
        {
            return this._id_;
        }

        public void SetId(TIntegerConstant node)
        {
            if (this._id_ != null)
            {
                this._id_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._id_ = node;
        }

        public TIntegerConstant GetArgCount()
        {
            return this._argCount_;
        }

        public void SetArgCount(TIntegerConstant node)
        {
            if (this._argCount_ != null)
            {
                this._argCount_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._argCount_ = node;
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
            return this.ToString(this._action_) + this.ToString(this._pos_) + this.ToString(this._type_) + this.ToString(this._id_) + this.ToString(this._argCount_) + this.ToString(this._semi_);
        }

        public override void RemoveChild(Node child)
        {
            if (this._action_ == child)
            {
                this._action_ = null;
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

            if (this._id_ == child)
            {
                this._id_ = null;
                return;
            }

            if (this._argCount_ == child)
            {
                this._argCount_ = null;
                return;
            }

            if (this._semi_ == child)
            {
                this._semi_ = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._action_ == oldChild)
            {
                this.SetAction((TAction)newChild);
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

            if (this._id_ == oldChild)
            {
                this.SetId((TIntegerConstant)newChild);
                return;
            }

            if (this._argCount_ == oldChild)
            {
                this.SetArgCount((TIntegerConstant)newChild);
                return;
            }

            if (this._semi_ == oldChild)
            {
                this.SetSemi((TSemi)newChild);
            }
        }
    }
}




