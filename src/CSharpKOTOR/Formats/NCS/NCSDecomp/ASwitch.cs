// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ASwitch : ScriptNode
    {
        protected AExpression switchexp;
        protected List<object> cases;
        protected ASwitchCase defaultcase;
        protected int start;
        protected int end;
        public ASwitch(int start, AExpression switchexp)
        {
            this.start = start;
            this.cases = new List<object>();
            this.SwitchExp(switchexp);
        }

        public virtual void SwitchExp(AExpression switchexp)
        {
            switchexp.Parent(this);
            this.switchexp = switchexp;
        }

        public virtual AExpression SwitchExp()
        {
            return this.switchexp;
        }

        public virtual void End(int end)
        {
            this.end = end;
            if (this.defaultcase != null)
            {
                this.defaultcase.End(end);
            }
            else if (this.cases.Count > 0)
            {
                ((ASwitchCase)this.cases[this.cases.Count - 1]).End(end);
            }
        }

        public virtual int End()
        {
            return this.end;
        }

        public virtual void AddCase(ASwitchCase acase)
        {
            acase.Parent(this);
            this.cases.Add(acase);
        }

        public virtual void AddDefaultCase(ASwitchCase acase)
        {
            acase.Parent(this);
            this.defaultcase = acase;
        }

        public virtual ASwitchCase GetLastCase()
        {
            return (ASwitchCase)this.cases[this.cases.Count - 1];
        }

        public virtual ASwitchCase GetNextCase(ASwitchCase lastcase)
        {
            if (lastcase == null)
            {
                return this.GetFirstCase();
            }

            if (lastcase.Equals(this.defaultcase))
            {
                return null;
            }

            int index = this.cases.IndexOf(lastcase) + 1;
            if (index == 0)
            {
                throw new Exception("invalid last case passed in");
            }

            if (this.cases.Count > index)
            {
                return (ASwitchCase)this.cases[index];
            }

            return this.defaultcase;
        }

        public virtual ASwitchCase GetFirstCase()
        {
            if (this.cases.Count > 0)
            {
                return (ASwitchCase)this.cases[0];
            }

            return this.defaultcase;
        }

        public virtual int GetFirstCaseStart()
        {
            if (this.cases.Count > 0)
            {
                return ((ASwitchCase)this.cases[0]).GetStart();
            }

            if (this.defaultcase != null)
            {
                return this.defaultcase.GetStart();
            }

            return -1;
        }

        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs.ToString() + "switch (" + this.switchexp + ") {" + this.newline);
            for (int i = 0; i < this.cases.Count; ++i)
            {
                buff.Append(this.cases[i].ToString());
            }

            if (this.defaultcase != null)
            {
                buff.Append(this.defaultcase.ToString());
            }

            buff.Append(this.tabs.ToString() + "}" + this.newline);
            return buff.ToString();
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.cases != null)
            {
                foreach (ScriptNode param in this.cases)
                {
                    param.Dispose();
                }

                this.cases = null;
            }

            if (this.switchexp != null)
            {
                ((ScriptNode)this.switchexp).Dispose();
            }

            this.switchexp = null;
            if (this.defaultcase != null)
            {
                this.defaultcase.Dispose();
            }

            this.defaultcase = null;
        }
    }
}




