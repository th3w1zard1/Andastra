// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:12-73
// Original: public class AFcnCallExp extends ScriptNode implements AExpression
using System.Collections.Generic;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;
namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AFcnCallExp : ScriptNode, AExpression
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:13
        // Original: private List<AExpression> params;
        // Note: Using List<object> to handle type compatibility
        private List<object> @params;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:14
        // Original: private byte id;
        private byte id;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:15
        // Original: private StackEntry stackentry;
        private StackEntry stackentry;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:17-24
        // Original: public AFcnCallExp(byte id, List<AExpression> params) { this.id = id; this.params = new ArrayList<>(); for (int i = 0; i < params.size(); i++) { this.addParam(params.get(i)); } }
        public AFcnCallExp(byte id, List<object> @params)
        {
            this.id = id;
            this.@params = new List<object>();
            for (int i = 0; i < @params.Count; ++i)
            {
                this.AddParam((AExpression)@params[i]);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:26-29
        // Original: protected void addParam(AExpression param) { param.parent(this); this.params.add(param); }
        protected virtual void AddParam(AExpression param)
        {
            param.Parent(this);
            this.@params.Add(param);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java:31-44
        // Original: @Override public String toString() { StringBuffer buff = new StringBuffer(); buff.append("sub").append(Byte.toString(this.id)).append("("); ... }
        public override string ToString()
        {
            StringBuilder buff = new StringBuilder();
            buff.Append("sub").Append(this.id.ToString()).Append("(");
            string prefix = "";
            for (int i = 0; i < this.@params.Count; ++i)
            {
                buff.Append(prefix + this.@params[i].ToString());
                prefix = ", ";
            }

            buff.Append(")");
            return buff.ToString();
        }

        public virtual StackEntry Stackentry()
        {
            return this.stackentry;
        }

        public virtual void Stackentry(StackEntry stackentry)
        {
            this.stackentry = stackentry;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AFcnCallExp.java
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

                this.@params = null;
            }

            if (this.stackentry != null)
            {
                this.stackentry.Close();
            }

            this.stackentry = null;
        }
    }
}




