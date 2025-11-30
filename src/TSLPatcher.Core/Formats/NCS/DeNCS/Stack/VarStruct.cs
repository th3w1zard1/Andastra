using System.Collections.Generic;
using TSLPatcher.Core.Formats.NCS.DeNCS.Utils;

namespace TSLPatcher.Core.Formats.NCS.DeNCS.Stack
{
    public class VarStruct : Variable
    {
        protected LinkedList<Variable> vars;
        protected StructType structtype;

        public VarStruct()
            : base(new Utils.Type(-15))
        {
            this.vars = new LinkedList<Variable>();
            this.size = 0;
            this.structtype = new StructType();
        }

        public VarStruct(StructType structtype)
            : this()
        {
            this.structtype = structtype;
            foreach (Type type in structtype.Types())
            {
                if (type is StructType st)
                {
                    this.AddVar(new VarStruct(st));
                }
                else
                {
                    this.AddVar(new Variable(type));
                }
            }
        }

        public override void Close()
        {
            base.Close();
            if (this.vars != null)
            {
                foreach (Variable var in this.vars)
                {
                    var.Close();
                }
            }
            this.vars = null;
            if (this.structtype != null)
            {
                this.structtype.Close();
            }
            this.structtype = null;
        }

        public void AddVar(Variable var)
        {
            this.vars.AddFirst(var);
            var.Varstruct(this);
            this.structtype.AddType(var.Type());
            this.size += var.Size();
        }

        public void AddVarStackOrder(Variable var)
        {
            this.vars.AddLast(var);
            var.Varstruct(this);
            this.structtype.AddTypeStackOrder(var.Type());
            this.size += var.Size();
        }

        public void Name(string prefix, byte count)
        {
            this.name = prefix + "struct" + count;
        }

        public string Name()
        {
            return this.name;
        }

        public void StructType(StructType structtype)
        {
            this.structtype = structtype;
        }

        public override string ToString()
        {
            return this.name ?? "";
        }

        public string TypeName()
        {
            return this.structtype.TypeName();
        }

        public override string ToDeclString()
        {
            return this.structtype.ToDeclString() + " " + this.name;
        }

        public void UpdateNames()
        {
            if (this.structtype.IsVector())
            {
                var varsList = new List<Variable>(this.vars);
                varsList[0].Name("z");
                varsList[1].Name("y");
                varsList[2].Name("x");
            }
            else
            {
                var varsList = new List<Variable>(this.vars);
                for (int i = 0; i < varsList.Count; i++)
                {
                    varsList[i].Name(this.structtype.ElementName(varsList.Count - i - 1));
                }
            }
        }

        public override void Assigned()
        {
            foreach (Variable var in this.vars)
            {
                var.Assigned();
            }
        }

        public override void AddedToStack(LocalStack stack)
        {
            foreach (Variable var in this.vars)
            {
                var.AddedToStack(stack);
            }
        }

        public bool Contains(Variable var)
        {
            return this.vars.Contains(var);
        }

        public StructType StructType()
        {
            return this.structtype;
        }

        public override StackEntry GetElement(int stackpos)
        {
            int pos = 0;
            var varsList = new List<Variable>(this.vars);
            for (int i = varsList.Count - 1; i >= 0; i--)
            {
                StackEntry entry = varsList[i];
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
            throw new Utils.RuntimeException("Stackpos was greater than stack size");
        }

        // TODO: Uncomment when SubroutineAnalysisData is created
        /*
        public VarStruct Structify(int firstelement, int count, Utils.SubroutineAnalysisData subdata)
        {
            var varsList = new List<Variable>(this.vars);
            int pos = 0;
            for (int i = 0; i < varsList.Count; i++)
            {
                StackEntry entry = varsList[i];
                pos += entry.Size();
                if (pos == firstelement)
                {
                    VarStruct varstruct = new VarStruct();
                    varstruct.AddVarStackOrder((Variable)entry);
                    varsList[i] = varstruct;
                    int j = i + 1;
                    while (j < varsList.Count && pos <= firstelement + count - 1)
                    {
                        entry = varsList[j];
                        pos += entry.Size();
                        varstruct.AddVarStackOrder((Variable)entry);
                        varsList.RemoveAt(j);
                    }
                    subdata.AddStruct(varstruct);
                    this.vars = new LinkedList<Variable>(varsList);
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
        */
    }
}

