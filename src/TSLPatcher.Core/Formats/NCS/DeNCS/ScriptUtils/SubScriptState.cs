namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptUtils
{
    public class SubScriptState
    {
        private string _name;

        public SubScriptState()
        {
            _name = "";
        }

        public string GetName()
        {
            return _name;
        }

        public void SetName(string name)
        {
            _name = name;
        }

        public bool IsMain()
        {
            return false;
        }

        public void Close()
        {
        }

        public System.Collections.Generic.List<object> GetVariables()
        {
            return new System.Collections.Generic.List<object>();
        }

        public object GetProto()
        {
            return null;
        }

        public override string ToString()
        {
            return "";
        }

        public string ToStringGlobals()
        {
            return "";
        }
    }
}
