namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class StringConst : Const
    {
        private string value;

        public StringConst(object value)
        {
            this.type = new Utils.Type(5);
            if (value is string s)
            {
                if (s.StartsWith('"') && s.EndsWith('"'))
                {
                    this.value = s.Substring(1, s.Length - 2);
                }
                else
                {
                    this.value = s;
                }
            }
            else
            {
                this.value = value?.ToString() ?? "";
            }
            this.size = 1;
        }

        public string Value()
        {
            return this.value;
        }

        public override string ToString()
        {
            return this.value;
        }
    }
}

