using System.Collections.Generic;
using System.Text;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.ScriptNode
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

        public int GetEnd()
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
                _val.SetParent(null);
            }
            if (val != null)
            {
                val.SetParent(this);
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

        public void ReplaceUnknown(AUnkLoopControl unk, ScriptNode newNode)
        {
            newNode.SetParent(this);
            var children = GetChildren();
            int index = children.IndexOf(unk);
            if (index >= 0)
            {
                children[index] = newNode;
                unk.SetParent(null);
            }
        }

        public override string ToString()
        {
            var buff = new StringBuilder();
            if (_val == null)
            {
                buff.Append(GetTabs() + "default:" + GetNewline());
            }
            else
            {
                buff.Append(GetTabs() + "case " + _val.ToString() + ":" + GetNewline());
            }
            foreach (var child in GetChildren())
            {
                buff.Append(child.ToString());
            }
            return buff.ToString();
        }

        public override void Close()
        {
            base.Close();
            if (_val != null)
            {
                _val.Close();
            }
            _val = null;
        }
    }
}

