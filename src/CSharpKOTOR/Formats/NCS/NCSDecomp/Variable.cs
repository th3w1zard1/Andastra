//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using Comparable = System.IComparable;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Stack
{
    public class Variable : StackEntry, Comparable
    {
        protected static readonly byte FCN_NORMAL = 0;
        protected static readonly byte FCN_RETURN = 1;
        protected static readonly byte FCN_PARAM = 2;
        private Dictionary<object, object> stackcounts;
        protected string name;
        protected bool assigned;
        protected VarStruct varstruct;
        protected byte function;
        public Variable(UtilsType type)
        {
            this.type = type;
            this.varstruct = null;
            this.assigned = false;
            this.size = 1;
            this.function = 0;
            this.stackcounts = new Dictionary<object, object>();
        }

        public Variable(byte type) : this(new UtilsType(type))
        {
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/stack/Variable.java:38-42
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            this.stackcounts = null;
            this.varstruct = null;
        }

        public override void DoneParse()
        {
            this.stackcounts = null;
        }

        public override void DoneWithStack(LocalVarStack stack)
        {
            this.stackcounts.Remove(stack);
        }

        public virtual void IsReturn(bool isreturn)
        {
            if (isreturn)
            {
                this.function = 1;
            }
            else
            {
                this.function = 0;
            }
        }

        public virtual void IsParam(bool isparam)
        {
            if (isparam)
            {
                this.function = 2;
            }
            else
            {
                this.function = 0;
            }
        }

        public virtual bool IsReturn()
        {
            return this.function == 1;
        }

        public virtual bool IsParam()
        {
            return this.function == 2;
        }

        public virtual void Assigned()
        {
            this.assigned = true;
        }

        public virtual bool IsAssigned()
        {
            return this.assigned;
        }

        public virtual bool IsStruct()
        {
            return this.varstruct != null;
        }

        public virtual void Varstruct(VarStruct varstruct)
        {
            this.varstruct = varstruct;
        }

        public virtual VarStruct Varstruct()
        {
            return this.varstruct;
        }

        public override void AddedToStack(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                this.stackcounts[stack] = 1;
            }
            else
            {
                int count = (int)countObj;
                this.stackcounts[stack] = count + 1;
            }
        }

        public override void RemovedFromStack(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                this.stackcounts.Remove(stack);
            }
            else
            {
                int count = (int)countObj;
                if (count == 0)
                {
                    this.stackcounts.Remove(stack);
                }
                else
                {
                    this.stackcounts[stack] = count - 1;
                }
            }
        }

        public virtual bool IsPlaceholder(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                return true;
            }

            int count = (int)countObj;
            return count == 0 && !this.assigned;
        }

        public virtual bool IsOnStack(LocalStack stack)
        {
            object countObj;
            this.stackcounts.TryGetValue(stack, out countObj);
            if (countObj == null)
            {
                return false;
            }

            int count = (int)countObj;
            return count > 0;
        }

        public virtual void Name(string prefix, byte hint)
        {
            this.name = prefix.ToString() + this.type.ToString() + hint.ToString();
        }

        public virtual void Name(string infix, int hint)
        {
            this.name = this.type.ToString() + infix + hint;
        }

        public virtual void Name(string name)
        {
            this.name = name;
        }

        public override StackEntry GetElement(int stackpos)
        {

            // For simple variables (non-struct), any position >= 1 returns this variable
            // The position calculation in LocalVarStack/VarStruct can produce values > 1
            // even for size-1 variables due to the formula (pos - offset + 1)
            // We treat simple variables as "atomic" - any sub-element access returns the whole variable
            if (stackpos < 1)
            {
                throw new Exception("Position " + stackpos + " must be >= 1");
            }

            return this;
        }

        // For simple variables (non-struct), any position >= 1 returns this variable
        // The position calculation in LocalVarStack/VarStruct can produce values > 1
        // even for size-1 variables due to the formula (pos - offset + 1)
        // We treat simple variables as "atomic" - any sub-element access returns the whole variable
        public virtual string ToDebugString()
        {
            return "type: " + this.type + " name: " + this.name + " assigned: " + this.assigned.ToString();
        }

        // For simple variables (non-struct), any position >= 1 returns this variable
        // The position calculation in LocalVarStack/VarStruct can produce values > 1
        // even for size-1 variables due to the formula (pos - offset + 1)
        // We treat simple variables as "atomic" - any sub-element access returns the whole variable
        public override string ToString()
        {
            if (this.varstruct != null)
            {
                this.varstruct.UpdateNames();
                return this.varstruct.Name() + "." + this.name;
            }

            return this.name;
        }

        // For simple variables (non-struct), any position >= 1 returns this variable
        // The position calculation in LocalVarStack/VarStruct can produce values > 1
        // even for size-1 variables due to the formula (pos - offset + 1)
        // We treat simple variables as "atomic" - any sub-element access returns the whole variable
        public virtual string ToDeclString()
        {
            return this.type + " " + this.name;
        }

        // For simple variables (non-struct), any position >= 1 returns this variable
        // The position calculation in LocalVarStack/VarStruct can produce values > 1
        // even for size-1 variables due to the formula (pos - offset + 1)
        // We treat simple variables as "atomic" - any sub-element access returns the whole variable
        public virtual int CompareTo(object o)
        {
            if (o == null)
            {
                throw new NullReferenceException();
            }

            if (!typeof(Variable).IsInstanceOfType(o))
            {
                throw new InvalidCastException();
            }

            if (this == o)
            {
                return 0;
            }

            if (this.name == null)
            {
                return -1;
            }

            if (((Variable)o).name == null)
            {
                return 1;
            }

            return this.name.CompareTo(((Variable)o).name);
        }

        // For simple variables (non-struct), any position >= 1 returns this variable
        // The position calculation in LocalVarStack/VarStruct can produce values > 1
        // even for size-1 variables due to the formula (pos - offset + 1)
        // We treat simple variables as "atomic" - any sub-element access returns the whole variable
        public virtual void StackWasCloned(LocalStack oldstack, LocalStack newstack)
        {
            object countObj;
            this.stackcounts.TryGetValue(oldstack, out countObj);
            if (countObj != null && (int)countObj > 0)
            {
                int count = (int)countObj;
                this.stackcounts[newstack] = count;
            }
        }
    }
}




