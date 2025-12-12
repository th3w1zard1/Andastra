//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public sealed class ACommandBlock : PCommandBlock
    {
        private readonly TypedLinkedList _cmd_;
        public ACommandBlock()
        {
            this._cmd_ = new TypedLinkedList(new Cmd_Cast());
        }

        public ACommandBlock(IList<object> _cmd_)
        {
            this._cmd_ = new TypedLinkedList(new Cmd_Cast());
            this._cmd_.Clear();
            foreach (var item in _cmd_)
            {
                this._cmd_.Add(item);
            }
        }

        public ACommandBlock(XPCmd _cmd_)
        {
            this._cmd_ = new TypedLinkedList(new Cmd_Cast());
            if (_cmd_ != null)
            {
                while (_cmd_ is X1PCmd)
                {
                    this._cmd_.AddFirst(((X1PCmd)_cmd_).GetPCmd());
                    _cmd_ = ((X1PCmd)_cmd_).GetXPCmd();
                }

                this._cmd_.AddFirst(((X2PCmd)_cmd_).GetPCmd());
            }
        }

        public override object Clone()
        {
            var list = new List<object>();
            foreach (var item in this._cmd_)
            {
                list.Add(item);
            }
            return new ACommandBlock(list);
        }
        public override void Apply(Switch sw)
        {
            if (sw is IAnalysis ia)
            {
                ia.CaseACommandBlock(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public TypedLinkedList GetCmd()
        {
            return this._cmd_;
        }

        public void SetCmd(IList<object> list)
        {
            this._cmd_.Clear();
            foreach (var item in list)
            {
                this._cmd_.Add(item);
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var cmd in this._cmd_)
            {
                sb.Append(cmd != null ? cmd.ToString() : "");
            }
            return sb.ToString();
        }

        public override void RemoveChild(Node child)
        {
            if (this._cmd_.Remove(child))
            {
                return;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            ListIterator i = this._cmd_.ListIterator(0);
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

        private class Cmd_Cast : Cast
        {
            internal Cmd_Cast()
            {
            }

            public override object CastInternal(object o)
            {
                PCmd node = (PCmd)o;
                Node parent = node.Parent();
                if (parent != null)
                {
                    parent.RemoveChild(node);
                }

                // Note: Parent() expects a Node, but we're in a Cast class
                // The parent should be set by the caller, not here
                return node;
            }
        }
    }
}




