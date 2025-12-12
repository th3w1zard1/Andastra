// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.ScriptNode;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AReturnStatement : ScriptNode
    {
        protected AExpression returnexp;
        public AReturnStatement()
        {
        }

        public AReturnStatement(AExpression returnexp)
        {
            this.Returnexp(returnexp);
        }

        public virtual void Returnexp(AExpression returnexp)
        {
            returnexp.Parent(this);
            this.returnexp = returnexp;
        }

        public virtual AExpression Exp()
        {
            return this.returnexp;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/scriptnode/AReturnStatement.java:27-32
        // Original: @Override public String toString() { return this.returnexp == null ? this.tabs + "return;" + this.newline : this.tabs + "return " + ExpressionFormatter.formatValue(this.returnexp) + ";" + this.newline; }
        public override string ToString()
        {
            return this.returnexp == null
                ? this.tabs + "return;" + this.newline
                : this.tabs + "return " + ExpressionFormatter.FormatValue(this.returnexp) + ";" + this.newline;
        }

        public override void Close()
        {
            base.Close();
            if (this.returnexp != null)
            {
                ((ScriptNode)this.returnexp).Close();
            }

            this.returnexp = null;
        }
    }
}




