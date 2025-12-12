// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AExpressionStatement : ScriptNode
    {
        private AExpression exp;
        public AExpressionStatement(AExpression exp)
        {
            exp.Parent(this);
            this.exp = exp;
        }

        public virtual AExpression Exp()
        {
            return this.exp;
        }

        public override string ToString()
        {
            return this.tabs.ToString() + this.exp.ToString() + ";" + this.newline;
        }

        public override void Parent(ScriptNode parent)
        {
            base.Parent(parent);
            this.exp.Parent(this);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AExpressionStatement.java
        // Original: @Override public void close()
        public override void Close()
        {
            base.Close();
            if (this.exp != null)
            {
                ((ScriptNode)this.exp).Close();
            }

            this.exp = null;
        }
    }
}




