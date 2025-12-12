// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public class AActionArgExp : ScriptRootNode, AExpression
    {
        public AActionArgExp(int start, int end) : base(start, end)
        {
            this.start = start;
            this.end = end;
        }

        public virtual StackEntry Stackentry()
        {
            return null;
        }

        public virtual void Stackentry(StackEntry stackentry)
        {
        }
    }
}




