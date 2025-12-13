// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:15-130
// Original: public class ASwitch extends ScriptNode
using System.Collections.Generic;
using System.Text;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ASwitch : ScriptNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:16
        // Original: protected AExpression switchexp;
        protected AExpression switchexp;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:17
        // Original: protected List<ASwitchCase> cases;
        protected List<ASwitchCase> cases;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:18
        // Original: protected ASwitchCase defaultcase;
        protected ASwitchCase defaultcase;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:19
        // Original: protected int start;
        protected int start;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:20
        // Original: protected int end;
        protected int end;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:22-26
        // Original: public ASwitch(int start, AExpression switchexp) { this.start = start; this.cases = new ArrayList<>(); this.switchExp(switchexp); }
        public ASwitch(int start, AExpression switchexp)
        {
            this.start = start;
            this.cases = new List<ASwitchCase>();
            this.SwitchExp(switchexp);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:28-31
        // Original: public void switchExp(AExpression switchexp) { switchexp.parent(this); this.switchexp = switchexp; }
        public virtual void SwitchExp(AExpression switchexp)
        {
            switchexp.Parent(this);
            this.switchexp = switchexp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:33-35
        // Original: public AExpression switchExp() { return this.switchexp; }
        public virtual AExpression SwitchExp()
        {
            return this.switchexp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:37-44
        // Original: public void end(int end) { this.end = end; if (this.defaultcase != null) { this.defaultcase.end(end); } else if (this.cases.size() > 0) { this.cases.get(this.cases.size() - 1).end(end); } }
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

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:46-48
        // Original: public int end() { return this.end; }
        public virtual int End()
        {
            return this.end;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:50-53
        // Original: public void addCase(ASwitchCase acase) { acase.parent(this); this.cases.add(acase); }
        public virtual void AddCase(ASwitchCase acase)
        {
            acase.Parent(this);
            this.cases.Add(acase);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:55-58
        // Original: public void addDefaultCase(ASwitchCase acase) { acase.parent(this); this.defaultcase = acase; }
        public virtual void AddDefaultCase(ASwitchCase acase)
        {
            acase.Parent(this);
            this.defaultcase = acase;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:60-62
        // Original: public ASwitchCase getLastCase() { return this.cases.get(this.cases.size() - 1); }
        public virtual ASwitchCase GetLastCase()
        {
            return this.cases[this.cases.Count - 1];
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:64-77
        // Original: public ASwitchCase getNextCase(ASwitchCase lastcase) { ... }
        public virtual ASwitchCase GetNextCase(ASwitchCase lastcase)
        {
            if (lastcase == null)
            {
                return this.GetFirstCase();
            }
            else if (lastcase.Equals(this.defaultcase))
            {
                return null;
            }
            else
            {
                int index = this.cases.IndexOf(lastcase) + 1;
                if (index == 0)
                {
                    throw new Exception("invalid last case passed in");
                }
                else
                {
                    return this.cases.Count > index ? this.cases[index] : this.defaultcase;
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:79-81
        // Original: public ASwitchCase getFirstCase() { return this.cases.size() > 0 ? this.cases.get(0) : this.defaultcase; }
        public virtual ASwitchCase GetFirstCase()
        {
            return this.cases.Count > 0 ? this.cases[0] : this.defaultcase;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:83-89
        // Original: public int getFirstCaseStart() { ... }
        public virtual int GetFirstCaseStart()
        {
            if (this.cases.Count > 0)
            {
                return this.cases[0].GetStart();
            }
            else
            {
                return this.defaultcase != null ? this.defaultcase.GetStart() : -1;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java:91-106
        // Original: @Override public String toString() { ... }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.tabs + "switch(" + this.switchexp + ") {" + this.newline);

            for (int i = 0; i < this.cases.Count; i++)
            {
                buff.Append(this.cases[i].ToString());
            }

            if (this.defaultcase != null)
            {
                buff.Append(this.defaultcase.ToString());
            }

            buff.Append(this.tabs + "}" + this.newline);
            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASwitch.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (this.cases != null)
            {
                foreach (ScriptNode param in this.cases)
                {
                    param.Close();
                }

                this.cases = null;
            }

            if (this.switchexp != null)
            {
                ((ScriptNode)this.switchexp).Close();
            }

            this.switchexp = null;
            if (this.defaultcase != null)
            {
                this.defaultcase.Close();
            }

            this.defaultcase = null;
        }
    }
}




