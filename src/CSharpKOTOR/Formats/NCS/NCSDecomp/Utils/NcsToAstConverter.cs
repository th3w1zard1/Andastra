using System;
using System.Collections.Generic;
using System.Globalization;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    /*
    NCS to AST Converter - Comprehensive instruction conversion.

    This module provides comprehensive conversion of NCS (NWScript Compiled Script) bytecode
    instructions directly to NCSDecomp AST (Abstract Syntax Tree) format, bypassing the traditional
    Decoder -> Lexer -> Parser chain for improved performance and accuracy.

    The converter handles all NCS instruction types comprehensively:
    - Constants: CONSTI, CONSTF, CONSTS, CONSTO
    - Control flow: JMP, JSR, JZ, JNZ, RETN
    - Stack operations: CPDOWNSP, CPTOPSP, CPDOWNBP, CPTOPBP, MOVSP, INCxSP, DECxSP, INCxBP, DECxBP
    - RSADD variants: RSADDI, RSADDF, RSADDS, RSADDO, RSADDEFF, RSADDEVT, RSADDLOC, RSADDTAL
    - Function calls: ACTION
    - Stack management: SAVEBP, RESTOREBP, STORE_STATE, DESTRUCT
    - Arithmetic: ADDxx, SUBxx, MULxx, DIVxx, MODxx, NEGx
    - Comparison: EQUALxx, NEQUALxx, GTxx, GEQxx, LTxx, LEQxx
    - Logical: LOGANDxx, LOGORxx, NOTx
    - Bitwise: BOOLANDxx, INCORxx, EXCORxx, SHLEFTxx, SHRIGHTxx, USHRIGHTxx, COMPx
    - No-ops: NOP, NOP2, RESERVED (typically skipped during conversion)

    References:
    ----------
        vendor/reone/src/libs/script/format/ncsreader.cpp - NCS instruction reading
        vendor/xoreos/src/aurora/nwscript/ncsfile.cpp - NCS instruction execution
        NCSDecomp - Original NCS decompiler implementation
    */
    public static class NcsToAstConverter
    {
        public static Start ConvertNcsToAst(NCS ncs)
        {
            AProgram program = new AProgram();
            List<NCSInstruction> instructions = ncs != null ? ncs.Instructions : null;
            if (instructions == null || instructions.Count == 0)
            {
                return new Start(program, new EOF());
            }

            HashSet<int> subroutineStarts = new HashSet<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                NCSInstruction inst = instructions[i];
                if (inst.InsType == NCSInstructionType.JSR && inst.Jump != null)
                {
                    try
                    {
                        int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                        if (jumpIdx >= 0)
                        {
                            subroutineStarts.Add(jumpIdx);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            int mainEnd = instructions.Count;
            if (subroutineStarts.Count > 0)
            {
                int min = int.MaxValue;
                foreach (int start in subroutineStarts)
                {
                    if (start < min)
                    {
                        min = start;
                    }
                }

                mainEnd = min;
            }

            ASubroutine mainSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, mainEnd, 0);
            if (mainSub != null)
            {
                program.GetSubroutine().AddLast(mainSub);
            }

            List<int> sortedStarts = new List<int>(subroutineStarts);
            sortedStarts.Sort();
            for (int idx = 0; idx < sortedStarts.Count; idx++)
            {
                int subStart = sortedStarts[idx];
                int subEnd = instructions.Count;
                for (int i = subStart + 1; i < instructions.Count; i++)
                {
                    if (subroutineStarts.Contains(i))
                    {
                        subEnd = i;
                        break;
                    }

                    if (instructions[i].InsType == NCSInstructionType.RETN)
                    {
                        subEnd = i + 1;
                        break;
                    }
                }

                ASubroutine sub = ConvertInstructionRangeToSubroutine(
                    ncs,
                    instructions,
                    subStart,
                    subEnd,
                    program.GetSubroutine().Count);
                if (sub != null)
                {
                    program.GetSubroutine().AddLast(sub);
                }
            }

            return new Start(program, new EOF());
        }

        private static ASubroutine ConvertInstructionRangeToSubroutine(
            NCS ncs,
            List<NCSInstruction> instructions,
            int startIdx,
            int endIdx,
            int subId)
        {
            if (startIdx >= endIdx || startIdx >= instructions.Count)
            {
                return null;
            }

            ASubroutine sub = new ASubroutine();
            sub.SetId(subId);
            ACommandBlock cmdBlock = new ACommandBlock();

            int limit = Math.Min(endIdx, instructions.Count);
            for (int i = startIdx; i < limit; i++)
            {
                PCmd cmd = ConvertInstructionToCmd(ncs, instructions[i], i, instructions);
                if (cmd != null)
                {
                    cmdBlock.GetCmd().Add(cmd);
                }
            }

            sub.SetCommandBlock(cmdBlock);

            for (int i = startIdx; i < limit; i++)
            {
                if (instructions[i].InsType == NCSInstructionType.RETN)
                {
                    AReturn ret = ConvertRetn(instructions[i], i);
                    if (ret != null)
                    {
                        sub.SetReturn(ret);
                    }

                    break;
                }
            }

            return sub;
        }

        private static PCmd ConvertInstructionToCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            if (insType == NCSInstructionType.CONSTI ||
                insType == NCSInstructionType.CONSTF ||
                insType == NCSInstructionType.CONSTS ||
                insType == NCSInstructionType.CONSTO)
            {
                return ConvertConstCmd(inst, pos);
            }

            if (insType == NCSInstructionType.ACTION)
            {
                return ConvertActionCmd(inst, pos);
            }

            if (insType == NCSInstructionType.JMP || insType == NCSInstructionType.JSR)
            {
                return ConvertJumpCmd(ncs, inst, pos, instructions);
            }

            if (insType == NCSInstructionType.JZ || insType == NCSInstructionType.JNZ)
            {
                return ConvertConditionalJumpCmd(ncs, inst, pos, instructions);
            }

            if (insType == NCSInstructionType.RETN)
            {
                return ConvertRetnCmd(inst, pos);
            }

            if (insType == NCSInstructionType.CPDOWNSP || insType == NCSInstructionType.CPTOPSP)
            {
                return ConvertCopySpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.CPDOWNBP || insType == NCSInstructionType.CPTOPBP)
            {
                return ConvertCopyBpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.MOVSP)
            {
                return ConvertMovespCmd(inst, pos);
            }

            if (insType == NCSInstructionType.INCxSP ||
                insType == NCSInstructionType.DECxSP ||
                insType == NCSInstructionType.INCxBP ||
                insType == NCSInstructionType.DECxBP)
            {
                return ConvertStackOpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.RSADDI ||
                insType == NCSInstructionType.RSADDF ||
                insType == NCSInstructionType.RSADDS ||
                insType == NCSInstructionType.RSADDO ||
                insType == NCSInstructionType.RSADDEFF ||
                insType == NCSInstructionType.RSADDEVT ||
                insType == NCSInstructionType.RSADDLOC ||
                insType == NCSInstructionType.RSADDTAL)
            {
                return ConvertRsaddCmd(inst, pos);
            }

            if (insType == NCSInstructionType.DESTRUCT)
            {
                return ConvertDestructCmd(inst, pos);
            }

            if (insType == NCSInstructionType.SAVEBP || insType == NCSInstructionType.RESTOREBP)
            {
                return ConvertBpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.STORE_STATE)
            {
                return ConvertStoreStateCmd(inst, pos);
            }

            if (insType == NCSInstructionType.NOP ||
                insType == NCSInstructionType.NOP2 ||
                insType == NCSInstructionType.RESERVED ||
                insType == NCSInstructionType.RESERVED_01)
            {
                return null;
            }

            if (insType == NCSInstructionType.NEGI ||
                insType == NCSInstructionType.NEGF ||
                insType == NCSInstructionType.NOTI ||
                insType == NCSInstructionType.COMPI)
            {
                return ConvertUnaryCmd(inst, pos);
            }

            if (inst.IsArithmetic() || inst.IsComparison() || inst.IsBitwise())
            {
                return ConvertBinaryCmd(inst, pos);
            }

            if (inst.IsLogical() ||
                insType == NCSInstructionType.BOOLANDII ||
                insType == NCSInstructionType.INCORII ||
                insType == NCSInstructionType.EXCORII)
            {
                return ConvertLogiiCmd(inst, pos);
            }

            System.Diagnostics.Debug.WriteLine(
                "Unknown instruction type at position " + pos + ": " + insType +
                " (value: " + insType + ")");
            return null;
        }

        private static AConstCmd ConvertConstCmd(NCSInstruction inst, int pos)
        {
            AConstCmd constCmd = new AConstCmd();
            AConstCommand constCommand = new AConstCommand();

            constCommand.SetConst(new TConst(pos, 0));
            constCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            constCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            if (inst.Args != null && inst.Args.Count > 0)
            {
                if (inst.InsType == NCSInstructionType.CONSTI)
                {
                    int intVal = GetArgAsInt(inst, 0);
                    AIntConstant constConstant = new AIntConstant();
                    constConstant.SetIntegerConstant(new TIntegerConstant(Convert.ToString(intVal, CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTF)
                {
                    double floatVal = GetArgAsDouble(inst, 0);
                    AFloatConstant constConstant = new AFloatConstant();
                    constConstant.SetFloatConstant(new TFloatConstant(floatVal.ToString(CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTS)
                {
                    string strVal = GetArgAsString(inst, 0, "");
                    AStringConstant constConstant = new AStringConstant();
                    constConstant.SetStringLiteral(new TStringLiteral("\"" + strVal + "\"", pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTO)
                {
                    int objVal = GetArgAsInt(inst, 0);
                    AIntConstant constConstant = new AIntConstant();
                    constConstant.SetIntegerConstant(new TIntegerConstant(Convert.ToString(objVal, CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
            }

            constCommand.SetSemi(new TSemi(pos, 0));
            constCmd.SetConstCommand(constCommand);

            return constCmd;
        }

        private static AActionCmd ConvertActionCmd(NCSInstruction inst, int pos)
        {
            AActionCmd actionCmd = new AActionCmd();
            AActionCommand actionCommand = new AActionCommand();

            actionCommand.SetAction(new TAction(pos, 0));
            actionCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            actionCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            int idVal = 0;
            int argCountVal = 0;
            if (inst.Args != null && inst.Args.Count >= 1)
            {
                idVal = GetArgAsInt(inst, 0);
                if (inst.Args.Count > 1)
                {
                    argCountVal = GetArgAsInt(inst, 1);
                }
            }

            actionCommand.SetId(new TIntegerConstant(Convert.ToString(idVal, CultureInfo.InvariantCulture), pos, 0));
            actionCommand.SetArgCount(new TIntegerConstant(Convert.ToString(argCountVal, CultureInfo.InvariantCulture), pos, 0));
            actionCommand.SetSemi(new TSemi(pos, 0));

            actionCmd.SetActionCommand(actionCommand);
            return actionCmd;
        }

        private static PCmd ConvertJumpCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);

            int offset = 0;
            if (inst.Jump != null)
            {
                try
                {
                    int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                    if (jumpIdx >= 0)
                    {
                        offset = jumpIdx - pos;
                    }
                }
                catch (Exception)
                {
                    offset = 0;
                }
            }

            if (insType == NCSInstructionType.JSR)
            {
                AJumpSubCmd jsrCmd = new AJumpSubCmd();
                AJumpToSubroutine jsrToSub = new AJumpToSubroutine();

                jsrToSub.SetJsr(new TJsr(pos, 0));
                jsrToSub.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetSemi(new TSemi(pos, 0));

                jsrCmd.SetJumpToSubroutine(jsrToSub);
                return jsrCmd;
            }

            AJumpCmd jumpCmd = new AJumpCmd();
            AJumpCommand jumpCommand = new AJumpCommand();

            jumpCommand.SetJmp(new TJmp(pos, 0));
            jumpCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetSemi(new TSemi(pos, 0));

            jumpCmd.SetJumpCommand(jumpCommand);
            return jumpCmd;
        }

        private static PCmd ConvertConditionalJumpCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);

            int offset = 0;
            if (inst.Jump != null)
            {
                try
                {
                    int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                    if (jumpIdx >= 0)
                    {
                        offset = jumpIdx - pos;
                    }
                }
                catch (Exception)
                {
                    offset = 0;
                }
            }

            ACondJumpCmd condJumpCmd = new ACondJumpCmd();
            AConditionalJumpCommand condJumpCommand = new AConditionalJumpCommand();

            if (insType == NCSInstructionType.JZ)
            {
                AZeroJumpIf zeroJumpIf = new AZeroJumpIf();
                zeroJumpIf.SetJz(new TJz(pos, 0));
                condJumpCommand.SetJumpIf(zeroJumpIf);
            }
            else
            {
                ANonzeroJumpIf nonzeroJumpIf = new ANonzeroJumpIf();
                nonzeroJumpIf.SetJnz(new TJnz(pos, 0));
                condJumpCommand.SetJumpIf(nonzeroJumpIf);
            }

            condJumpCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetSemi(new TSemi(pos, 0));

            condJumpCmd.SetConditionalJumpCommand(condJumpCommand);
            return condJumpCmd;
        }

        private static AReturn ConvertRetn(NCSInstruction inst, int pos)
        {
            AReturn ret = new AReturn();
            ret.SetRetn(new TRetn(pos, 0));
            ret.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            ret.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            ret.SetSemi(new TSemi(pos, 0));

            return ret;
        }

        private static AReturnCmd ConvertRetnCmd(NCSInstruction inst, int pos)
        {
            AReturnCmd retnCmd = new AReturnCmd();
            AReturn retn = ConvertRetn(inst, pos);
            retnCmd.SetReturn(retn);
            return retnCmd;
        }

        private static PCmd ConvertCopySpCmd(NCSInstruction inst, int pos)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);
            int offset = GetArgAsInt(inst, 0);
            int size = GetArgAsInt(inst, 1);

            if (insType == NCSInstructionType.CPDOWNSP)
            {
                ACopydownspCmd cmd = new ACopydownspCmd();
                ACopyDownSpCommand command = new ACopyDownSpCommand();
                command.SetCpdownsp(new TCpdownsp(pos, 0));
                command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                command.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
                command.SetSemi(new TSemi(pos, 0));
                cmd.SetCopyDownSpCommand(command);
                return cmd;
            }

            ACopytopspCmd topCmd = new ACopytopspCmd();
            ACopyTopSpCommand topCommand = new ACopyTopSpCommand();
            topCommand.SetCptopsp(new TCptopsp(pos, 0));
            topCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSemi(new TSemi(pos, 0));
            topCmd.SetCopyTopSpCommand(topCommand);
            return topCmd;
        }

        private static PCmd ConvertCopyBpCmd(NCSInstruction inst, int pos)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);
            int offset = GetArgAsInt(inst, 0);
            int size = GetArgAsInt(inst, 1);

            if (insType == NCSInstructionType.CPDOWNBP)
            {
                ACopydownbpCmd cmd = new ACopydownbpCmd();
                ACopyDownBpCommand command = new ACopyDownBpCommand();
                command.SetCpdownbp(new TCpdownbp(pos, 0));
                command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                command.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
                command.SetSemi(new TSemi(pos, 0));
                cmd.SetCopyDownBpCommand(command);
                return cmd;
            }

            ACopytopbpCmd topCmd = new ACopytopbpCmd();
            ACopyTopBpCommand topCommand = new ACopyTopBpCommand();
            topCommand.SetCptopbp(new TCptopbp(pos, 0));
            topCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSemi(new TSemi(pos, 0));
            topCmd.SetCopyTopBpCommand(topCommand);
            return topCmd;
        }

        private static PCmd ConvertMovespCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int offset = GetArgAsInt(inst, 0);

            AMovespCmd cmd = new AMovespCmd();
            AMoveSpCommand command = new AMoveSpCommand();
            command.SetMovsp(new TMovsp(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetMoveSpCommand(command);

            return cmd;
        }

        private static PCmd ConvertRsaddCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);

            AST.ARsaddCmd cmd = new AST.ARsaddCmd();
            AST.ARsaddCommand command = new AST.ARsaddCommand();
            command.SetRsadd(new AST.TRsadd(pos, 0));
            command.SetPos(new AST.TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new AST.TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new AST.TSemi(pos, 0));
            cmd.SetRsaddCommand(command);

            return cmd;
        }

        private static PCmd ConvertStackOpCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int offset = GetArgAsInt(inst, 0);

            PCmd stackOpCmd = null;
            PStackOp stackOp = null;

            if (inst.InsType == NCSInstructionType.INCxSP)
            {
                stackOp = new AIncispStackOp();
                ((AIncispStackOp)stackOp).SetIncisp(new TIncisp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.DECxSP)
            {
                stackOp = new ADecispStackOp();
                ((ADecispStackOp)stackOp).SetDecisp(new TDecisp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.INCxBP)
            {
                stackOp = new AIncibpStackOp();
                ((AIncibpStackOp)stackOp).SetIncibp(new TIncibp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.DECxBP)
            {
                stackOp = new ADecibpStackOp();
                ((ADecibpStackOp)stackOp).SetDecibp(new TDecibp(pos, 0));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "Unexpected instruction type in _convert_stack_op_cmd: " + inst.InsType);
                return null;
            }

            AStackCommand stackCommand = new AStackCommand();
            stackCommand.SetStackOp(stackOp);
            stackCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetSemi(new TSemi(pos, 0));

            AStackOpCmd cmd = new AStackOpCmd();
            cmd.SetStackCommand(stackCommand);
            stackOpCmd = cmd;

            return stackOpCmd;
        }

        private static PCmd ConvertDestructCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int sizeRem = GetArgAsInt(inst, 0);
            int offset = GetArgAsInt(inst, 1);
            int sizeSave = GetArgAsInt(inst, 2);

            ADestructCmd cmd = new ADestructCmd();
            ADestructCommand command = new ADestructCommand();
            command.SetDestruct(new TDestruct(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeRem(new TIntegerConstant(Convert.ToString(sizeRem, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeSave(new TIntegerConstant(Convert.ToString(sizeSave, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetDestructCommand(command);

            return cmd;
        }

        private static PCmd ConvertBpCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);

            ABpCmd cmd = new ABpCmd();
            ABpCommand command = new ABpCommand();

            if (inst.InsType == NCSInstructionType.SAVEBP)
            {
                ASavebpBpOp bpOp = new ASavebpBpOp();
                bpOp.SetSavebp(new TSavebp(pos, 0));
                command.SetBpOp(bpOp);
            }
            else
            {
                ARestorebpBpOp bpOp = new ARestorebpBpOp();
                bpOp.SetRestorebp(new TRestorebp(pos, 0));
                command.SetBpOp(bpOp);
            }

            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetBpCommand(command);

            return cmd;
        }

        private static PCmd ConvertStoreStateCmd(NCSInstruction inst, int pos)
        {
            int offset = GetArgAsInt(inst, 0);
            int sizeBp = inst.Args != null && inst.Args.Count > 1 ? GetArgAsInt(inst, 1) : 0;
            int sizeSp = inst.Args != null && inst.Args.Count > 2 ? GetArgAsInt(inst, 2) : 0;

            AStoreStateCmd cmd = new AStoreStateCmd();
            AStoreStateCommand command = new AStoreStateCommand();
            command.SetStorestate(new TStorestate(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeBp(new TIntegerConstant(Convert.ToString(sizeBp, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeSp(new TIntegerConstant(Convert.ToString(sizeSp, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetStoreStateCommand(command);

            return cmd;
        }

        private static PCmd ConvertBinaryCmd(NCSInstruction inst, int pos)
        {
            ABinaryCmd cmd = new ABinaryCmd();
            ABinaryCommand command = new ABinaryCommand();

            command.SetBinaryOp(CreateBinaryOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            int resultSize = 1;
            command.SetSize(new TIntegerConstant(Convert.ToString(resultSize, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetBinaryCommand(command);

            return cmd;
        }

        private static PCmd ConvertUnaryCmd(NCSInstruction inst, int pos)
        {
            AUnaryCmd cmd = new AUnaryCmd();
            AUnaryCommand command = new AUnaryCommand();

            command.SetUnaryOp(CreateUnaryOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetUnaryCommand(command);

            return cmd;
        }

        private static PCmd ConvertLogiiCmd(NCSInstruction inst, int pos)
        {
            ALogiiCmd cmd = new ALogiiCmd();
            ALogiiCommand command = new ALogiiCommand();

            command.SetLogiiOp(CreateLogiiOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetLogiiCommand(command);

            return cmd;
        }

        private static PBinaryOp CreateBinaryOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.USHRIGHTII)
            {
                return new AUnrightBinaryOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("ADD"))
            {
                return new AAddBinaryOp();
            }

            if (insName.StartsWith("SUB"))
            {
                return new ASubBinaryOp();
            }

            if (insName.StartsWith("MUL"))
            {
                return new AMulBinaryOp();
            }

            if (insName.StartsWith("DIV"))
            {
                return new ADivBinaryOp();
            }

            if (insName.StartsWith("MOD"))
            {
                return new AModBinaryOp();
            }

            if (insName.StartsWith("EQUAL"))
            {
                return new AEqualBinaryOp();
            }

            if (insName.StartsWith("NEQUAL"))
            {
                return new ANequalBinaryOp();
            }

            if (insName.StartsWith("GT"))
            {
                return new AGtBinaryOp();
            }

            if (insName.StartsWith("LT"))
            {
                return new ALtBinaryOp();
            }

            if (insName.StartsWith("GEQ"))
            {
                return new AGeqBinaryOp();
            }

            if (insName.StartsWith("LEQ"))
            {
                return new ALeqBinaryOp();
            }

            if (insName.StartsWith("SHLEFT"))
            {
                return new AShleftBinaryOp();
            }

            if (insName.StartsWith("SHRIGHT"))
            {
                return new AShrightBinaryOp();
            }

            if (insName.StartsWith("USHRIGHT"))
            {
                return new AUnrightBinaryOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown binary operator: " + insType + ", creating placeholder");
            return new PlaceholderBinaryOp();
        }

        private static PUnaryOp CreateUnaryOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.NEGI || insType == NCSInstructionType.NEGF)
            {
                return new ANegUnaryOp();
            }

            if (insType == NCSInstructionType.NOTI)
            {
                return new ANotUnaryOp();
            }

            if (insType == NCSInstructionType.COMPI)
            {
                return new ACompUnaryOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("NEG"))
            {
                return new ANegUnaryOp();
            }

            if (insName.StartsWith("NOT"))
            {
                return new ANotUnaryOp();
            }

            if (insName.StartsWith("COMP"))
            {
                return new ACompUnaryOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown unary operator: " + insType + ", creating placeholder");
            return new PlaceholderUnaryOp();
        }

        private static PLogiiOp CreateLogiiOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.LOGANDII)
            {
                return new AAndLogiiOp();
            }

            if (insType == NCSInstructionType.LOGORII)
            {
                return new AOrLogiiOp();
            }

            if (insType == NCSInstructionType.BOOLANDII)
            {
                return new ABitAndLogiiOp();
            }

            if (insType == NCSInstructionType.EXCORII)
            {
                return new AExclOrLogiiOp();
            }

            if (insType == NCSInstructionType.INCORII)
            {
                return new AInclOrLogiiOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("LOGAND"))
            {
                return new AAndLogiiOp();
            }

            if (insName.StartsWith("LOGOR"))
            {
                return new AOrLogiiOp();
            }

            if (insName.StartsWith("BOOLAND"))
            {
                return new ABitAndLogiiOp();
            }

            if (insName.StartsWith("EXCOR"))
            {
                return new AExclOrLogiiOp();
            }

            if (insName.StartsWith("INCOR"))
            {
                return new AInclOrLogiiOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown logical operator: " + insType + ", creating placeholder");
            return new PlaceholderLogiiOp();
        }

        private static int GetQualifier(NCSInstructionType insType)
        {
            return insType.GetValue().Qualifier;
        }

        private static int GetArgAsInt(NCSInstruction inst, int index)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                object value = inst.Args[index];
                if (value is uint uintVal)
                {
                    return unchecked((int)uintVal);
                }

                if (value is long longVal)
                {
                    return unchecked((int)longVal);
                }

                if (value is IConvertible convertible)
                {
                    return convertible.ToInt32(CultureInfo.InvariantCulture);
                }

                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            return 0;
        }

        private static double GetArgAsDouble(NCSInstruction inst, int index)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                return Convert.ToDouble(inst.Args[index], CultureInfo.InvariantCulture);
            }

            return 0.0;
        }

        private static string GetArgAsString(NCSInstruction inst, int index, string defaultValue)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                return Convert.ToString(inst.Args[index], CultureInfo.InvariantCulture) ?? defaultValue;
            }

            return defaultValue;
        }

        private class PlaceholderBinaryOp : PBinaryOp
        {
            public override object Clone()
            {
                return new PlaceholderBinaryOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }

        private class PlaceholderUnaryOp : PUnaryOp
        {
            public override object Clone()
            {
                return new PlaceholderUnaryOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }

        private class PlaceholderLogiiOp : PLogiiOp
        {
            public override object Clone()
            {
                return new PlaceholderLogiiOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }
    }
}





