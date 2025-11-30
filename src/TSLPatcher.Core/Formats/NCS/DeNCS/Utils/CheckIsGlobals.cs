using TSLPatcher.Core.Formats.NCS.DeNCS.Analysis;
using TSLPatcher.Core.Formats.NCS.DeNCS.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public class CheckIsGlobals : PrunedReversedDepthFirstAdapter
    {
        private bool isGlobals;

        public CheckIsGlobals()
        {
            isGlobals = false;
        }

        public virtual void InABpCommand(ABpCommand node)
        {
            isGlobals = true;
        }

        public virtual void CaseACommandBlock(ACommandBlock node)
        {
            InACommandBlock(node);
            var cmds = node.GetCmd();
            for (int i = cmds.Count - 1; i >= 0; i--)
            {
                cmds[i].Apply(this);
                if (isGlobals)
                {
                    return;
                }
            }
            OutACommandBlock(node);
        }

        public bool GetIsGlobals()
        {
            return isGlobals;
        }
    }
}

