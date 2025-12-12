// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AControlLoop : ScriptRootNode
    {
        protected AExpression condition;
        public AControlLoop(int start, int end) : base(start, end)
        {
        }

        public virtual void End(int end)
        {
            this.end = end;
        }

        public virtual void Condition(AExpression condition)
        {
            condition.Parent(this);
            this.condition = condition;
        }

        public virtual AExpression Condition()
        {
            return this.condition;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (this.condition != null)
            {
                ((ScriptNode)this.condition).Dispose();
                this.condition = null;
            }
        }
    }
}




