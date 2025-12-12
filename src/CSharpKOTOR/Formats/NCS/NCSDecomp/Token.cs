// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public abstract class Token : Node
    {
        private string text;
        private int line;
        private int pos;
        public virtual string GetText()
        {
            return this.text;
        }

        public virtual void SetText(string text)
        {
            this.text = text;
        }

        public virtual int GetLine()
        {
            return this.line;
        }

        public virtual void SetLine(int line)
        {
            this.line = line;
        }

        public virtual int GetPos()
        {
            return this.pos;
        }

        public virtual void SetPos(int pos)
        {
            this.pos = pos;
        }

        public override string ToString()
        {
            return this.text.ToString() + " ";
        }

        public override void RemoveChild(Node child)
        {
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
        }
    }
}




