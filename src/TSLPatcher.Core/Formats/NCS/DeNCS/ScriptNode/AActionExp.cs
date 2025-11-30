using System.Collections.Generic;
using System.Text;
using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AActionExp : AExpression
    {
        private string _action;
        private int _id;
        private List<AExpression> _params;
        private StackEntry _stackEntry;

        public AActionExp(string action, int idVal, List<AExpression> @params)
        {
            _action = action;
            _id = idVal;
            _params = new List<AExpression>();
            if (@params != null)
            {
                foreach (var param in @params)
                {
                    AddParam(param);
                }
            }
        }

        public void AddParam(AExpression param)
        {
            if (param != null)
            {
                param.SetParent(this);
            }
            _params.Add(param);
        }

        public AExpression GetParam(int pos)
        {
            return _params[pos];
        }

        public List<AExpression> GetParams()
        {
            return _params;
        }

        public string GetAction()
        {
            return _action;
        }

        public void SetAction(string action)
        {
            _action = action;
        }

        public int GetId()
        {
            return _id;
        }

        public void SetId(int id)
        {
            _id = id;
        }

        public override StackEntry StackEntry()
        {
            return _stackEntry;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            _stackEntry = stackEntry;
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            buff.Append(_action + "(");
            string prefix = "";
            foreach (var param in _params)
            {
                buff.Append(prefix + (param != null ? param.ToString() : ""));
                prefix = ", ";
            }
            buff.Append(")");
            return buff.ToString();
        }

        public override void Close()
        {
            base.Close();
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    if (param != null)
                    {
                        param.Close();
                    }
                }
            }
            _params = null;
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}

