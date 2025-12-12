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
        public virtual void Dispose()
        {
            if (this.type != null)
            {
                this.type.Dispose();
            }

            this.type = null;
        }

        public abstract void DoneParse();
        public abstract void DoneWithStack(LocalVarStack p0);
        public virtual void Close()
        {
            Dispose();
        }
    }
}




