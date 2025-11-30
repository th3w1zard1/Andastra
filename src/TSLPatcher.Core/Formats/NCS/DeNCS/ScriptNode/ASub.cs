using System.Collections.Generic;
using System.Text;
using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;
using TSLPatcher.Core.Formats.NCS.DeNCS.Utils;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class ASub : ScriptRootNode
    {
        private Type _type;
        private int _id;
        private List<AVarRef> _params;
        private string _name;
        private bool _isMain;

        public ASub() : this(0, null, null, 0, 0)
        {
        }

        public ASub(int typeVal, int? idVal, List<AVarRef> @params, int start, int end) : base(start, end)
        {
            _type = new Type((sbyte)typeVal);
            if (idVal.HasValue)
            {
                _id = idVal.Value;
                _params = new List<AVarRef>();
                if (@params != null)
                {
                    foreach (var param in @params)
                    {
                        AddParam(param);
                    }
                }
                _name = "sub" + _id;
            }
            else
            {
                _type = new Type(0);
                _params = null;
                _name = "";
            }
            SetTabs("");
        }

        public ASub(Type typeVal, int? idVal, List<AVarRef> @params, int start, int end) : base(start, end)
        {
            _type = typeVal;
            if (idVal.HasValue)
            {
                _id = idVal.Value;
                _params = new List<AVarRef>();
                if (@params != null)
                {
                    foreach (var param in @params)
                    {
                        AddParam(param);
                    }
                }
                _name = "sub" + _id;
            }
            else
            {
                _type = new Type(0);
                _params = null;
                _name = "";
            }
            SetTabs("");
        }

        public void AddParam(AVarRef param)
        {
            param.SetParent(this);
            if (_params == null)
            {
                _params = new List<AVarRef>();
            }
            _params.Add(param);
        }

        public override string ToString()
        {
            return GetHeader() + " {" + GetNewline() + GetBody() + "}" + GetNewline();
        }

        public string GetBody()
        {
            var buff = new StringBuilder();
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            return buff.ToString();
        }

        public string GetHeader()
        {
            var buff = new StringBuilder();
            buff.Append((_type != null ? _type.ToString() : "") + " " + _name + "(");
            string link = "";
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    var ptype = param.GetType();
                    buff.Append(link + (ptype != null ? ptype.ToString() : "") + " " + param.ToString());
                    link = ", ";
                }
            }
            buff.Append(")");
            return buff.ToString();
        }

        public void SetIsMain(bool isMain)
        {
            _isMain = isMain;
            if (isMain)
            {
                if (_type != null && _type.Equals(3))
                {
                    _name = "StartingConditional";
                }
                else
                {
                    _name = "main";
                }
            }
        }

        public bool IsMain()
        {
            return _isMain;
        }

        public Type GetType()
        {
            return _type;
        }

        public void SetName(string name)
        {
            _name = name;
        }

        public string GetName()
        {
            return _name;
        }

        public List<AVarRef> GetParams()
        {
            return _params;
        }

        public List<StackEntry> GetParamVars()
        {
            var varsList = new List<StackEntry>();
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    varsList.Add(param.GetVarVar());
                }
            }
            return varsList;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int id)
        {
            _id = id;
        }

        public override void Close()
        {
            base.Close();
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    param.Close();
                }
            }
            _params = null;
            if (_type != null)
            {
                _type.Close();
            }
            _type = null;
        }
    }
}

