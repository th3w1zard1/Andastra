//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Utils
{
    public class StructType : Type
    {
        private List<object> types;
        private bool alltyped;
        private string typename;
        private List<object> elements;
        public StructType() : base(unchecked((byte)(-15)))
        {
            this.types = new List<object>();
            this.alltyped = true;
            this.size = 0;
        }

        public override void Dispose()
        {
            if (this.types != null)
            {
                IEnumerator<object> it = this.types.Iterator();
                while (it.HasNext())
                {
                    if (it.Next() is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                this.types = null;
            }

            this.elements = null;
        }

        public virtual void Print()
        {
            JavaSystem.@out.Println("Struct has " + this.types.Count + " entries.");
            if (this.alltyped)
            {
                JavaSystem.@out.Println("They have all been typed");
            }
            else
            {
                JavaSystem.@out.Println("They have not all been typed");
            }

            for (int i = 0; i < this.types.Count; ++i)
            {
                JavaSystem.@out.Println("  Type: " + this.types[i].ToString());
            }
        }

        public virtual void AddType(Type type)
        {
            this.types.Add(type);
            if (type.Equals(new Type(unchecked((byte)(-1)))))
            {
                this.alltyped = false;
            }

            this.size += type.Size();
        }

        public virtual void AddTypeStackOrder(Type type)
        {
            this.types.Insert(0, type);
            if (type.Equals(new Type(unchecked((byte)(-1)))))
            {
                this.alltyped = false;
            }

            this.size += type.Size();
        }

        public virtual bool IsVector()
        {
            if (this.size != 3)
            {
                return false;
            }

            for (int i = 0; i < 3; ++i)
            {
                if (!this.types[i].Equals((byte)4))
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

        public override int Count
        {
            get { return this.types.Count; }
        }

        public virtual void UpdateType(int pos, Type type)
        {
            this.types[pos] = type;
            this.UpdateTyped();
        }

        public virtual List<object> Types()
        {
            return this.types;
        }

        protected virtual void UpdateTyped()
        {
            this.alltyped = true;
            for (int i = 0; i < this.types.Count; ++i)
            {
                if (!((Type)this.types[i]).IsTyped())
                {
                    this.alltyped = false;
                    return;
                }
            }
        }

        public override bool Equals(object obj)
        {
            return typeof(StructType).IsInstanceOfType(obj) && this.types.Equals(((StructType)obj).Types());
        }

        public override int GetHashCode()
        {
            return this.types.GetHashCode();
        }

        public virtual void TypeName(string name)
        {
            this.typename = name;
        }

        public virtual string TypeName()
        {
            return this.typename;
        }

        public override string ToDeclString()
        {
            if (this.IsVector())
            {
                return new Type(Type.VT_VECTOR).ToString();
            }

            return this.ToString() + " " + this.typename;
        }

        public virtual string ElementName(int i)
        {
            if (this.elements == null)
            {
                this.SetElementNames();
            }

            return (string)this.elements[i];
        }

        public override Type GetElement(int pos)
        {
            IEnumerator<object> it = this.types.Iterator();
            //int curpos = 0;
            if (!it.HasNext())
            {
                throw new Exception("Pos was greater than struct size");
            }

            Type entry = (Type)it.Next();
            pos += entry.Size();
            return entry.GetElement(1);
        }

        private void SetElementNames()
        {
            this.elements = new List<object>();
            Dictionary<object, object> typecounts = new Dictionary<object, object>();
            if (this.IsVector())
            {
                this.elements.Add("x");
                this.elements.Add("y");
                this.elements.Add("z");
            }
            else
            {
                for (int i = 0; i < this.types.Count; ++i)
                {
                    Type type = (Type)this.types[i];
                    object typecountObj = typecounts.ContainsKey(type) ? typecounts[type] : null;
                    int typecount = typecountObj != null ? (int)typecountObj : 0;
                    int count;
                    if (typecount != 0)
                    {
                        count = 1 + typecount;
                    }
                    else
                    {
                        count = 1;
                    }

                    this.elements.Add(type.ToString() + count);
                    typecounts[type] = count + 1;
                }
            }

            typecounts = null;
        }
    }
}




