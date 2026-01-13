// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:12-24
// Original: public class ParserException extends Exception
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.Parser
{
    public class ParserException : Exception
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:14
        // Original: private final transient Token token;
        Token token;

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:16-19
        // Original: public ParserException(Token token, String message) { super(message); this.token = token; }
        public ParserException(Token token, string message) : base(message)
        {
            this.token = token;
        }

        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/parser/ParserException.java:21-23
        // Original: public Token getToken() { return this.token; }
        public virtual Token GetToken()
        {
            return this.token;
        }
    }
}




