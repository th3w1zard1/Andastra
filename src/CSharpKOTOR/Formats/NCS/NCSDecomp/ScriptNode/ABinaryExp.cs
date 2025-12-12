using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ABinaryExp : Scriptnode.ScriptNode, AExpression
    {
        private AExpression _left;
        private AExpression _right;
        private string _op;
        private StackEntry _stackEntry;

        public ABinaryExp(AExpression left, AExpression right, string op)
        {
            SetLeft(left);
            SetRight(right);
            _op = op;
        }

        public AExpression GetLeft()
        {
            return _left;
        }

        public void SetLeft(AExpression left)
        {
            _left = left;
            if (left != null)
            {
                left.Parent((Scriptnode.ScriptNode)(object)this);
            }
        }

        public AExpression GetRight()
        {
            return _right;
        }

        public void SetRight(AExpression right)
        {
            _right = right;
            if (right != null)
            {
                right.Parent((Scriptnode.ScriptNode)(object)this);
            }
        }

        public string GetOp()
        {
            return _op;
        }

        public void SetOp(string op)
        {
            _op = op;
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
            return "(" + (_left != null ? _left.ToString() : "") + " " + _op + " " + (_right != null ? _right.ToString() : "") + ")";
        }

        public virtual void Close()
        {
            if (_left != null)
            {
                if (_left is Scriptnode.ScriptNode leftNode)
                {
                    leftNode.Dispose();
                }
                else if (_left is StackEntry leftEntry)
                {
                    leftEntry.Close();
                }
                _left = null;
            }
            if (_right != null)
            {
                if (_right is Scriptnode.ScriptNode rightNode)
                {
                    rightNode.Dispose();
                }
                else if (_right is StackEntry rightEntry)
                {
                    rightEntry.Close();
                }
                _right = null;
            }
            if (_stackEntry != null)
            {
                _stackEntry.Close();
            }
            _stackEntry = null;
        }
    }
}





