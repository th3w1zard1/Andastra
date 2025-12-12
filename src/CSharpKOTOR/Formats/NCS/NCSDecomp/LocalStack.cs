// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    public class LocalStack
    {
        protected LinkedList stack;
        public LocalStack()
        {
            this.stack = new LinkedList();
        }

        public virtual int Size()
        {
            return this.stack.Count;
        }

        public virtual object Clone()
        {
            LocalStack newStack = new LocalStack();
            // Clone the custom LinkedList
            newStack.stack = new LinkedList();
            var it = this.stack.Iterator();
            while (it.HasNext())
            {
                newStack.stack.Add(it.Next());
            }
            return newStack;
        }

        public virtual void Dispose()
        {
            this.stack = null;
        }
    }
}




