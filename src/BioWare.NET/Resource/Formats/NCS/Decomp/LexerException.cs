// Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/lexer/LexerException.java:10-15
// Original: public class LexerException extends Exception
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.NCS.Decomp.Node;

namespace BioWare.NET.Resource.Formats.NCS.Decomp.Lexer
{
    public class LexerException : Exception
    {
        // Matching NCSDecomp implementation at vendor/NCSDecomp/src/main/java/com/kotor/resource/formats/ncs/lexer/LexerException.java:12-14
        // Original: public LexerException(String message) { super(message); }
        public LexerException(string message) : base(message)
        {
        }
    }
}




