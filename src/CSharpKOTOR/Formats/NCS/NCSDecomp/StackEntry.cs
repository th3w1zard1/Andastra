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
    public abstract class StackEntry
    {
        protected UtilsType type;
        protected int size;
        public virtual UtilsType Type()
        {
            return this.type;
        }

        public virtual int Size()
        {
            return this.size;
        }

        public abstract void RemovedFromStack(LocalStack p0);
        public abstract void AddedToStack(LocalStack p0);
        public abstract override string ToString();
        public abstract StackEntry GetElement(int p0);
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:35-38
        // Original: public void close() { ... this.type.close(); ... }
        public virtual void Close()
        {
            if (this.type != null)
            {
                this.type.Close();
            }

            this.type = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:42
        // Original: public abstract void doneParse();
        public abstract void DoneParse();

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:44
        // Original: public abstract void doneWithStack(LocalVarStack var1);
        public abstract void DoneWithStack(LocalVarStack p0);
    }
}




