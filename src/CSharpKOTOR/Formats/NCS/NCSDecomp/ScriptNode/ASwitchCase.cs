using System.Collections.Generic;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class ASwitchCase : ScriptRootNode
    {
        private AConst _val;
        private int _end;

        public ASwitchCase(int start) : this(start, null)
        {
        }

        public ASwitchCase(int start, AConst val) : base(start, -1)
        {
            _end = -1;
            if (val != null)
            {
                SetVal(val);
            }
        }

        public void SetEnd(int end)
        {
            _end = end;
        }

        public override int GetEnd()
        {
            return _end;
        }

        public AConst GetVal()
        {
            return _val;
        }

        public void SetVal(AConst val)
        {
            if (_val != null)
            {
                ((AExpression)_val).Parent(null);
            }
            if (val != null)
            {
                ((AExpression)val).Parent((Scriptnode.ScriptNode)(object)this);
            }
            _val = val;
        }

        public List<AUnkLoopControl> GetUnknowns()
        {
            var unks = new List<AUnkLoopControl>();
            foreach (var node in GetChildren())
            {
                if (node is AUnkLoopControl unk)
                {
                    unks.Add(unk);
                }
            }
            return unks;
        }

        public void ReplaceUnknown(AUnkLoopControl unk, Scriptnode.ScriptNode newNode)
        {
            newNode.Parent((Scriptnode.ScriptNode)(object)this);
            var children = GetChildren();
            int index = -1;
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == unk)
                {
                    index = i;
                    break;
                }
            }
            if (index >= 0)
            {
                children[index] = newNode;
                ((Scriptnode.ScriptNode)(object)unk).Parent(null);
            }
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            var tabs = GetTabsString();
            var newline = GetNewlineString();
            if (_val == null)
            {
                buff.Append(tabs + "default:" + newline);
            }
            else
            {
                buff.Append(tabs + "case " + _val.ToString() + ":" + newline);
            }
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            return buff.ToString();
        }

        private string GetTabsString()
        {
            int depth = 0;
            Scriptnode.ScriptNode node = this;
            while (node.Parent() != null)
            {
                depth++;
                node = node.Parent();
            }
            return new string('\t', depth);
        }

        private string GetNewlineString()
        {
            return System.Environment.NewLine;
        }

        public virtual void Close()
        {
            if (_val != null)
            {
                _val.Close();
            }
            _val = null;
        }
    }
}





