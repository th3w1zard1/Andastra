using System.Collections.Generic;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Node
{
    public sealed class AProgram : PProgram
    {
        private PSize _size;
        private PRsaddCommand _conditional;
        private PJumpToSubroutine _jumpToSubroutine;
        private PReturn _return;
        private List<PSubroutine> _subroutine;

        public AProgram()
        {
            _subroutine = new List<PSubroutine>();
        }

        public AProgram(PSize size, PRsaddCommand conditional, PJumpToSubroutine jumpToSubroutine, PReturn returnNode, List<PSubroutine> subroutine)
        {
            _subroutine = new List<PSubroutine>();
            SetSize(size);
            SetConditional(conditional);
            SetJumpToSubroutine(jumpToSubroutine);
            SetReturn(returnNode);
            if (subroutine != null)
            {
                _subroutine.AddRange(subroutine);
            }
        }

        public override object Clone()
        {
            return new AProgram(
                (PSize)CloneNode(_size),
                (PRsaddCommand)CloneNode(_conditional),
                (PJumpToSubroutine)CloneNode(_jumpToSubroutine),
                (PReturn)CloneNode(_return),
                CloneList(_subroutine));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            if (sw is Analysis.PrunedDepthFirstAdapter adapter)
            {
                adapter.CaseAProgram(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PSize GetSize()
        {
            return _size;
        }

        public void SetSize(PSize node)
        {
            if (_size != null)
            {
                _size.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _size = node;
        }

        public PRsaddCommand GetConditional()
        {
            return _conditional;
        }

        public void SetConditional(PRsaddCommand node)
        {
            if (_conditional != null)
            {
                _conditional.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _conditional = node;
        }

        public PJumpToSubroutine GetJumpToSubroutine()
        {
            return _jumpToSubroutine;
        }

        public void SetJumpToSubroutine(PJumpToSubroutine node)
        {
            if (_jumpToSubroutine != null)
            {
                _jumpToSubroutine.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _jumpToSubroutine = node;
        }

        public PReturn GetReturn()
        {
            return _return;
        }

        public void SetReturn(PReturn node)
        {
            if (_return != null)
            {
                _return.SetParent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.SetParent(this);
            }
            _return = node;
        }

        public List<PSubroutine> GetSubroutine()
        {
            return _subroutine;
        }

        public void SetSubroutine(List<PSubroutine> subroutine)
        {
            _subroutine = subroutine ?? new List<PSubroutine>();
        }

        public void AddSubroutine(PSubroutine sub)
        {
            if (sub.Parent() != null)
            {
                sub.Parent().RemoveChild(sub);
            }
            sub.SetParent(this);
            _subroutine.Add(sub);
        }

        public override void RemoveChild(Node child)
        {
            if (_size == child)
            {
                _size = null;
                return;
            }
            if (_conditional == child)
            {
                _conditional = null;
                return;
            }
            if (_jumpToSubroutine == child)
            {
                _jumpToSubroutine = null;
                return;
            }
            if (_return == child)
            {
                _return = null;
                return;
            }
            _subroutine.Remove((PSubroutine)child);
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_size == oldChild)
            {
                SetSize((PSize)newChild);
                return;
            }
            if (_conditional == oldChild)
            {
                SetConditional((PRsaddCommand)newChild);
                return;
            }
            if (_jumpToSubroutine == oldChild)
            {
                SetJumpToSubroutine((PJumpToSubroutine)newChild);
                return;
            }
            if (_return == oldChild)
            {
                SetReturn((PReturn)newChild);
                return;
            }
            int index = _subroutine.IndexOf((PSubroutine)oldChild);
            if (index >= 0)
            {
                if (newChild != null)
                {
                    _subroutine[index] = (PSubroutine)newChild;
                    if (newChild.Parent() != null)
                    {
                        newChild.Parent().RemoveChild(newChild);
                    }
                    newChild.SetParent(this);
                }
                else
                {
                    _subroutine.RemoveAt(index);
                }
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(ToString(_size));
            sb.Append(ToString(_conditional));
            sb.Append(ToString(_jumpToSubroutine));
            sb.Append(ToString(_return));
            foreach (var sub in _subroutine)
            {
                sb.Append(sub != null ? sub.ToString() : "");
            }
            return sb.ToString();
        }
    }
}

