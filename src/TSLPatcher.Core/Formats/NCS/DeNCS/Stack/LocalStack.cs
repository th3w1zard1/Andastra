using System;
using System.Collections.Generic;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class LocalStack
    {
        protected List<object> stack;

        public LocalStack()
        {
            this.stack = new List<object>();
        }

        public int Size()
        {
            return this.stack.Count;
        }

        public virtual object Clone()
        {
            LocalStack newStack = new LocalStack();
            newStack.stack = new List<object>(this.stack);
            return newStack;
        }

        public virtual void Close()
        {
            this.stack = null;
        }
    }

    public class LocalVarStack : LocalStack
    {
        public LocalVarStack()
            : base()
        {
        }

        public override void Close()
        {
            if (this.stack != null)
            {
                foreach (object item in this.stack)
                {
                    if (item is StackEntry entry)
                    {
                        entry.Close();
                    }
                }
            }
            base.Close();
        }

        public void DoneParse()
        {
            if (this.stack != null)
            {
                foreach (object item in this.stack)
                {
                    if (item is StackEntry entry)
                    {
                        entry.DoneParse();
                    }
                }
            }
            this.stack = null;
        }

        public void DoneWithStack()
        {
            if (this.stack != null)
            {
                foreach (object item in this.stack)
                {
                    if (item is StackEntry entry)
                    {
                        entry.DoneWithStack(this);
                    }
                }
                this.stack = null;
            }
        }

        public new int Size()
        {
            int size = 0;
            if (this.stack != null)
            {
                foreach (object item in this.stack)
                {
                    if (item is StackEntry entry)
                    {
                        size += entry.Size();
                    }
                }
            }
            return size;
        }

        public void Push(StackEntry entry)
        {
            if (this.stack == null)
            {
                this.stack = new List<object>();
            }
            this.stack.Insert(0, entry);
            entry.AddedToStack(this);
        }

        public StackEntry Get(int offset)
        {
            if (this.stack == null)
            {
                throw new Utils.RuntimeException("Stack is null");
            }
            int pos = 0;
            foreach (object item in this.stack)
            {
                if (item is StackEntry entry)
                {
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
            }
            throw new Utils.RuntimeException("offset " + offset + " was greater than stack size " + pos);
        }

        public Utils.Type GetType(int offset)
        {
            return this.Get(offset).Type();
        }

        public StackEntry Remove()
        {
            if (this.stack == null || this.stack.Count == 0)
            {
                throw new Utils.RuntimeException("Stack is empty");
            }
            StackEntry entry = (StackEntry)this.stack[0];
            this.stack.RemoveAt(0);
            entry.RemovedFromStack(this);
            return entry;
        }

        // TODO: Uncomment when SubroutineAnalysisData is created
        /*
        public void Destruct(int removesize, int savestart, int savesize, Utils.SubroutineAnalysisData subdata)
        {
            this.Structify(1, removesize, subdata);
            if (savesize > 1)
            {
                this.Structify(removesize - (savestart + savesize) + 1, savesize, subdata);
            }
            Variable structVar = (Variable)this.stack[0];
            Variable element = (Variable)structVar.GetElement(removesize - (savestart + savesize) + 1);
            this.stack[0] = element;
        }

        public VarStruct Structify(int firstelement, int count, Utils.SubroutineAnalysisData subdata)
        {
            if (this.stack == null)
            {
                return null;
            }
            int pos = 0;
            for (int i = 0; i < this.stack.Count; i++)
            {
                if (this.stack[i] is StackEntry entry)
                {
                    pos += entry.Size();
                    if (pos == firstelement)
                    {
                        VarStruct varstruct = new VarStruct();
                        varstruct.AddVarStackOrder((Variable)entry);
                        this.stack[i] = varstruct;
                        int j = i + 1;
                        while (j < this.stack.Count && pos <= firstelement + count - 1)
                        {
                            entry = (StackEntry)this.stack[j];
                            pos += entry.Size();
                            varstruct.AddVarStackOrder((Variable)entry);
                            this.stack.RemoveAt(j);
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
            }
            return null;
        }
        */

        public override string ToString()
        {
            string newline = Environment.NewLine;
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            int max = this.stack != null ? this.stack.Count : 0;
            buffer.Append("---stack, size " + max + "---" + newline);
            for (int i = 0; i < max; i++)
            {
                if (this.stack[i] is StackEntry entry)
                {
                    buffer.Append("-->" + i + entry.ToString() + newline);
                }
            }
            return buffer.ToString();
        }

        public override object Clone()
        {
            LocalVarStack newStack = new LocalVarStack();
            if (this.stack != null)
            {
                newStack.stack = new List<object>(this.stack);
            }
            return newStack;
        }
    }

    public class LocalTypeStack : LocalStack
    {
        public LocalTypeStack()
            : base()
        {
        }

        public void Push(Utils.Type type)
        {
            if (this.stack == null)
            {
                this.stack = new List<object>();
            }
            this.stack.Insert(0, type);
        }

        public Utils.Type Get(int offset)
        {
            if (this.stack == null)
            {
                return new Utils.Type(-1);
            }
            int pos = 0;
            foreach (object item in this.stack)
            {
                if (item is Utils.Type type)
                {
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
            }
            return new Utils.Type(-1);
        }

        // TODO: Uncomment when SubroutineState is created
        /*
        public Utils.Type Get(int offset, Utils.SubroutineState state)
        {
            if (this.stack == null)
            {
                return new Utils.Type(-1);
            }
            int pos = 0;
            foreach (object item in this.stack)
            {
                if (item is Utils.Type type)
                {
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
            }
            if (state.IsPrototyped())
            {
                Utils.Type type = state.GetParamType(offset - pos);
                if (!type.Equals((sbyte)0))
                {
                    return type;
                }
            }
            return new Utils.Type(-1);
        }
        */

        public void Remove(int count)
        {
            if (this.stack == null)
            {
                return;
            }
            for (int i = 0; i < count && this.stack.Count > 0; i++)
            {
                this.stack.RemoveAt(0);
            }
        }

        // TODO: Uncomment when SubroutineState is created
        /*
        public void RemoveParams(int count, Utils.SubroutineState state)
        {
            if (this.stack == null)
            {
                return;
            }
            List<Utils.Type> paramsList = new List<Utils.Type>();
            for (int i = 0; i < count && this.stack.Count > 0; i++)
            {
                if (this.stack[0] is Utils.Type type)
                {
                    paramsList.Insert(0, type);
                    this.stack.RemoveAt(0);
                }
            }
            state.UpdateParams(paramsList);
        }
        */

        public int RemovePrototyping(int count)
        {
            if (this.stack == null)
            {
                return count;
            }
            int paramCount = 0;
            int i = 0;
            while (i < count)
            {
                if (this.stack.Count == 0)
                {
                    paramCount++;
                    i++;
                }
                else
                {
                    if (this.stack[0] is Utils.Type type)
                    {
                        i += type.Size();
                        this.stack.RemoveAt(0);
                    }
                }
            }
            return paramCount;
        }

        public void Remove(int start, int count)
        {
            if (this.stack == null)
            {
                return;
            }
            int loc = start - 1;
            for (int i = 0; i < count && loc < this.stack.Count; i++)
            {
                this.stack.RemoveAt(loc);
            }
        }

        public override string ToString()
        {
            string newline = Environment.NewLine;
            System.Text.StringBuilder buffer = new System.Text.StringBuilder();
            int max = this.stack != null ? this.stack.Count : 0;
            buffer.Append("---stack, size " + max + "---" + newline);
            for (int i = 1; i <= max; i++)
            {
                if (this.stack[max - i] is Utils.Type type)
                {
                    buffer.Append("-->" + i + " is type " + type + newline);
                }
            }
            return buffer.ToString();
        }

        public override object Clone()
        {
            LocalTypeStack newStack = new LocalTypeStack();
            if (this.stack != null)
            {
                newStack.stack = new List<object>(this.stack);
            }
            return newStack;
        }
    }
}

