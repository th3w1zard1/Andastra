using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AExpressionStatement : ScriptNode
    {
        private AExpression _exp;

        public AExpressionStatement(AExpression exp)
        {
            SetExp(exp);
        }

        public AExpression GetExp()
        {
            return _exp;
        }

        public void SetExp(AExpression exp)
        {
            if (_exp != null)
            {
                _exp.Parent(null);
            }
            if (exp != null)
            {
                exp.Parent((Scriptnode.ScriptNode)(object)this);
            }
            _exp = exp;
        }

        public override string ToString()
        {
            return GetTabs() + (_exp != null ? _exp.ToString() : "") + ";" + GetNewline();
        }

        public override void Close()
        {
            base.Close();
            if (_exp != null)
            {
                if (_exp is Scriptnode.ScriptNode expNode)
                {
                    expNode.Dispose();
                }
                else if (_exp is StackEntry expEntry)
                {
                    expEntry.Close();
                }
            }
            _exp = null;
        }
    }
}





