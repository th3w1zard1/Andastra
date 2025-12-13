// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:22-480
// Original: public class SubroutineState
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class SubroutineState
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:23-38
        // Original: private static final byte PROTO_NO = 0; private static final byte PROTO_IN_PROGRESS = 1; private static final byte PROTO_DONE = 2; protected static final byte JUMP_YES = 0; protected static final byte JUMP_NO = 1; protected static final byte JUMP_NA = 2; private Type type; private ArrayList<Type> params; private int returndepth; private Node root; private int paramsize; private boolean paramstyped; private byte status; private NodeAnalysisData nodedata; private LinkedList<DecisionData> decisionqueue; private byte id;
        private static readonly byte PROTO_NO = 0;
        private static readonly byte PROTO_IN_PROGRESS = 1;
        private static readonly byte PROTO_DONE = 2;
        protected static readonly byte JUMP_YES = 0;
        protected static readonly byte JUMP_NO = 1;
        protected static readonly byte JUMP_NA = 2;
        private Type type;
        private List<Type> @params;
        private int returndepth;
        private Node root;
        private int paramsize;
        private bool paramstyped;
        private byte status;
        private NodeAnalysisData nodedata;
        private LinkedList decisionqueue;
        private byte id;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:40-50
        // Original: public SubroutineState(NodeAnalysisData nodedata, Node root, byte id) { this.status = 0; ... }
        public SubroutineState(NodeAnalysisData nodedata, Node root, byte id)
        {
            this.nodedata = nodedata;
            this.@params = new List<Type>();
            this.decisionqueue = new LinkedList();
            this.paramstyped = true;
            this.paramsize = 0;
            this.status = 0;
            this.type = new Type((byte)0);
            this.root = root;
            this.id = id;
        }

        public virtual void ParseDone()
        {
            this.root = null;
            this.nodedata = null;
            this.decisionqueue = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:58-73
        // Original: public void close() { ... it.next().close(); ... }
        public virtual void Close()
        {
            this.@params = null;
            this.root = null;
            this.nodedata = null;
            if (this.decisionqueue != null)
            {
                IEnumerator<object> it = this.decisionqueue.Iterator();
                while (it.HasNext())
                {
                    if (it.Next() is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                this.decisionqueue = null;
            }

            this.type = null;
        }

        public virtual void Dispose()
        {
            Close();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java:75-88
        // Original: public void printState()
        public virtual void PrintState()
        {
            JavaSystem.@out.Println("Return type is " + this.type);
            JavaSystem.@out.Println("There are " + this.paramsize + " parameters");
            if (this.paramsize > 0)
            {
                StringBuilder buff = new StringBuilder();
                buff.Append(" Types: ");

                foreach (Type paramType in this.@params)
                {
                    buff.Append(paramType + " ");
                }

                JavaSystem.@out.Println(buff);
            }
        }

        public virtual void PrintDecisions()
        {
            JavaSystem.@out.Println("-----------------------------");
            JavaSystem.@out.Println("Jump Decisions");
            for (int i = 0; i < this.decisionqueue.Count; ++i)
            {
                DecisionData data = (DecisionData)this.decisionqueue[i];
                string str = "  (" + (i + 1);
                str = str.ToString() + ") at pos " + this.nodedata.GetPos(data.decisionnode);
                if (data.decision == 0)
                {
                    str = str.ToString() + " do optional jump to ";
                }
                else if (data.decision == 2)
                {
                    str = str.ToString() + " do required jump to ";
                }
                else
                {
                    str = str.ToString() + " do not jump to ";
                }

                str = str.ToString() + data.destination;
                JavaSystem.@out.Println(str);
            }
        }

        public virtual string ToString(bool main)
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.type + " ");
            if (main)
            {
                buff.Append("main(");
            }
            else
            {
                buff.Append("sub" + this.id + "(");
            }

            string link = "";
            for (int i = 0; i < this.paramsize; ++i)
            {
                Type ptype = this.@params[i];
                buff.Append(link + ptype.ToDeclString() + " param" + i);
                link = ", ";
            }

            buff.Append(")");
            return buff.ToString();
        }

        public virtual void StartPrototyping()
        {
            this.status = PROTO_IN_PROGRESS;
        }

        public virtual void StopPrototyping(bool success)
        {
            if (success)
            {
                this.status = PROTO_DONE;
                this.decisionqueue = null;
            }
            else
            {
                this.status = PROTO_NO;
            }
        }

        public virtual bool IsPrototyped()
        {
            return this.status == PROTO_DONE;
        }

        public virtual bool IsBeingPrototyped()
        {
            return this.status == PROTO_IN_PROGRESS;
        }

        public virtual bool IsTotallyPrototyped()
        {
            return this.status == PROTO_DONE && this.paramstyped && this.type.IsTyped();
        }

        public virtual bool GetSkipStart(int pos)
        {
            if (this.decisionqueue == null || this.decisionqueue.IsEmpty())
            {
                return false;
            }

            DecisionData decision = (DecisionData)this.decisionqueue.GetFirst();
            if (this.nodedata.GetPos(decision.decisionnode) == pos)
            {
                if (decision.DoJump())
                {
                    return true;
                }

                this.decisionqueue.RemoveFirst();
            }

            return false;
        }

        public virtual bool GetSkipEnd(int pos)
        {
            if (((DecisionData)this.decisionqueue.GetFirst()).destination == pos)
            {
                this.decisionqueue.RemoveFirst();
                return true;
            }

            return false;
        }

        public virtual void SetParamCount(int @params)
        {
            this.paramsize = @params;
            if (@params > 0)
            {
                this.paramstyped = false;
                if (this.returndepth <= @params)
                {
                    this.type = new Type((byte)0);
                }
            }
        }

        public virtual int GetParamCount()
        {
            return this.paramsize;
        }

        public virtual Type Type()
        {
            return this.type;
        }

        public virtual List<object> Params()
        {
            return new List<object>(this.@params.Cast<object>());
        }

        public virtual void SetReturnType(Type type, int depth)
        {
            this.type = type;
            this.returndepth = depth;
        }

        public virtual void UpdateParams(LinkedList types)
        {
            new Type(unchecked((byte)(-1)));
            this.paramstyped = true;
            bool redo = this.@params.Count > 0;
            if (types.Count != this.paramsize)
            {
                throw new Exception("Parameter count does not match: expected " + this.paramsize + " and got " + types.Count);
            }

            for (int i = 0; i < types.Count; ++i)
            {
                Type newtype = (Type)types[i];
                if (redo)
                {
                    Type type = this.@params[i];
                }

                if (redo && !this.@params[i].IsTyped())
                {
                    this.@params[i] = newtype;
                }
                else if (!redo)
                {
                    this.@params.Add(newtype);
                }

                if (!this.@params[i].IsTyped())
                {
                    this.paramstyped = false;
                }
            }
        }

        public virtual Type GetParamType(int pos)
        {
            if (this.@params.Count < pos)
            {
                return new Type((byte)0);
            }

            return this.@params[pos - 1];
        }

        public virtual void InitStack(LocalTypeStack stack)
        {
            if (this.IsPrototyped())
            {
                if (this.type.IsTyped() && !this.type.Equals((byte)0))
                {
                    if (this.type.Equals(unchecked((byte)(-15))))
                    {
                        List<object> structtypes = ((StructType)this.type).Types();
                        for (int i = 0; i < structtypes.Count; ++i)
                        {
                            stack.Push((Type)structtypes[i]);
                        }

                        structtypes = null;
                    }
                    else
                    {
                        stack.Push(this.type);
                    }
                }

                if (this.paramsize == this.@params.Count)
                {
                    for (int j = 0; j < this.paramsize; ++j)
                    {
                        stack.Push(this.@params[j]);
                    }
                }
                else
                {
                    for (int j = 0; j < this.paramsize; ++j)
                    {
                        stack.Push(new Type(unchecked((byte)(-1))));
                    }
                }
            }
        }

        public virtual void InitStack(LocalVarStack stack)
        {
            if (!this.type.Equals((byte)0))
            {
                Variable retvar;
                if (typeof(StructType).IsInstanceOfType(this.type))
                {
                    retvar = new VarStruct((StructType)this.type);
                }
                else
                {
                    retvar = new Variable(this.type);
                }

                retvar.IsReturn(true);
                stack.Push(retvar);
                retvar = null;
            }

            for (int i = 0; i < this.paramsize; ++i)
            {
                Variable paramVar = new Variable(this.@params[i]);
                paramVar.IsParam(true);
                stack.Push(paramVar);
            }

            //Variable paramvar = null;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/SubroutineState.java
        // Original: public void ensureParamPlaceholders()
        public virtual void EnsureParamPlaceholders()
        {
            while (this.@params.Count < this.paramsize)
            {
                this.@params.Add(new Type(unchecked((byte)(-1))));
            }
        }

        public virtual byte GetId()
        {
            return this.id;
        }

        public virtual int GetStart()
        {
            return this.nodedata.GetPos(this.root);
        }

        public virtual int GetEnd()
        {
            return NodeUtils.GetSubEnd((ASubroutine)this.root);
        }

        public virtual void AddDecision(Node node, int destination)
        {
            DecisionData decision = new DecisionData(node, destination, false);
            this.decisionqueue.AddLast(decision);
            if (this.decisionqueue.Count > 3000)
            {
                throw new Exception("Decision queue size over 3000 - probable infinite loop");
            }
        }

        public virtual void AddJump(Node node, int destination)
        {
            DecisionData decision = new DecisionData(node, destination, true);
            this.decisionqueue.AddLast(decision);
        }

        public virtual int GetCurrentDestination()
        {
            DecisionData data = (DecisionData)this.decisionqueue.GetLast();
            if (data == null)
            {
                throw new Exception("Attempted to get a destination but no decision nodes found.");
            }

            return data.destination;
        }

        public virtual int SwitchDecision()
        {
            while (this.decisionqueue.Count > 0)
            {
                DecisionData data = (DecisionData)this.decisionqueue.GetLast();
                if (data.SwitchDecision())
                {
                    return data.destination;
                }

                this.decisionqueue.RemoveLast();
            }

            return -1;
        }

        private class DecisionData
        {
            public Node decisionnode;
            public byte decision;
            public int destination;
            public DecisionData(Node node, int destination, bool forcejump)
            {
                if (forcejump)
                {
                    this.decision = 2;
                }
                else
                {
                    this.decision = 1;
                }

                this.decisionnode = node;
                this.destination = destination;
            }

            public virtual bool DoJump()
            {
                return this.decision != 1;
            }

            public virtual bool SwitchDecision()
            {
                if (this.decision == 1)
                {
                    this.decision = 0;
                    return true;
                }

                return false;
            }

            public virtual void Dispose()
            {
                this.decisionnode = null;
            }
        }
    }
}




