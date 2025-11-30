namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class Const : StackEntry
    {
        public static Const NewConst(Utils.Type type, object value)
        {
            switch (type.ByteValue())
            {
                case 3:
                    return new IntConst(value);
                case 4:
                    return new FloatConst(value);
                case 5:
                    return new StringConst(value);
                case 6:
                    return new ObjectConst(value);
                default:
                    throw new Utils.RuntimeException("Invalid const type " + type);
            }
        }

        public override void RemovedFromStack(LocalStack stack)
        {
        }

        public override void AddedToStack(LocalStack stack)
        {
        }

        public override void DoneParse()
        {
        }

        public override void DoneWithStack(LocalVarStack stack)
        {
        }

        public override string ToString()
        {
            return "";
        }

        public override StackEntry GetElement(int stackpos)
        {
            if (stackpos != 1)
            {
                throw new Utils.RuntimeException("Position > 1 for const, not struct");
            }
            return this;
        }
    }
}

