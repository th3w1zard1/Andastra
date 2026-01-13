// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:13-45
// Original: public abstract class StackEntry
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Utils;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.Stack
{
    public abstract class StackEntry
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:14-15
        // Original: protected Type type; protected int size;
        protected Utils.Type type;
        protected int size;

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:17-19
        // Original: public Type type() { return this.type; }
        public virtual Utils.Type Type()
        {
            return this.type;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:21-23
        // Original: public int size() { return this.size; }
        public virtual int Size()
        {
            return this.size;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:25
        // Original: public abstract void removedFromStack(LocalStack<?> var1);
        public abstract void RemovedFromStack(LocalStack p0);

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:27
        // Original: public abstract void addedToStack(LocalStack<?> var1);
        public abstract void AddedToStack(LocalStack p0);

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:30
        // Original: @Override public abstract String toString();
        public abstract override string ToString();

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:32
        // Original: public abstract StackEntry getElement(int var1);
        public abstract StackEntry GetElement(int p0);
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:35-38
        // Original: public void close() { ... this.type.close(); ... }
        public virtual void Close()
        {
            if (this.type != null)
            {
                this.type.Close();
            }

            this.type = null;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:42
        // Original: public abstract void doneParse();
        public abstract void DoneParse();

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/stack/StackEntry.java:44
        // Original: public abstract void doneWithStack(LocalVarStack var1);
        public abstract void DoneWithStack(LocalVarStack p0);
    }
}




