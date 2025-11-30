namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class IntConst : Const
    {
        private long value;

        public IntConst(object value)
        {
            this.type = new Utils.Type(3);
            if (value is long l)
            {
                this.value = l;
            }
            else if (value is int i)
            {
                this.value = i;
            }
            else if (value is string s)
            {
                this.value = long.Parse(s);
            }
            else
            {
                this.value = 0;
            }
            this.size = 1;
        }

        public long Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            if (this.value == 0xFFFFFFFF)
            {
                return "0xFFFFFFFF";
            }
            return this.value.ToString();
        }
    }
}

