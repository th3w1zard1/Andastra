// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    public class LocalTypeStack : LocalStack
    {
        public virtual void Push(UtilsType type)
        {
            this.stack.AddFirst(type);
        }

        public virtual UtilsType Get(int offset)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                UtilsType type = (UtilsType)it.Next();
                pos += type.Size();
                if (pos > offset)
                {
                    return type.GetElement(pos - offset + 1);
                }

                if (pos == offset)
                {
                    return type.GetElement(1);
                }
            }

            return new UtilsType(unchecked((byte)(-1)));
        }

        public virtual UtilsType Get(int offset, SubroutineState state)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                UtilsType type = (UtilsType)it.Next();
                pos += type.Size();
                if (pos > offset)
                {
                    return type.GetElement(pos - offset + 1);
                }

                if (pos == offset)
                {
                    return type.GetElement(1);
                }
            }

            if (state.IsPrototyped())
            {
                UtilsType type = state.GetParamType(offset - pos);
                if (!type.Equals((byte)0))
                {
                    return type;
                }
            }

            return new UtilsType(unchecked((byte)(-1)));
        }

        public virtual void Remove(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                this.stack.RemoveFirst();
            }
        }

        public virtual void RemoveParams(int count, SubroutineState state)
        {
            LinkedList @params = new LinkedList();
            for (int i = 0; i < count; ++i)
            {
                UtilsType type = (UtilsType)this.stack.RemoveFirst();
                @params.AddFirst(type);
            }

            state.UpdateParams(@params);
        }

        public virtual int RemovePrototyping(int count)
        {
            int @params = 0;
            int i = 0;
            while (i < count)
            {
                if (this.stack.Count == 0)
                {
                    ++@params;
                    ++i;
                }
                else
                {
                    UtilsType type = (UtilsType)this.stack.RemoveFirst();
                    i += type.Size();
                }
            }

            return @params;
        }

        public virtual void Remove(int start, int count)
        {
            int loc = start - 1;
            for (int i = 0; i < count; ++i)
            {
                this.stack.Remove(loc);
            }
        }

        public override string ToString()
        {
            string newline = Environment.NewLine;
            StringBuilder buffer = new StringBuilder();
            int max = this.stack.Count;
            buffer.Append("---stack, size " + max + "---" + newline);
            for (int i = 1; i <= max; ++i)
            {
                UtilsType type = (UtilsType)this.stack[max - i];
                buffer.Append("-->" + i + " is type " + type + newline);
            }

            return buffer.ToString();
        }

        public override object Clone()
        {
            LocalTypeStack newStack = new LocalTypeStack();
            // Clone the custom LinkedList
            newStack.stack = new LinkedList();
            var it = this.stack.Iterator();
            while (it.HasNext())
            {
                newStack.stack.Add(it.Next());
            }
            return newStack;
        }
    }
}




