namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public abstract class StackEntry
    {
        protected Utils.Type type;
        protected int size;

        public Utils.Type Type()
        {
            return this.type;
        }

        public int Size()
        {
            return this.size;
        }

        public abstract void RemovedFromStack(LocalStack stack);

        public abstract void AddedToStack(LocalStack stack);

        public abstract override string ToString();

        public abstract StackEntry GetElement(int pos);

        public virtual void Close()
        {
            if (this.type != null)
            {
                this.type.Close();
            }
            this.type = null;
        }

        public abstract void DoneParse();

        public abstract void DoneWithStack(LocalVarStack stack);
    }
}

