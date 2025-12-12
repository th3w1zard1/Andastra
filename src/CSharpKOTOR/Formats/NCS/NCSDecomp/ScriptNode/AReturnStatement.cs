using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AReturnStatement : ScriptNode
    {
        private AExpression _returnExp;

        public AReturnStatement() : this(null)
        {
        }

        public AReturnStatement(AExpression returnExp)
        {
            if (returnExp != null)
            {
                SetReturnExp(returnExp);
            }
        }

        public AExpression GetReturnExp()
        {
            return _returnExp;
        }

        public AExpression GetExp()
        {
            return _returnExp;
        }

        public void SetReturnExp(AExpression returnExp)
        {
            if (_returnExp != null && _returnExp is ScriptNode returnExpNode)
            {
                returnExpNode.SetParent(null);
            }
            if (returnExp != null && returnExp is ScriptNode returnExpNode2)
            {
                returnExpNode2.SetParent(this);
            }
            _returnExp = returnExp;
        }

        public override string ToString()
        {
            if (_returnExp == null)
            {
                return GetTabs() + "return;" + GetNewline();
            }
            return GetTabs() + "return " + _returnExp.ToString() + ";" + GetNewline();
        }

        public override void Close()
        {
            base.Close();
            if (_returnExp != null && _returnExp is ScriptNode returnExpNode)
            {
                returnExpNode.Close();
            }
            _returnExp = null;
        }
    }
}





