using TSLPatcher.Core.Formats.NCS.DeNCS.Stack;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
{
    public class AConst : AExpression
    {
        private Const _theConst;

        public AConst(Const theConst)
        {
            _theConst = theConst;
        }

        public override StackEntry StackEntry()
        {
            return _theConst;
        }

        public override void SetStackEntry(StackEntry stackEntry)
        {
            _theConst = (Const)stackEntry;
        }

        public override string ToString()
        {
            return _theConst != null ? _theConst.ToString() : "";
        }

        public override void Close()
        {
            base.Close();
            if (_theConst != null)
            {
                _theConst.Close();
            }
            _theConst = null;
        }
    }
}

