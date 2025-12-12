// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Analysis;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    public class DoTypes : PrunedDepthFirstAdapter
    {
        private SubroutineState state;
        protected LocalTypeStack stack;
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private ActionsData actions;
        private bool initialproto;
        private bool protoskipping;
        private bool protoreturn;
        private bool skipdeadcode;
        private LocalTypeStack backupstack;
        // Debug mode - set to true for verbose stack tracing
        private static bool DEBUG_STACK = JavaSystem.GetProperty("NCSDecomp.debug.stack") == "true";
        // Debug mode - set to true for verbose stack tracing
        private int instructionCount = 0;
        // Debug mode - set to true for verbose stack tracing
        private void DebugLog(string operation, string details, int sizeBefore, int sizeAfter)
        {
            if (DEBUG_STACK)
            {
                JavaSystem.@out.Println(String.Format("[DoTypes #%d] %s: %s | stack: %d -> %d (delta: %+d)", instructionCount, operation, details, sizeBefore, sizeAfter, sizeAfter - sizeBefore));
            }
        }

        // Debug mode - set to true for verbose stack tracing
        public DoTypes(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions, bool initialprototyping)
        {
            this.stack = new LocalTypeStack();
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = state;
            this.actions = actions;
            if (!initialprototyping)
            {
                this.state.InitStack(this.stack);
            }

            this.initialproto = initialprototyping;
            this.protoskipping = false;
            this.skipdeadcode = false;
            this.protoreturn = (this.initialproto || !state.Type().IsTyped());
        }

        // Debug mode - set to true for verbose stack tracing
        public virtual void Done()
        {
            this.state = null;
            if (this.stack != null)
            {
                this.stack.Dispose();
                this.stack = null;
            }

            this.nodedata = null;
            this.subdata = null;
            if (this.backupstack != null)
            {
                this.backupstack.Dispose();
                this.backupstack = null;
            }

            this.actions = null;
        }

        // Debug mode - set to true for verbose stack tracing
        public virtual void AssertStack()
        {
            if (this.stack.Size() > 0)
            {
                JavaSystem.@out.Println("Uh-oh... dumping main() state:");
                this.state.PrintState();
                throw new Exception("Error: Final stack size " + this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        public override void OutARsaddCommand(ARsaddCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                UtilsType type = NodeUtils.GetType(node);
                this.stack.Push(type);
                DebugLog("RSADD", "type=" + type.ByteValue(), before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                bool isstruct = copy > 1;
                if (this.protoreturn && loc > this.stack.Size())
                {
                    if (isstruct)
                    {
                        StructType @struct = new StructType();
                        for (int i = copy; i >= 1; --i)
                        {
                            @struct.AddType(this.stack.Get(i, this.state));
                        }

                        this.state.SetReturnType(@struct, loc - this.stack.Size());
                        this.subdata.AddStruct(@struct);
                    }
                    else
                    {
                        this.state.SetReturnType(this.stack.Get(1, this.state), loc - this.stack.Size());
                    }
                }


                // CPDOWNSP doesn't change stack size, it copies TO a location
                DebugLog("CPDOWNSP", "copy=" + copy + " loc=" + loc, before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                for (int i = 0; i < copy; ++i)
                {
                    this.stack.Push(this.stack.Get(loc, this.state));
                }

                DebugLog("CPTOPSP", "copy=" + copy + " loc=" + loc, before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        public override void OutAConstCommand(AConstCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                UtilsType type = NodeUtils.GetType(node);
                this.stack.Push(type);
                DebugLog("CONST", "type=" + type.ByteValue(), before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        public override void OutAActionCommand(AActionCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int remove = NodeUtils.ActionRemoveElementCount(node, this.actions);
                UtilsType type = NodeUtils.GetReturnType(node, this.actions);
                int add = NodeUtils.StackSizeToPos(type.TypeSize());

                // Safety check: don't remove more than we have
                if (remove > this.stack.Size())
                {
                    JavaSystem.@out.Println("[DoTypes] WARNING: ACTION trying to remove " + remove + " but stack only has " + this.stack.Size() + " elements. Action: " + (this.actions != null ? this.actions.GetName(NodeUtils.GetActionId(node)) : "unknown"));

                    // Remove what we can
                    this.stack.Remove(this.stack.Size());
                }
                else
                {
                    this.stack.Remove(remove);
                }

                for (int i = 0; i < add; ++i)
                {
                    this.stack.Push(type);
                }

                string actionName = (this.actions != null) ? this.actions.GetName(NodeUtils.GetActionId(node)) : "action_" + NodeUtils.GetActionId(node);
                DebugLog("ACTION", actionName + " remove=" + remove + " add=" + add + " retType=" + type.ByteValue(), before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        public override void OutALogiiCommand(ALogiiCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();

                // Safety check
                if (this.stack.Size() < 2)
                {
                    JavaSystem.@out.Println("[DoTypes] WARNING: LOGII trying to remove 2 but stack only has " + this.stack.Size());
                    this.stack.Remove(this.stack.Size());
                }
                else
                {
                    this.stack.Remove(2);
                }

                this.stack.Push(new UtilsType((byte)3));
                DebugLog("LOGII", "", before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        public override void OutABinaryCommand(ABinaryCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int sizep3;
                int sizep2;
                int sizeresult;
                UtilsType resulttype;
                string opType;
                if (NodeUtils.IsEqualityOp(node))
                {
                    if (NodeUtils.GetType(node).Equals((byte)36))
                    {
                        sizep2 = (sizep3 = NodeUtils.StackSizeToPos(node.GetSize()));
                        opType = "equality_struct";
                    }
                    else
                    {
                        sizep2 = (sizep3 = 1);
                        opType = "equality";
                    }

                    sizeresult = 1;
                    resulttype = new UtilsType((byte)3);
                }
                else if (NodeUtils.IsVectorAllowedOp(node))
                {
                    sizep3 = NodeUtils.GetParam1Size(node);
                    sizep2 = NodeUtils.GetParam2Size(node);
                    sizeresult = NodeUtils.GetResultSize(node);
                    resulttype = NodeUtils.GetReturnType(node);
                    opType = "vector_op";
                }
                else
                {
                    sizep3 = 1;
                    sizep2 = 1;
                    sizeresult = 1;
                    resulttype = new UtilsType((byte)3);
                    opType = "default";
                }

                int totalRemove = sizep3 + sizep2;

                // Safety check
                if (totalRemove > this.stack.Size())
                {
                    JavaSystem.@out.Println("[DoTypes] WARNING: BINARY trying to remove " + totalRemove + " but stack only has " + this.stack.Size() + ". opType=" + opType);
                    this.stack.Remove(this.stack.Size());
                }
                else
                {
                    this.stack.Remove(totalRemove);
                }

                for (int i = 0; i < sizeresult; ++i)
                {
                    this.stack.Push(resulttype);
                }

                DebugLog("BINARY", opType + " p1=" + sizep3 + " p2=" + sizep2 + " result=" + sizeresult, before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        public override void OutAUnaryCommand(AUnaryCommand node)
        {
            instructionCount++;

            // Unary operations don't change stack size - they operate on top of stack in place
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                DebugLog("UNARY", "", before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int removeCount = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (this.initialproto)
                {
                    int @params = this.stack.RemovePrototyping(removeCount);
                    if (@params > 0)
                    {
                        this.state.SetParamCount(@params);
                    }
                }
                else
                {

                    // Safety check
                    if (removeCount > this.stack.Size())
                    {
                        JavaSystem.@out.Println("[DoTypes] WARNING: MOVSP trying to remove " + removeCount + " but stack only has " + this.stack.Size());
                        this.stack.Remove(this.stack.Size());
                    }
                    else
                    {
                        this.stack.Remove(removeCount);
                    }
                }

                DebugLog("MOVSP", "remove=" + removeCount, before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        public override void OutAStoreStateCommand(AStoreStateCommand node)
        {
            instructionCount++;

            // STORESTATE doesn't modify the stack - it saves state for later
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                DebugLog("STORESTATE", "", before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        public override void OutAStackCommand(AStackCommand node)
        {
            instructionCount++;

            // Handle SAVEBP/RESTOREBP - these don't affect SP stack
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                DebugLog("STACK", NodeUtils.GetType(node).ToString(), before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();

                // Safety check
                if (this.stack.Size() < 1)
                {
                    JavaSystem.@out.Println("[DoTypes] WARNING: JZ/JNZ trying to remove 1 but stack is empty");
                }
                else
                {
                    this.stack.Remove(1);
                }

                DebugLog("JZ/JNZ", "", before, this.stack.Size());
            }

            this.CheckProtoskippingStart(node);
            if (!this.protoskipping && !this.skipdeadcode && !this.IsLogOr(node))
            {
                this.StoreStackState(this.nodedata.GetDestination(node));
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        public override void OutAJumpCommand(AJumpCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                DebugLog("JMP", "", before, this.stack.Size());
            }

            this.CheckProtoskippingStart(node);
            if (!this.protoskipping && !this.skipdeadcode)
            {
                this.StoreStackState(this.nodedata.GetDestination(node));
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                SubroutineState substate = this.subdata.GetState(this.nodedata.GetDestination(node));
                if (!substate.IsPrototyped())
                {
                    JavaSystem.@out.Println("Uh-oh...");
                    substate.PrintState();
                    throw new Exception("Hit JSR on unprototyped subroutine " + this.nodedata.GetPos(this.nodedata.GetDestination(node)));
                }

                int paramsize = substate.GetParamCount();
                if (substate.IsTotallyPrototyped())
                {

                    // Safety check
                    if (paramsize > this.stack.Size())
                    {
                        JavaSystem.@out.Println("[DoTypes] WARNING: JSR trying to remove " + paramsize + " params but stack only has " + this.stack.Size());
                        this.stack.Remove(this.stack.Size());
                    }
                    else
                    {
                        this.stack.Remove(paramsize);
                    }
                }
                else
                {
                    this.stack.RemoveParams(paramsize, substate);
                    if (substate.Type().Equals(unchecked((byte)(-1))))
                    {
                        if (this.stack.Size() > 0)
                        {
                            substate.SetReturnType(this.stack.Get(1, this.state), 0);
                        }
                    }

                    if (substate.Type().Equals(unchecked((byte)(-15))) && !substate.Type().IsTyped())
                    {
                        for (int i = 0; i < substate.Type().Count; ++i)
                        {
                            if (this.stack.Size() >= substate.Type().Count - i)
                            {
                                UtilsType type = this.stack.Get(substate.Type().Count - i, this.state);
                                if (!type.Equals(unchecked((byte)(-1))))
                                {
                                    ((StructType)substate.Type()).UpdateType(i, type);
                                }
                            }
                        }
                    }
                }

                DebugLog("JSR", "params=" + paramsize + " retType=" + substate.Type().ByteValue(), before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        public override void OutADestructCommand(ADestructCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int removesize = NodeUtils.StackSizeToPos(node.GetSizeRem());
                int savestart = NodeUtils.StackSizeToPos(node.GetOffset());
                int savesize = NodeUtils.StackSizeToPos(node.GetSizeSave());
                int firstRemove = removesize - (savesize + savestart);

                // Safety check
                if (firstRemove > this.stack.Size())
                {
                    JavaSystem.@out.Println("[DoTypes] WARNING: DESTRUCT first remove " + firstRemove + " but stack only has " + this.stack.Size());
                    firstRemove = this.stack.Size();
                }

                this.stack.Remove(firstRemove);

                // Second removal
                if (this.stack.Size() >= savesize + 1)
                {
                    this.stack.Remove(savesize + 1, savestart);
                }
                else
                {
                    JavaSystem.@out.Println("[DoTypes] WARNING: DESTRUCT second remove issue");
                }

                DebugLog("DESTRUCT", "rem=" + removesize + " start=" + savestart + " save=" + savesize, before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        public override void OutACopyTopBpCommand(ACopyTopBpCommand node)
        {
            instructionCount++;
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                for (int i = 0; i < copy; ++i)
                {
                    this.stack.Push(this.subdata.GetGlobalStack().GetType(loc));
                    --loc;
                }

                DebugLog("CPTOPBP", "copy=" + copy, before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        public override void OutACopyDownBpCommand(ACopyDownBpCommand node)
        {
            instructionCount++;

            // CPDOWNBP copies from stack to global - doesn't change SP stack size
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                DebugLog("CPDOWNBP", "", before, this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        public override void OutASubroutine(ASubroutine node)
        {
            if (this.initialproto)
            {
                this.state.StopPrototyping(true);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        public override void DefaultIn(Node node)
        {
            if (!this.protoskipping)
            {
                this.RestoreStackState(node);
            }
            else
            {
                this.CheckProtoskippingDone(node);
            }

            if (NodeUtils.IsCommandNode(node))
            {
                this.skipdeadcode = this.nodedata.DeadCode(node);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void CheckProtoskippingDone(Node node)
        {
            if (this.state.GetSkipEnd(this.nodedata.GetPos(node)))
            {
                this.protoskipping = false;
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void CheckProtoskippingStart(Node node)
        {
            if (this.state.GetSkipStart(this.nodedata.GetPos(node)))
            {
                this.protoskipping = true;
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void StoreStackState(Node node)
        {
            if (NodeUtils.IsStoreStackNode(node))
            {
                this.nodedata.SetStack(node, (LocalStack)this.stack.Clone(), true);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void RestoreStackState(Node node)
        {
            LocalTypeStack restore = (LocalTypeStack)this.nodedata.GetStack(node);
            if (restore != null)
            {
                this.stack = restore;
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private bool IsLogOr(Node node)
        {
            return this.nodedata.LogOrCode(node);
        }
    }
}




