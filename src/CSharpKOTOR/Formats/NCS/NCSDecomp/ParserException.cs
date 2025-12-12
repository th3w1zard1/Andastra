// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp.Parser
{
    public class ParserException : Exception
    {
        Token token;
        public ParserException(Token token, string message) : base(message)
        {
            this.token = token;
        }

        public virtual Token GetToken()
        {
            return this.token;
        }
    }
}




