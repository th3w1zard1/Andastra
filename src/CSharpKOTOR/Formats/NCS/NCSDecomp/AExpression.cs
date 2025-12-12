// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp.Stack;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Scriptnode
{
    public interface AExpression
    {
        string ToString();
        ScriptNode Parent();
        void Parent(ScriptNode p0);
        StackEntry Stackentry();
        void Stackentry(StackEntry p0);
    }
}




