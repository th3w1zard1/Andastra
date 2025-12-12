namespace CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode
{
    public class AUnkLoopControl : ScriptNode
    {
        private int _dest;

        public AUnkLoopControl(int dest)
        {
            _dest = dest;
        }

        public int GetDestination()
        {
            return _dest;
        }

        public void SetDestination(int dest)
        {
            _dest = dest;
        }

        public override string ToString()
        {
            return GetTabs() + "BREAK or CONTINUE undetermined" + GetNewline();
        }
    }
}





