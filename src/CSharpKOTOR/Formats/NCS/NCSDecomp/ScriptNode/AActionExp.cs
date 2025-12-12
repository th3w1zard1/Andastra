using System.Collections.Generic;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AActionExp : Scriptnode.ScriptNode, AExpression
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
                param.Parent((Scriptnode.ScriptNode)(object)this);
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

        public StackEntry Stackentry()
        {
            return _stackEntry;
        }

        public void Stackentry(StackEntry p0)
        {
            _stackEntry = p0;
        }

        public new Scriptnode.ScriptNode Parent() => base.Parent();
        public new void Parent(Scriptnode.ScriptNode p0) => base.Parent(p0);

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

        public virtual void Close()
        {
            base.Dispose();
            if (_params != null)
            {
                foreach (var param in _params)
                {
                    if (param != null)
                    {
                        if (param is Scriptnode.ScriptNode paramNode)
                        {
                            paramNode.Dispose();
                        }
                        else if (param is StackEntry paramEntry)
                        {
                            paramEntry.Close();
                        }
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





