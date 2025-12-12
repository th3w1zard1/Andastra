// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Utils;
using ScriptNodeNS = CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;
using UtilsType = CSharpKOTOR.Formats.NCS.NCSDecomp.Utils.Type;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class ASub : ScriptRootNode
    {
        private UtilsType type;
        private byte id;
        private List<object> @params;
        private string name;
        private bool ismain;
        public ASub(UtilsType type, byte id, List<object> @params, int start, int end) : base(start, end)
        {
            this.type = type;
            this.id = id;
            this.@params = new List<object>();
            this.tabs = "";
            for (int i = 0; i < @params.Count; ++i)
            {
                this.AddParam((ScriptNodeNS.AVarRef)@params[i]);
            }

            this.name = "sub" + id;
        }

        public ASub(int start, int end) : base(start, end)
        {
            this.type = new UtilsType((byte)0);
            this.@params = null;
            this.tabs = "";
        }

        protected virtual void AddParam(ScriptNodeNS.AVarRef param)
        {
            ((AExpression)param).Parent(this);
            this.@params.Add(param);
        }

        public override string ToString()
        {
            return this.GetHeader() + " {" + this.newline + this.GetBody() + "}" + this.newline;
        }

        public virtual string GetBody()
        {
            StringBuilder buff = new StringBuilder();
            for (int i = 0; i < this.children.Count; ++i)
            {
                buff.Append(this.children[i].ToString());
            }

            return buff.ToString();
        }

        public virtual string GetHeader()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(this.type + " " + this.name + "(");
            string link = "";
            for (int i = 0; i < this.@params.Count; ++i)
            {
                ScriptNodeNS.AVarRef param = (ScriptNodeNS.AVarRef)this.@params[i];
                UtilsType ptype = param.Type();
                buff.Append(link.ToString() + ptype + " " + param.ToString());
                link = ", ";
            }

            buff.Append(")");
            return buff.ToString();
        }

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

        public virtual bool IsMain()
        {
            return this.ismain;
        }

        public virtual UtilsType Type()
        {
            return this.type;
        }

        public virtual void Name(string name)
        {
            this.name = name;
        }

        public virtual string Name()
        {
            return this.name;
        }

        public virtual List<object> GetParamVars()
        {
            List<object> vars = new List<object>();
            if (this.@params != null)
            {
                foreach (object param in this.@params)
                {
                    ScriptNodeNS.AVarRef varRef = (ScriptNodeNS.AVarRef)param;
                    vars.Add(varRef.Var());
                }
            }

            return vars;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/ASub.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (this.@params != null)
            {
                foreach (ScriptNode param in this.@params)
                {
                    param.Close();
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




