// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:15-135
// Original: public class ASub extends ScriptRootNode
using System.Collections.Generic;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
using ScriptNodeNS = CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ASub : ScriptRootNode
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:16
        // Original: private Type type;
        private UtilsType type;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:17
        // Original: private byte id;
        private byte id;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:18
        // Original: private List<ScriptNode> params;
        // Note: Using List<object> to handle ScriptNodeNS.AVarRef (ScriptNode namespace) vs Scriptnode.ScriptNode namespace mismatch
        private List<object> @params;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:19
        // Original: private String name;
        private string name;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:20
        // Original: private boolean ismain;
        private bool ismain;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:22-34
        // Original: public ASub(Type type, byte id, List<AVarRef> params, int start, int end) { ... this.name = "sub" + Byte.toString(id); }
        public ASub(UtilsType type, byte id, List<ScriptNodeNS.AVarRef> @params, int start, int end) : base(start, end)
        {
            this.type = type;
            this.id = id;
            this.@params = new List<object>();
            this.tabs = "";
            for (int i = 0; i < @params.Count; i++)
            {
                this.AddParam(@params[i]);
            }

            this.name = "sub" + this.id.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:36-41
        // Original: public ASub(int start, int end) { super(start, end); this.type = new Type((byte)0); this.params = null; this.tabs = ""; }
        public ASub(int start, int end) : base(start, end)
        {
            this.type = new UtilsType((byte)0);
            this.@params = null;
            this.tabs = "";
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:43-46
        // Original: protected void addParam(AVarRef param) { param.parent(this); this.params.add(param); }
        protected virtual void AddParam(ScriptNodeNS.AVarRef param)
        {
            param.Parent((ScriptNodeNS.ScriptNode)(object)this);
            this.@params.Add(param);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:48-50
        // Original: @Override public String toString() { return this.getHeader() + " {" + this.newline + this.getBody() + "}" + this.newline; }
        public override string ToString()
        {
            return this.GetHeader() + " {" + this.newline + this.GetBody() + "}" + this.newline;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:53-61
        // Original: public String getBody() { StringBuffer buff = new StringBuffer(); for (int i = 0; i < this.children.size(); i++) { buff.append(this.children.get(i).toString()); } return buff.toString(); }
        public virtual string GetBody()
        {
            StringBuilder buff = new StringBuilder();
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:63-77
        // Original: public String getHeader() { ... }
        public virtual string GetHeader()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.type + " " + this.name + "(");
            string link = "";
            for (int i = 0; i < this.@params.Count; i++)
            {
                ScriptNodeNS.AVarRef param = (ScriptNodeNS.AVarRef)this.@params[i];
                UtilsType ptype = param.Type();
                buff.Append(link + ptype + " " + param.ToString());
                link = ", ";
            }

            buff.Append(")");
            return buff.ToString();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:79-88
        // Original: public void isMain(boolean ismain) { this.ismain = ismain; if (ismain) { if (this.type.equals((byte)3)) { this.name = "StartingConditional"; } else { this.name = "main"; } } }
        public virtual void IsMain(bool ismain)
        {
            this.ismain = ismain;
            if (ismain)
            {
                if (this.type.Equals((byte)3))
                {
                    this.name = "StartingConditional";
                }
                else
                {
                    this.name = "main";
                }
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:90-92
        // Original: public boolean isMain() { return this.ismain; }
        public virtual bool IsMain()
        {
            return this.ismain;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:94-96
        // Original: public Type type() { return this.type; }
        public virtual UtilsType Type()
        {
            return this.type;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:98-100
        // Original: public void name(String name) { this.name = name; }
        public virtual void Name(string name)
        {
            this.name = name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:102-104
        // Original: public String name() { return this.name; }
        public virtual string Name()
        {
            return this.name;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:106-117
        // Original: public ArrayList<Variable> getParamVars() { ArrayList<Variable> vars = new ArrayList<>(); if (this.params != null) { Iterator<ScriptNode> it = this.params.iterator(); while (it.hasNext()) { vars.add(((AVarRef)it.next()).var()); } } return vars; }
        public virtual List<Variable> GetParamVars()
        {
            List<Variable> vars = new List<Variable>();
            if (this.@params != null)
            {
                System.Collections.Generic.IEnumerator<object> it = this.@params.GetEnumerator();
                while (it.MoveNext())
                {
                    vars.Add(((ScriptNodeNS.AVarRef)it.Current).Var());
                }
            }

            return vars;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:122-126
            // Original: if (this.params != null) { for (ScriptNode param : this.params) { param.close(); } }
            if (this.@params != null)
            {
                foreach (object param in this.@params)
                {
                    ((ScriptNodeNS.AVarRef)param).Close();
                }
            }

            this.@params = null;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java:129-133
            // Original: if (this.type != null) { this.type.close(); } this.type = null;
            if (this.type != null)
            {
                this.type.Close();
            }
            this.type = null;
        }
    }
}




