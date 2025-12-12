//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using NCSDecompLinkedList = CSharpKOTOR.Formats.NCS.NCSDecomp.LinkedList;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:18-26
    // Original: public class VarStruct extends Variable
    public class VarStruct : Variable
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:19
        // Original: protected LinkedList<Variable> vars = new LinkedList<>();
        protected NCSDecompLinkedList vars = new NCSDecompLinkedList();
        protected StructType structtype;
        public VarStruct() : base(new UtilsType(unchecked((byte)(-15))))
        {
            this.size = 0;
            this.structtype = new StructType();
        }

        public VarStruct(StructType structtype) : this()
        {
            this.structtype = structtype;
            foreach (object typeObj in structtype.Types())
            {
                UtilsType type = (UtilsType)typeObj;
                if (typeof(StructType).IsInstanceOfType(typeObj))
                {
                    this.AddVar(new VarStruct((StructType)typeObj));
                }
                else
                {
                    this.AddVar(new Variable(type));
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:43-57
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (this.vars != null)
            {
                for (int i = 0; i < this.vars.Count; i++)
                {
                    ((Variable)this.vars[i]).Close();
                }
            }

            this.vars = null;
            if (this.structtype != null)
            {
                this.structtype.Close();
            }

            this.structtype = null;
        }

        public virtual void AddVar(Variable var)
        {
            this.vars.AddFirst(var);
            var.Varstruct(this);
            this.structtype.AddType(var.Type());
            this.size += var.Size();
        }

        public virtual void AddVarStackOrder(Variable var)
        {
            this.vars.Add(var);
            var.Varstruct(this);
            this.structtype.AddTypeStackOrder(var.Type());
            this.size += var.Size();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/VarStruct.java:74-76
        // Original: @Override public void name(String prefix, byte count)
        public override void Name(string prefix, byte count)
        {
            this.name = prefix + "struct" + count;
        }

        public virtual string Name()
        {
            return this.name;
        }

        public virtual void StructType(StructType structtype)
        {
            this.structtype = structtype;
        }

        public override string ToString()
        {
            return this.name;
        }

        public virtual string TypeName()
        {
            return this.structtype.TypeName();
        }

        public override string ToDeclString()
        {
            return this.structtype.ToDeclString() + " " + this.name;
        }

        public virtual void UpdateNames()
        {
            if (this.structtype.IsVector())
            {
                ((Variable)this.vars[0]).Name("z");
                ((Variable)this.vars[1]).Name("y");
                ((Variable)this.vars[2]).Name("x");
            }
            else
            {
                for (int i = 0; i < this.vars.Count; ++i)
                {
                    ((Variable)this.vars[i]).Name(this.structtype.ElementName(this.vars.Count - i - 1));
                }
            }
        }

        public override void Assigned()
        {
            for (int i = 0; i < this.vars.Count; ++i)
            {
                ((Variable)this.vars[i]).Assigned();
            }
        }

        public override void AddedToStack(LocalStack stack)
        {
            for (int i = 0; i < this.vars.Count; ++i)
            {
                ((Variable)this.vars[i]).AddedToStack(stack);
            }
        }

        public virtual bool Contains(Variable var)
        {
            return this.vars.Contains(var);
        }

        public virtual StructType StructType()
        {
            return this.structtype;
        }

        public override StackEntry GetElement(int stackpos)
        {

            // Handle edge case: empty struct
            if (this.vars.Count == 0)
            {
                throw new Exception("Attempting to get element from empty VarStruct");
            }


            // Handle edge case: stackpos beyond struct bounds - return last element
            // This can happen with certain bytecode patterns
            if (stackpos > this.size)
            {

                // Return the last variable element as fallback
                StackEntry lastEntry = (StackEntry)this.vars[this.vars.Count - 1];
                return lastEntry.GetElement(1);
            }

            int pos = 0;
            for (int i = this.vars.Count - 1; i >= 0; --i)
            {
                StackEntry entry = (StackEntry)this.vars[i];
                pos += entry.Size();
                if (pos == stackpos)
                {
                    return entry.GetElement(1);
                }

                if (pos > stackpos)
                {
                    return entry.GetElement(pos - stackpos + 1);
                }
            }


            // Fallback: return first element if we couldn't find exact match
            StackEntry firstEntry = (StackEntry)this.vars[0];
            return firstEntry.GetElement(1);
        }

        // Handle edge case: empty struct
        // Handle edge case: stackpos beyond struct bounds - return last element
        // This can happen with certain bytecode patterns
        // Return the last variable element as fallback
        // Fallback: return first element if we couldn't find exact match
        public virtual VarStruct Structify(int firstelement, int count, SubroutineAnalysisData subdata)
        {
            ListIterator it = this.vars.ListIterator();
            int pos = 0;
            while (it.HasNext())
            {
                StackEntry entry = (StackEntry)it.Next();
                pos += entry.Size();
                if (pos == firstelement)
                {
                    VarStruct varstruct = new VarStruct();
                    varstruct.AddVarStackOrder((Variable)entry);
                    it.Set(varstruct);
                    for (entry = (StackEntry)it.Next(), pos += entry.Size(); pos <= firstelement + count - 1; pos += entry.Size())
                    {
                        it.Remove();
                        varstruct.AddVarStackOrder((Variable)entry);
                        if (!it.HasNext())
                        {
                            break;
                        }

                        entry = (StackEntry)it.Next();
                    }

                    subdata.AddStruct(varstruct);
                    return varstruct;
                }

                if (pos == firstelement + count - 1)
                {
                    return (VarStruct)entry;
                }

                if (pos > firstelement + count - 1)
                {
                    return ((VarStruct)entry).Structify(firstelement - (pos - entry.Size()), count, subdata);
                }
            }

            return null;
        }
    }
}




