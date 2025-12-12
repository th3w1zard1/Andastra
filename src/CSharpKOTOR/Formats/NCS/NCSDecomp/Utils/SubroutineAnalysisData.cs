using System;
using System.Collections;
using System.Collections.Generic;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using CSharpKOTOR.Formats.NCS.NCSDecomp.AST;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptutils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using IEnumerator = CSharpKOTOR.Formats.NCS.NCSDecomp.IEnumerator<object>;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class SubroutineAnalysisData
    {
        private NodeAnalysisData nodedata;
        private Dictionary<object, object> subroutines;
        private Dictionary<object, object> substates;
        private ASubroutine mainsub;
        private ASubroutine globalsub;
        private LocalVarStack globalstack;
        private List<object> globalstructs;
        private SubScriptState globalstate;
        //private bool result;

        public SubroutineAnalysisData(NodeAnalysisData nodedata)
        {
            this.nodedata = nodedata;
            this.subroutines = new Dictionary<object, object>();
            this.substates = new Dictionary<object, object>();
            this.globalsub = null;
            this.globalstack = null;
            this.mainsub = null;
            this.globalstructs = new List<object>();
        }

        public void ParseDone()
        {
            this.nodedata = null;
            if (this.substates != null)
            {
                foreach (object sub in this.substates.Values)
                {
                    ((SubroutineState)sub).ParseDone();
                }
                this.substates = null;
            }
            this.subroutines = null;
            this.mainsub = null;
            this.globalsub = null;
            this.globalstate = null;
        }

        public void Close()
        {
            if (this.nodedata != null)
            {
                this.nodedata.Dispose();
                this.nodedata = null;
            }
            if (this.substates != null)
            {
                foreach (object sub in this.substates.Values)
                {
                    ((SubroutineState)sub).Dispose();
                }
                this.substates = null;
            }
            if (this.subroutines != null)
            {
                this.subroutines.Clear();
                this.subroutines = null;
            }
            this.mainsub = null;
            this.globalsub = null;
            if (this.globalstack != null)
            {
                this.globalstack.Dispose();
                this.globalstack = null;
            }
            if (this.globalstructs != null)
            {
                foreach (object item in this.globalstructs)
                {
                    ((IDisposable)item)?.Dispose();
                }
                this.globalstructs = null;
            }
            if (this.globalstate != null)
            {
                this.globalstate.Dispose();
                this.globalstate = null;
            }
        }

        public void PrintStates()
        {
            PrintStates(int.MaxValue);
        }

        public void PrintStates(int maxStates)
        {
            int count = 0;
            IEnumerator<object> subnodes = new DictionaryKeyEnumeratorAdapter(this.substates);
            while (subnodes.HasNext() && count < maxStates)
            {
                Node node = (Node)subnodes.Next();
                SubroutineState state = (SubroutineState)this.substates[node];
                Console.WriteLine("Printing state for subroutine at " + this.nodedata.GetPos(node).ToString());
                state.PrintState();
                count++;
            }
            if (subnodes.HasNext())
            {
                Console.WriteLine("... (additional states omitted to prevent spam)");
            }
        }

        public SubScriptState GlobalState()
        {
            return this.globalstate;
        }

        public void GlobalState(SubScriptState globalstate)
        {
            this.globalstate = globalstate;
        }

        public ASubroutine GetGlobalsSub()
        {
            return this.globalsub;
        }

        public void SetGlobalsSub(ASubroutine globalsub)
        {
            this.globalsub = globalsub;
        }

        public ASubroutine GetMainSub()
        {
            return this.mainsub;
        }

        public void SetMainSub(ASubroutine mainsub)
        {
            this.mainsub = mainsub;
        }

        public LocalVarStack GetGlobalStack()
        {
            return this.globalstack;
        }

        public void SetGlobalStack(LocalVarStack stack)
        {
            this.globalstack = stack;
        }

        public int NumSubs()
        {
            return this.subroutines.Count;
        }

        public int CountSubsDone()
        {
            IEnumerator<object> subs = new DictionaryValueEnumeratorAdapter(this.substates);
            int count = 0;
            while (subs.HasNext())
            {
                if (((SubroutineState)subs.Next()).IsTotallyPrototyped())
                {
                    ++count;
                }
            }
            return count;
        }

        public SubroutineState GetState(Node sub)
        {
            SubroutineState state = (SubroutineState)this.substates[sub];
            return state;
        }

        public bool IsPrototyped(int pos, bool nullok)
        {
            Node sub = (Node)this.subroutines[pos];
            if (sub != null)
            {
                SubroutineState state = (SubroutineState)this.substates[sub];
                return state != null && state.IsPrototyped();
            }
            if (nullok)
            {
                return false;
            }
            throw new Exception("Checking prototype on a subroutine not in the hash");
        }

        public bool IsBeingPrototyped(int pos)
        {
            Node sub = (Node)this.subroutines[pos];
            if (sub == null)
            {
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            SubroutineState state = (SubroutineState)this.substates[sub];
            return state != null && state.IsBeingPrototyped();
        }

        public bool IsFullyPrototyped(int pos)
        {
            Node sub = (Node)this.subroutines[pos];
            if (sub == null)
            {
                throw new Exception("Checking prototype on a subroutine not in the hash");
            }
            SubroutineState state = (SubroutineState)this.substates[sub];
            return state != null && state.IsTotallyPrototyped();
        }

        public void AddStruct(StructType structType)
        {
            if (!this.globalstructs.Contains(structType))
            {
                this.globalstructs.Add(structType);
                structType.TypeName("structtype" + this.globalstructs.Count);
            }
        }

        public void AddStruct(VarStruct structVar)
        {
            StructType structtype = structVar.StructType();
            if (!this.globalstructs.Contains(structtype))
            {
                this.globalstructs.Add(structtype);
                structtype.TypeName("structtype" + this.globalstructs.Count);
            }
            else
            {
                structVar.StructType(this.GetStructPrototype(structtype));
            }
        }

        public string GetStructDeclarations()
        {
            string newline = Environment.NewLine;
            System.Text.StringBuilder buff = new System.Text.StringBuilder();
            for (int i = 0; i < this.globalstructs.Count; ++i)
            {
                StructType structtype = (StructType)this.globalstructs[i];
                if (!structtype.IsVector())
                {
                    buff.Append(structtype.ToDeclString() + " {" + newline);
                    List<object> types = structtype.Types();
                    for (int j = 0; j < types.Count; ++j)
                    {
                        buff.Append("\t" + ((Type)types[j]).ToDeclString() + " " + structtype.ElementName(j) + ";" + newline);
                    }
                    buff.Append("};" + newline + newline);
                    types = null;
                }
            }
            return buff.ToString();
        }

        public string GetStructTypeName(StructType structtype)
        {
            StructType protostruct = this.GetStructPrototype(structtype);
            return protostruct.TypeName();
        }

        public StructType GetStructPrototype(StructType structtype)
        {
            int index = this.globalstructs.IndexOf(structtype);
            if (index == -1)
            {
                this.globalstructs.Add(structtype);
                index = this.globalstructs.Count - 1;
            }
            return (StructType)this.globalstructs[index];
        }

        private void AddSubroutine(int pos, Node node, byte id)
        {
            this.subroutines[pos] = node;
            this.AddSubState(node, id);
        }

        private void AddSubState(Node sub, byte id)
        {
            SubroutineState state = new SubroutineState(this.nodedata, sub, id);
            this.substates[sub] = state;
        }

        private void AddSubState(Node sub, byte id, Type type)
        {
            SubroutineState state = new SubroutineState(this.nodedata, sub, id);
            state.SetReturnType(type, 1);
            this.substates[sub] = state;
        }

        private void AddMain(ASubroutine sub, bool conditional)
        {
            this.mainsub = sub;
            if (conditional)
            {
                this.AddSubState(this.mainsub, (byte)0, new Type((byte)3));
            }
            else
            {
                this.AddSubState(this.mainsub, (byte)0);
            }
        }

        private void AddGlobals(ASubroutine sub)
        {
            this.globalsub = sub;
        }

        public IEnumerator<object> GetSubroutines()
        {
            List<object> subs = new List<object>();
            SortedSet<int> keys = new SortedSet<int>(Comparer<int>.Create((x, y) => y.CompareTo(x)));
            foreach (int key in this.subroutines.Keys)
            {
                keys.Add(key);
            }
            foreach (int key in keys)
            {
                subs.Add(this.subroutines[key]);
            }
            return new ListEnumeratorAdapter(subs);
        }

        private class ListEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _list;
            private int _index;
            private object _current;

            public ListEnumeratorAdapter(List<object> list)
            {
                _list = list;
                _index = 0;
                _current = null;
            }

            public bool HasNext()
            {
                return _index < _list.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _current = _list[_index++];
                return _current;
            }

            // IEnumerator<object> implementation
            public object Current => _current;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _index = 0; _current = null; }
            public void Dispose() { }
        }

        private class DictionaryKeyEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _keys;
            private int _index;
            private object _current;

            public DictionaryKeyEnumeratorAdapter(Dictionary<object, object> dictionary)
            {
                _keys = new List<object>(dictionary.Keys);
                _index = 0;
                _current = null;
            }

            public bool HasNext()
            {
                return _index < _keys.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _current = _keys[_index++];
                return _current;
            }

            // IEnumerator<object> implementation
            public object Current => _current;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _index = 0; _current = null; }
            public void Dispose() { }
        }

        private class DictionaryValueEnumeratorAdapter : IEnumerator<object>
        {
            private readonly List<object> _values;
            private int _index;
            private object _current;

            public DictionaryValueEnumeratorAdapter(Dictionary<object, object> dictionary)
            {
                _values = new List<object>(dictionary.Values);
                _index = 0;
                _current = null;
            }

            public bool HasNext()
            {
                return _index < _values.Count;
            }

            public object Next()
            {
                if (!HasNext())
                    throw new InvalidOperationException("No next element");
                _current = _values[_index++];
                return _current;
            }

            // IEnumerator<object> implementation
            public object Current => _current;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (!HasNext()) return false;
                Next();
                return true;
            }
            public void Reset() { _index = 0; _current = null; }
            public void Dispose() { }
        }

        public void SplitOffSubroutines(Node ast)
        {
            Start rootStart = ast as Start;
            if (rootStart == null)
            {
                return;
            }

            bool conditional = NodeUtils.IsConditionalProgram(rootStart);
            TypedLinkedList subroutines = ((AProgram)rootStart.GetPProgram()).GetSubroutine();
            ASubroutine node = (ASubroutine)subroutines.RemoveFirst();
            if (subroutines.Count > 0 && this.IsGlobalsSub(node))
            {
                this.AddGlobals(node);
                node = (ASubroutine)subroutines.RemoveFirst();
            }
            this.AddMain(node, conditional);
            byte id = 1;
            while (subroutines.Count > 0)
            {
                node = (ASubroutine)subroutines.RemoveFirst();
                int pos = this.nodedata.GetPos(node);
                ASubroutine node2 = node;
                byte id2 = id;
                id = (byte)(id2 + 1);
                this.AddSubroutine(pos, node2, id2);
            }
            subroutines = null;
            node = null;
        }

        private bool IsGlobalsSub(ASubroutine node)
        {
            CheckIsGlobals cig = new CheckIsGlobals();
            node.Apply(cig);
            return cig.GetIsGlobals();
        }

        public virtual void Dispose()
        {
            // Clean up resources
            this.subroutines?.Clear();
            this.globalstructs?.Clear();
            this.substates?.Clear();
        }
    }
}




