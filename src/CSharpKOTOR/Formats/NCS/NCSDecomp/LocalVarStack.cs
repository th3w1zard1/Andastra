// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    public class LocalVarStack : LocalStack
    {
        public override void Dispose()
        {
            if (this.stack != null)
            {
                ListIterator it = this.stack.ListIterator();
                while (it.HasNext())
                {
                    object next = it.Next();
                    if (next is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else if (next is StackEntry entry)
                    {
                        entry.Close();
                    }
                }
            }

            base.Dispose();
        }

        public virtual void DoneParse()
        {
            if (this.stack != null)
            {
                ListIterator it = this.stack.ListIterator();
                while (it.HasNext())
                {
                    object next = it.Next();
                    if (next is StackEntry entry)
                    {
                        entry.DoneParse();
                    }
                }
            }

            this.stack = null;
        }

        public virtual void DoneWithStack()
        {
            if (this.stack != null)
            {
                ListIterator it = this.stack.ListIterator();
                while (it.HasNext())
                {
                    if (it.Next() is StackEntry entry)
                    {
                        entry.DoneWithStack(this);
                    }
                }

                this.stack = null;
            }
        }

        public override int Size()
        {
            int size = 0;
            ListIterator it = this.stack.ListIterator();
            while (it.HasNext())
            {
                if (it.Next() is StackEntry entry)
                {
                    size += entry.Size();
                }
            }

            return size;
        }

        public virtual void Push(StackEntry entry)
        {
            this.stack.AddFirst(entry);
            entry.AddedToStack(this);
        }

        public virtual StackEntry Get(int offset)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                StackEntry entry = (StackEntry)it.Next();
                pos += entry.Size();
                if (pos > offset)
                {
                    return entry.GetElement(pos - offset + 1);
                }

                if (pos == offset)
                {
                    return entry.GetElement(1);
                }
            }

            System.Console.WriteLine(this.ToString());
            throw new Exception("offset " + offset + " was greater than stack size " + pos);
        }

        public virtual UtilsType GetType(int offset)
        {
            return this.Get(offset).Type();
        }

        public virtual StackEntry this[int offset]
        {
            get { return this.Get(offset); }
        }

        public virtual StackEntry Remove()
        {
            StackEntry entry = (StackEntry)this.stack.RemoveFirst();
            entry.RemovedFromStack(this);
            return entry;
        }

        public virtual void Destruct(int removesize, int savestart, int savesize, SubroutineAnalysisData subdata)
        {
            this.Structify(1, removesize, subdata);
            if (savesize > 1)
            {
                this.Structify(removesize - (savestart + savesize) + 1, savesize, subdata);
            }

            Variable @struct = (Variable)this.stack.GetFirst();
            Variable element = (Variable)@struct.GetElement(removesize - (savestart + savesize) + 1);
            this.stack[0] = element;
            @struct = null;
            element = null;
        }

        public virtual VarStruct Structify(int firstelement, int count, SubroutineAnalysisData subdata)
        {
            ListIterator it = this.stack.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                StackEntry entry = (StackEntry)it.Next();
                pos += entry.Size();
                if (pos == firstelement)
                {
                    VarStruct varstruct = new VarStruct();
                    varstruct.AddVarStackOrder((Variable)entry);
                    it.Set(varstruct);
                    for (entry = (StackEntry)it.Next(), pos += entry.Size(); pos <= firstelement + count - 1; pos += entry.Size())
                    {
                        it.Remove();
                        varstruct.AddVarStackOrder((Variable)entry);
                        if (!it.HasNext())
                        {
                            break;
                        }

                        entry = (StackEntry)it.Next();
                    }

                    subdata.AddStruct(varstruct);
                    return varstruct;
                }

                if (pos == firstelement + count - 1)
                {
                    return (VarStruct)entry;
                }

                if (pos > firstelement + count - 1)
                {
                    return ((VarStruct)entry).Structify(firstelement - (pos - entry.Size()), count, subdata);
                }
            }

            return null;
        }

        public override string ToString()
        {
            string newline = Environment.NewLine;
            StringBuilder buffer = new StringBuilder();
            int max = this.stack.Count;
            buffer.Append("---stack, size " + max + "---" + newline);
            for (int i = 0; i < max; ++i)
            {
                StackEntry entry = (StackEntry)this.stack[i];
                buffer.Append("-->" + i + entry.ToString() + newline);
            }

            return buffer.ToString();
        }

        public override object Clone()
        {
            LocalVarStack newStack = new LocalVarStack();
            newStack.stack = this.stack.Clone();
            foreach (StackEntry entry in this.stack)
            {
                if (typeof(Variable).IsInstanceOfType(entry))
                {
                    ((Variable)entry).StackWasCloned(this, newStack);
                }
            }

            return newStack;
        }
    }
}




