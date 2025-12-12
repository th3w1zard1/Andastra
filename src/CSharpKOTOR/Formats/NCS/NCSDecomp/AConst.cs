// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AConst : ScriptNode, AExpression
    {
        private Const theconst;
        public AConst(Const theconst)
        {
            this.theconst = theconst;
        }

        public override string ToString()
        {
            return this.theconst.ToString();
        }

        public virtual StackEntry Stackentry()
        {
            return this.theconst;
        }

        public virtual void Stackentry(StackEntry stackentry)
        {
            this.theconst = (Const)stackentry;
        }

        public override void Dispose()
        {
            base.Dispose();
            this.theconst = null;
        }
    }
}




