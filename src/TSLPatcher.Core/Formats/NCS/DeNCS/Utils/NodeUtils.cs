using TSLPatcher.Core.Formats.NCS.DeNCS.Node;
using NodeType = TSLPatcher.Core.Formats.NCS.DeNCS.Node.Node;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public static class NodeUtils
    {
        public const int CMDSIZE_JUMP = 6;
        public const int CMDSIZE_RETN = 2;

        public static int GetCommandPos(NodeType node)
        {
            if (node is AConditionalJumpCommand cmd1)
            {
                return int.Parse(cmd1.GetPos().GetText());
            }
            if (node is AJumpCommand cmd2)
            {
                return int.Parse(cmd2.GetPos().GetText());
            }
            if (node is AJumpToSubroutine cmd3)
            {
                return int.Parse(cmd3.GetPos().GetText());
            }
            if (node is AReturn cmd4)
            {
                return int.Parse(cmd4.GetPos().GetText());
            }
            if (node is ACopyDownSpCommand cmd5)
            {
                return int.Parse(cmd5.GetPos().GetText());
            }
            if (node is ACopyTopSpCommand cmd6)
            {
                return int.Parse(cmd6.GetPos().GetText());
            }
            if (node is ACopyDownBpCommand cmd7)
            {
                return int.Parse(cmd7.GetPos().GetText());
            }
            if (node is ACopyTopBpCommand cmd8)
            {
                return int.Parse(cmd8.GetPos().GetText());
            }
            if (node is AMoveSpCommand cmd9)
            {
                return int.Parse(cmd9.GetPos().GetText());
            }
            if (node is ARsaddCommand cmd10)
            {
                return int.Parse(cmd10.GetPos().GetText());
            }
            if (node is AConstCommand cmd11)
            {
                return int.Parse(cmd11.GetPos().GetText());
            }
            if (node is AActionCommand cmd12)
            {
                return int.Parse(cmd12.GetPos().GetText());
            }
            if (node is ALogiiCommand cmd13)
            {
                return int.Parse(cmd13.GetPos().GetText());
            }
            if (node is ABinaryCommand cmd14)
            {
                return int.Parse(cmd14.GetPos().GetText());
            }
            if (node is AUnaryCommand cmd15)
            {
                return int.Parse(cmd15.GetPos().GetText());
            }
            if (node is ADestructCommand cmd17)
            {
                return int.Parse(cmd17.GetPos().GetText());
            }
            if (node is ABpCommand cmd18)
            {
                return int.Parse(cmd18.GetPos().GetText());
            }
            if (node is AStoreStateCommand cmd19)
            {
                return int.Parse(cmd19.GetPos().GetText());
            }
            return -1;
        }

        public static bool IsCommandNode(NodeType node)
        {
            return node is AConditionalJumpCommand ||
                   node is AJumpCommand ||
                   node is AJumpToSubroutine ||
                   node is AReturn ||
                   node is ACopyDownSpCommand ||
                   node is ACopyTopSpCommand ||
                   node is ACopyDownBpCommand ||
                   node is ACopyTopBpCommand ||
                   node is AMoveSpCommand ||
                   node is ARsaddCommand ||
                   node is AConstCommand ||
                   node is AActionCommand ||
                   node is ALogiiCommand ||
                   node is ABinaryCommand ||
                   node is AUnaryCommand ||
                   node is ADestructCommand ||
                   node is ABpCommand ||
                   node is AStoreStateCommand;
        }

        public static bool IsJz(NodeType node)
        {
            return node is AConditionalJumpCommand cmd && cmd.GetJumpIf() is AZeroJumpIf;
        }

        public static int GetSubEnd(ASubroutine sub)
        {
            return GetCommandPos(sub.GetReturn());
        }

        public static bool IsConditionalProgram(Start ast)
        {
            if (ast == null)
            {
                return false;
            }
            var program = ast.GetPProgram();
            if (program == null)
            {
                return false;
            }
            if (program is AProgram aProg)
            {
                return aProg.GetConditional() != null;
            }
            return false;
        }
    }
}

