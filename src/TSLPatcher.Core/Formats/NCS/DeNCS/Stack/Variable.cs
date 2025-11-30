using System;
using System.Collections.Generic;
using TSLPatcher.Core.Formats.NCS.DeNCS.Utils;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class Variable : StackEntry, IComparable
    {
        protected const byte FCN_NORMAL = 0;
        protected const byte FCN_RETURN = 1;
        protected const byte FCN_PARAM = 2;

        private Dictionary<LocalStack, int> stackcounts;
        protected string name;
        protected bool assigned;
        protected VarStruct varstruct;
        protected byte function;

        public Variable(Utils.Type type)
        {
            this.type = type;
            this.varstruct = null;
            this.assigned = false;
            this.size = 1;
            this.function = 0;
            this.stackcounts = new Dictionary<LocalStack, int>(1);
        }

        public Variable(sbyte type)
            : this(new Utils.Type(type))
        {
        }

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
            if (this.stackcounts != null && this.stackcounts.ContainsKey(stack))
            {
                this.stackcounts.Remove(stack);
            }
        }

        public void IsReturn(bool isreturn)
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

        public void IsParam(bool isparam)
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

        public bool IsReturn()
        {
            return this.function == 1;
        }

        public bool IsParam()
        {
            return this.function == 2;
        }

        public virtual void Assigned()
        {
            this.assigned = true;
        }

        public bool IsAssigned()
        {
            return this.assigned;
        }

        public bool IsStruct()
        {
            return this.varstruct != null;
        }

        public void Varstruct(VarStruct varstruct)
        {
            this.varstruct = varstruct;
        }

        public VarStruct Varstruct()
        {
            return this.varstruct;
        }

        public override void AddedToStack(LocalStack stack)
        {
            if (!this.stackcounts.ContainsKey(stack))
            {
                this.stackcounts[stack] = 1;
            }
            else
            {
                this.stackcounts[stack] = this.stackcounts[stack] + 1;
            }
        }

        public override void RemovedFromStack(LocalStack stack)
        {
            if (this.stackcounts.ContainsKey(stack))
            {
                int count = this.stackcounts[stack];
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

        public bool IsPlaceholder(LocalStack stack)
        {
            int count = this.stackcounts.ContainsKey(stack) ? this.stackcounts[stack] : 0;
            return count == 0 && !this.assigned;
        }

        public bool IsOnStack(LocalStack stack)
        {
            int count = this.stackcounts.ContainsKey(stack) ? this.stackcounts[stack] : 0;
            return count > 0;
        }

        public void Name(string prefix, byte hint)
        {
            this.name = prefix + this.type.ToString() + hint;
        }

        public void Name(string infix, int hint)
        {
            this.name = this.type.ToString() + infix + hint;
        }

        public void Name(string name)
        {
            this.name = name;
        }

        public override StackEntry GetElement(int stackpos)
        {
            if (stackpos != 1)
            {
                throw new Utils.RuntimeException("Position > 1 for var, not struct");
            }
            return this;
        }

        public string ToDebugString()
        {
            return "type: " + this.type + " name: " + this.name + " assigned: " + this.assigned;
        }

        public override string ToString()
        {
            if (this.varstruct != null)
            {
                this.varstruct.UpdateNames();
                return this.varstruct.Name() + "." + this.name;
            }
            return this.name ?? "";
        }

        public virtual string ToDeclString()
        {
            return this.type + " " + this.name;
        }

        public int CompareTo(object o)
        {
            if (o == null)
            {
                throw new NullReferenceException();
            }
            if (!(o is Variable))
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
            Variable other = (Variable)o;
            if (other.name == null)
            {
                return 1;
            }
            return this.name.CompareTo(other.name);
        }

        public void StackWasCloned(LocalStack oldstack, LocalStack newstack)
        {
            if (this.stackcounts.ContainsKey(oldstack) && this.stackcounts[oldstack] > 0)
            {
                this.stackcounts[newstack] = this.stackcounts[oldstack];
            }
        }
    }
}

