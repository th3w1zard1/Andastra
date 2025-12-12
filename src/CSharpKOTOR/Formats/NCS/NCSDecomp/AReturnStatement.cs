// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

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

        public override string ToString()
        {
            if (this.returnexp == null)
            {
                return this.tabs.ToString() + "return;" + this.newline;
            }

            return this.tabs.ToString() + "return " + this.returnexp.ToString() + ";" + this.newline;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.returnexp != null)
            {
                ((ScriptNode)this.returnexp).Dispose();
            }

            this.returnexp = null;
        }
    }
}




