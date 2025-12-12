using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AActionArgExp : Scriptnode.ScriptNode, AExpression
    {
        public AActionArgExp()
        {
        }

        Scriptnode.ScriptNode AExpression.Parent() => base.Parent();
        void AExpression.Parent(Scriptnode.ScriptNode p0) => base.Parent(p0);
        public StackEntry Stackentry() => null;
        public void Stackentry(StackEntry p0) { }
    }
}





