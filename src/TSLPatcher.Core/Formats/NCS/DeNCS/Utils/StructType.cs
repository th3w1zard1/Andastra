using System;
using System.Collections.Generic;
using System.Linq;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Utils
{
    public class StructType : Type
    {
        private List<Type> types;
        private bool alltyped;
        private string typename;
        private List<string> elements;

        public StructType()
            : base(-15)
        {
            this.types = new List<Type>();
            this.alltyped = true;
            this.size = 0;
        }

        public override void Close()
        {
            if (this.types != null)
            {
                foreach (Type type in this.types)
                {
                    type.Close();
                }
                this.types = null;
            }
            this.elements = null;
        }

        public void Print()
        {
            Console.WriteLine("Struct has " + this.types.Count + " entries.");
            if (this.alltyped)
            {
                Console.WriteLine("They have all been typed");
            }
            else
            {
                Console.WriteLine("They have not all been typed");
            }
            foreach (Type type in this.types)
            {
                Console.WriteLine("  Type: " + type);
            }
        }

        public void AddType(Type type)
        {
            this.types.Add(type);
            if (type.Equals(new Type(-1)))
            {
                this.alltyped = false;
            }
            this.size += type.Size();
        }

        public void AddTypeStackOrder(Type type)
        {
            this.types.Insert(0, type);
            if (type.Equals(new Type(-1)))
            {
                this.alltyped = false;
            }
            this.size += type.Size();
        }

        public bool IsVector()
        {
            if (this.size != 3)
            {
                return false;
            }
            for (int i = 0; i < 3; i++)
            {
                if (!this.types[i].Equals((sbyte)4))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool IsTyped()
        {
            return this.alltyped;
        }

        public void UpdateType(int pos, Type type)
        {
            this.types[pos] = type;
            this.UpdateTyped();
        }

        public List<Type> Types()
        {
            return this.types;
        }

        protected void UpdateTyped()
        {
            this.alltyped = true;
            foreach (Type type in this.types)
            {
                if (!type.IsTyped())
                {
                    this.alltyped = false;
                    return;
                }
            }
        }

        public override bool Equals(object obj)
        {
            return obj is StructType other && this.types.SequenceEqual(other.types);
        }

        public override int GetHashCode()
        {
            return this.types.GetHashCode();
        }

        public void TypeName(string name)
        {
            this.typename = name;
        }

        public string TypeName()
        {
            return this.typename;
        }

        public override string ToDeclString()
        {
            if (this.IsVector())
            {
                return Type.ToString(-16);
            }
            return this.ToString() + " " + this.typename;
        }

        public string ElementName(int i)
        {
            if (this.elements == null)
            {
                this.SetElementNames();
            }
            return this.elements[i];
        }

        public override Type GetElement(int pos)
        {
            int curpos = 0;
            foreach (Type entry in this.types)
            {
                curpos += entry.Size();
                if (curpos == pos)
                {
                    return entry.GetElement(1);
                }
                if (curpos > pos)
                {
                    return entry.GetElement(curpos - pos + 1);
                }
            }
            throw new RuntimeException("Pos was greater than struct size");
        }

        private void SetElementNames()
        {
            this.elements = new List<string>();
            Dictionary<Type, int> typecounts = new Dictionary<Type, int>(1);
            if (this.IsVector())
            {
                this.elements.Add("x");
                this.elements.Add("y");
                this.elements.Add("z");
            }
            else
            {
                foreach (Type type in this.types)
                {
                    if (!typecounts.ContainsKey(type))
                    {
                        typecounts[type] = 1;
                    }
                    else
                    {
                        typecounts[type] = typecounts[type] + 1;
                    }
                    int count = typecounts[type];
                    this.elements.Add(type.ToString() + count);
                }
            }
        }
    }
}

