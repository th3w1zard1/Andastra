namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class ObjectConst : Const
    {
        private int value;

        public ObjectConst(object value)
        {
            this.type = new Utils.Type(6);
            if (value is int i)
            {
                this.value = i;
            }
            else if (value is string s)
            {
                this.value = int.Parse(s);
            }
            else
            {
                this.value = 0;
            }
            this.size = 1;
        }

        public int Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            if (this.value == 0)
            {
                return "OBJECT_SELF";
            }
            if (this.value == 1)
            {
                return "OBJECT_INVALID";
            }
            return this.value.ToString();
        }
    }
}

