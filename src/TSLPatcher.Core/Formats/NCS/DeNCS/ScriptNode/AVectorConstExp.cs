using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AVectorConstExp : AExpression
    {
        private AExpression _exp1;
        private AExpression _exp2;
        private AExpression _exp3;

        public AVectorConstExp(AExpression exp1, AExpression exp2, AExpression exp3)
        {
            SetExp1(exp1);
            SetExp2(exp2);
            SetExp3(exp3);
        }

        public AExpression GetExp1()
        {
            return _exp1;
        }

        public void SetExp1(AExpression exp1)
        {
            _exp1 = exp1;
            if (exp1 != null)
            {
                exp1.SetParent(this);
            }
        }

        public AExpression GetExp2()
        {
            return _exp2;
        }

        public void SetExp2(AExpression exp2)
        {
            _exp2 = exp2;
            if (exp2 != null)
            {
                exp2.SetParent(this);
            }
        }

        public AExpression GetExp3()
        {
            return _exp3;
        }

        public void SetExp3(AExpression exp3)
        {
            _exp3 = exp3;
            if (exp3 != null)
            {
                exp3.SetParent(this);
            }
        }

        public override StackEntry StackEntry()
        {
            return null;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            // Do nothing
        }

        public override string ToString()
        {
            return "[" + (_exp1 != null ? _exp1.ToString() : "") + "," + 
                   (_exp2 != null ? _exp2.ToString() : "") + "," + 
                   (_exp3 != null ? _exp3.ToString() : "") + "]";
        }

        public override void Close()
        {
            base.Close();
            if (_exp1 != null)
            {
                _exp1.Close();
                _exp1 = null;
            }
            if (_exp2 != null)
            {
                _exp2.Close();
                _exp2 = null;
            }
            if (_exp3 != null)
            {
                _exp3.Close();
                _exp3 = null;
            }
        }
    }
}

