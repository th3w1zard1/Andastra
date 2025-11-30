namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class FloatConst : Const
    {
        private float value;

        public FloatConst(object value)
        {
            this.type = new Utils.Type(4);
            if (value is float f)
            {
                this.value = f;
            }
            else if (value is double d)
            {
                this.value = (float)d;
            }
            else if (value is string s)
            {
                this.value = float.Parse(s);
            }
            else if (value is int i)
            {
                this.value = i;
            }
            else
            {
                this.value = 0.0f;
            }
            this.size = 1;
        }

        public float Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            return this.value.ToString();
        }
    }
}

