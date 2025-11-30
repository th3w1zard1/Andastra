using TSLPatcher.Core.Formats.NCS.DeNCS.Node;
using NodeType = TSLPatcher.Core.Formats.NCS.DeNCS.Node.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Analysis
{
    public abstract class PrunedDepthFirstAdapter : AnalysisAdapter
    {
        public virtual void InStart(Start node)
        {
            DefaultIn(node);
        }

        public virtual void OutStart(Start node)
        {
            DefaultOut(node);
        }

        public virtual void CaseStart(Start node)
        {
            InStart(node);
            var pProgram = node.GetPProgram();
            if (pProgram != null)
            {
                pProgram.Apply(this);
            }
            OutStart(node);
        }

        public virtual void InEOF(EOF node)
        {
            DefaultIn(node);
        }

        public virtual void OutEOF(EOF node)
        {
            DefaultOut(node);
        }

        public virtual void CaseEOF(EOF node)
        {
            InEOF(node);
            OutEOF(node);
        }

        public virtual void InAProgram(AProgram node)
        {
            DefaultIn(node);
        }

        public virtual void OutAProgram(AProgram node)
        {
            DefaultOut(node);
        }

        public virtual void CaseAProgram(AProgram node)
        {
            InAProgram(node);
            if (node.GetSize() != null)
            {
                node.GetSize().Apply(this);
            }
            if (node.GetConditional() != null)
            {
                node.GetConditional().Apply(this);
            }
            if (node.GetJumpToSubroutine() != null)
            {
                node.GetJumpToSubroutine().Apply(this);
            }
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }
            foreach (var sub in node.GetSubroutine())
            {
                sub.Apply(this);
            }
            OutAProgram(node);
        }

        public virtual void InASubroutine(ASubroutine node)
        {
            DefaultIn(node);
        }

        public virtual void OutASubroutine(ASubroutine node)
        {
            DefaultOut(node);
        }

        public virtual void CaseASubroutine(ASubroutine node)
        {
            InASubroutine(node);
            if (node.GetCommandBlock() != null)
            {
                node.GetCommandBlock().Apply(this);
            }
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }
            OutASubroutine(node);
        }

        public virtual void InACommandBlock(ACommandBlock node)
        {
            DefaultIn(node);
        }

        public virtual void OutACommandBlock(ACommandBlock node)
        {
            DefaultOut(node);
        }

        public virtual void CaseACommandBlock(ACommandBlock node)
        {
            InACommandBlock(node);
            foreach (var cmd in node.GetCmd())
            {
                cmd.Apply(this);
            }
            OutACommandBlock(node);
        }

        public override void DefaultIn(NodeType node)
        {
            base.DefaultIn(node);
        }

        public override void DefaultOut(NodeType node)
        {
            base.DefaultOut(node);
        }

        // Virtual Case methods for command classes
        public virtual void CaseAReturnCmd(AReturnCmd node)
        {
            DefaultIn(node);
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAConstCmd(AConstCmd node)
        {
            DefaultIn(node);
            if (node.GetConstCommand() != null)
            {
                node.GetConstCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseACopydownspCmd(ACopydownspCmd node)
        {
            DefaultIn(node);
            if (node.GetCopyDownSpCommand() != null)
            {
                node.GetCopyDownSpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseACopytopspCmd(ACopytopspCmd node)
        {
            DefaultIn(node);
            if (node.GetCopyTopSpCommand() != null)
            {
                node.GetCopyTopSpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseACopydownbpCmd(ACopydownbpCmd node)
        {
            DefaultIn(node);
            if (node.GetCopyDownBpCommand() != null)
            {
                node.GetCopyDownBpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseACopytopbpCmd(ACopytopbpCmd node)
        {
            DefaultIn(node);
            if (node.GetCopyTopBpCommand() != null)
            {
                node.GetCopyTopBpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseACondJumpCmd(ACondJumpCmd node)
        {
            DefaultIn(node);
            if (node.GetConditionalJumpCommand() != null)
            {
                node.GetConditionalJumpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAJumpCmd(AJumpCmd node)
        {
            DefaultIn(node);
            if (node.GetJumpCommand() != null)
            {
                node.GetJumpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAJumpSubCmd(AJumpSubCmd node)
        {
            DefaultIn(node);
            if (node.GetJumpToSubroutine() != null)
            {
                node.GetJumpToSubroutine().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAMovespCmd(AMovespCmd node)
        {
            DefaultIn(node);
            if (node.GetMoveSpCommand() != null)
            {
                node.GetMoveSpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseALogiiCmd(ALogiiCmd node)
        {
            DefaultIn(node);
            if (node.GetLogiiCommand() != null)
            {
                node.GetLogiiCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAUnaryCmd(AUnaryCmd node)
        {
            DefaultIn(node);
            if (node.GetUnaryCommand() != null)
            {
                node.GetUnaryCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseABinaryCmd(ABinaryCmd node)
        {
            DefaultIn(node);
            if (node.GetBinaryCommand() != null)
            {
                node.GetBinaryCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseADestructCmd(ADestructCmd node)
        {
            DefaultIn(node);
            if (node.GetDestructCommand() != null)
            {
                node.GetDestructCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseABpCmd(ABpCmd node)
        {
            DefaultIn(node);
            if (node.GetBpCommand() != null)
            {
                node.GetBpCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAActionCmd(AActionCmd node)
        {
            DefaultIn(node);
            if (node.GetActionCommand() != null)
            {
                node.GetActionCommand().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAStoreStateCmd(AStoreStateCmd node)
        {
            DefaultIn(node);
            if (node.GetStoreStateCommand() != null)
            {
                node.GetStoreStateCommand().Apply(this);
            }
            DefaultOut(node);
        }

        // Virtual Case methods for command base classes
        public virtual void CaseAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            DefaultIn(node);
            if (node.GetJumpIf() != null)
            {
                node.GetJumpIf().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAJumpCommand(AJumpCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseAJumpToSubroutine(AJumpToSubroutine node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseAReturn(AReturn node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseACopyDownSpCommand(ACopyDownSpCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseACopyTopSpCommand(ACopyTopSpCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseACopyDownBpCommand(ACopyDownBpCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseACopyTopBpCommand(ACopyTopBpCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseAMoveSpCommand(AMoveSpCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseARsaddCommand(ARsaddCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseAConstCommand(AConstCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseAActionCommand(AActionCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseALogiiCommand(ALogiiCommand node)
        {
            DefaultIn(node);
            if (node.GetLogiiOp() != null)
            {
                node.GetLogiiOp().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseABinaryCommand(ABinaryCommand node)
        {
            DefaultIn(node);
            if (node.GetBinaryOp() != null)
            {
                node.GetBinaryOp().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAUnaryCommand(AUnaryCommand node)
        {
            DefaultIn(node);
            if (node.GetUnaryOp() != null)
            {
                node.GetUnaryOp().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseADestructCommand(ADestructCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }

        public virtual void CaseABpCommand(ABpCommand node)
        {
            DefaultIn(node);
            if (node.GetBpOp() != null)
            {
                node.GetBpOp().Apply(this);
            }
            DefaultOut(node);
        }

        public virtual void CaseAStoreStateCommand(AStoreStateCommand node)
        {
            DefaultIn(node);
            DefaultOut(node);
        }
    }
}

