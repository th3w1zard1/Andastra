using BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode;
using BioWare.NET.Resource.Formats.NCS.Decomp.Stack;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.ScriptNode
{
    public class AConst : ScriptNode, AExpression
    {
        private Const _theConst;

        public AConst(Const theConst)
        {
            _theConst = theConst;
        }

        public StackEntry Stackentry()
        {
            return _theConst;
        }

        public void Stackentry(StackEntry p0)
        {
            _theConst = (Const)p0;
        }

        public new ScriptNode Parent() => base.Parent();
        public new void Parent(ScriptNode p0) => base.Parent(p0);

        public override string ToString()
        {
            return _theConst != null ? _theConst.ToString() : "";
        }

        public override void Close()
        {
            if (_theConst != null)
            {
                _theConst.Close();
            }
            _theConst = null;
        }
    }
}





