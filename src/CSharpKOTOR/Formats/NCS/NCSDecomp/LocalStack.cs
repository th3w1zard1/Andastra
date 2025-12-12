// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java
// Original: public class LocalStack<T> implements Cloneable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:13-31
    // Original: public class LocalStack<T> implements Cloneable
    public class LocalStack
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:14
        // Original: protected LinkedList<T> stack = new LinkedList<>();
        protected LinkedList stack = new LinkedList();

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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/LocalStack.java:28-30
        // Original: public void close()
        public virtual void Close()
        {
            this.stack = null;
        }
    }
}




