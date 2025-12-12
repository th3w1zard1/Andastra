// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class AProgram : PProgram
    {
        private PSize _size_;
        private PRsaddCommand _conditional_;
        private PJumpToSubroutine _jumpToSubroutine_;
        private PReturn _return_;
        private readonly TypedLinkedList _subroutine_;
        public AProgram()
        {
            this._subroutine_ = new TypedLinkedList(new Subroutine_Cast());
        }

        public AProgram(PSize _size_, PRsaddCommand _conditional_, PJumpToSubroutine _jumpToSubroutine_, PReturn _return_, IList<object> _subroutine_)
        {
            this._subroutine_ = new TypedLinkedList(new Subroutine_Cast());
            this.SetSize(_size_);
            this.SetConditional(_conditional_);
            this.SetJumpToSubroutine(_jumpToSubroutine_);
            this.SetReturn(_return_);
            this._subroutine_.Clear();
            foreach (var item in _subroutine_)
            {
                this._subroutine_.AddLast(item);
            }
        }

        public AProgram(PSize _size_, PRsaddCommand _conditional_, PJumpToSubroutine _jumpToSubroutine_, PReturn _return_, XPSubroutine _subroutine_)
        {
            this._subroutine_ = new TypedLinkedList(new Subroutine_Cast());
            this.SetSize(_size_);
            this.SetConditional(_conditional_);
            this.SetJumpToSubroutine(_jumpToSubroutine_);
            this.SetReturn(_return_);
            if (_subroutine_ != null)
            {
                while (_subroutine_ is X1PSubroutine)
                {
                    this._subroutine_.AddFirst(((X1PSubroutine)_subroutine_).GetPSubroutine());
                    _subroutine_ = ((X1PSubroutine)_subroutine_).GetXPSubroutine();
                }

                this._subroutine_.AddFirst(((X2PSubroutine)_subroutine_).GetPSubroutine());
            }
        }

        public override object Clone()
        {
            List<object> subroutineList = new List<object>();
            foreach (object item in this._subroutine_)
            {
                subroutineList.Add(item);
            }
            return new AProgram((PSize)this.CloneNode(this._size_), (PRsaddCommand)this.CloneNode(this._conditional_), (PJumpToSubroutine)this.CloneNode(this._jumpToSubroutine_), (PReturn)this.CloneNode(this._return_), this.CloneList(subroutineList));
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseAProgram(this);
        }

        public PSize GetSize()
        {
            return this._size_;
        }

        public void SetSize(PSize node)
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

        public PRsaddCommand GetConditional()
        {
            return this._conditional_;
        }

        public void SetConditional(PRsaddCommand node)
        {
            if (this._conditional_ != null)
            {
                this._conditional_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._conditional_ = node;
        }

        public PJumpToSubroutine GetJumpToSubroutine()
        {
            return this._jumpToSubroutine_;
        }

        public void SetJumpToSubroutine(PJumpToSubroutine node)
        {
            if (this._jumpToSubroutine_ != null)
            {
                this._jumpToSubroutine_.Parent(null);
            }

            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                node.Parent(this);
            }

            this._jumpToSubroutine_ = node;
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

        public TypedLinkedList GetSubroutine()
        {
            return this._subroutine_;
        }

        public void SetSubroutine(IList<object> list)
        {
            this._subroutine_.Clear();
            foreach (var item in list)
            {
                this._subroutine_.AddLast(item);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.ToString(this._size_));
            sb.Append(this.ToString(this._conditional_));
            sb.Append(this.ToString(this._jumpToSubroutine_));
            sb.Append(this.ToString(this._return_));
            foreach (var sub in this._subroutine_)
            {
                sb.Append(this.ToString((Node)sub));
            }
            return sb.ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._size_ == child)
            {
                this._size_ = null;
                return;
            }

            if (this._conditional_ == child)
            {
                this._conditional_ = null;
                return;
            }

            if (this._jumpToSubroutine_ == child)
            {
                this._jumpToSubroutine_ = null;
                return;
            }

            if (this._return_ == child)
            {
                this._return_ = null;
                return;
            }

            if (this._subroutine_.Remove(child))
            {
                return;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (this._size_ == oldChild)
            {
                this.SetSize((PSize)newChild);
                return;
            }

            if (this._conditional_ == oldChild)
            {
                this.SetConditional((PRsaddCommand)newChild);
                return;
            }

            if (this._jumpToSubroutine_ == oldChild)
            {
                this.SetJumpToSubroutine((PJumpToSubroutine)newChild);
                return;
            }

            if (this._return_ == oldChild)
            {
                this.SetReturn((PReturn)newChild);
                return;
            }

            ListIterator i = this._subroutine_.ListIterator();
            while (i.HasNext())
            {
                if (i.Next() == oldChild)
                {
                    if (newChild != null)
                    {
                        i.Set(newChild);
                        oldChild.Parent(null);
                        return;
                    }

                    i.Remove();
                    oldChild.Parent(null);
                }
            }
        }

        private class Subroutine_Cast : Cast
        {
            internal Subroutine_Cast()
            {
            }

            public override object CastInternal(object o)
            {
                PSubroutine node = (PSubroutine)o;
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }

                return node;
            }
        }
    }
}




