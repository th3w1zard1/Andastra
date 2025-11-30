using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{

    /// <summary>
    /// NSS (NWScript Source) lexer/tokenizer.
    ///
    /// Tokenizes NSS source code into tokens for parsing. Handles keywords, operators,
    /// literals, identifiers, and special values (OBJECTSELF, OBJECTINVALID, etc.).
    ///
    /// References:
    ///     vendor/HoloLSP/server/src/nwscript-lexer.ts (TypeScript NSS lexer)
    ///     vendor/KotOR.js/src/nwscript/NWScriptCompiler.ts (Token handling)
    ///     vendor/xoreos-tools/src/nwscript/ (NSS lexer implementation)
    ///     PLY (Python Lex-Yacc) library for lexer generation
    /// </summary>
    public class NssLexer
    {
        private readonly string _source;
        private int _position;
        private int _line;
        private int _column;

        public NssLexer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _position = 0;
            _line = 1;
            _column = 1;
        }

        /// <summary>
        /// Tokenize the source code into a list of tokens.
        /// </summary>
        public static List<NssToken> Tokenize()
        {
            var tokens = new List<NssToken>();

            throw new NotImplementedException();
        }

        private char? Peek(int offset = 0)
        {
            int pos = _position + offset;
            return pos < _source.Length ? (char?)_source[pos] : null;
        }

        private char? Next()
        {
            if (_position >= _source.Length)
            {
                return null;
            }

            char ch = _source[_position++];
            if (ch == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            return ch;
        }
    }

    /// <summary>
    /// Represents a token in the NSS source code.
    /// </summary>
    public class NssToken
    {
        public NssTokenType Type { get; }
        public string Value { get; }
        public int Line { get; }
        public int Column { get; }

        public NssToken(NssTokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// Types of tokens in NSS source code.
    /// </summary>
    public enum NssTokenType
    {
        // Keywords
        Void,
        Int,
        Float,
        String,
        Object,
        Vector,
        Struct,
        If,
        Else,
        While,
        Do,
        For,
        Switch,
        Case,
        Default,
        Break,
        Continue,
        Return,
        Const,
        Action,
        Event,

        // Identifiers
        Identifier,

        // Literals
        IntegerLiteral,
        FloatLiteral,
        StringLiteral,

        // Operators
        Plus,
        Minus,
        Multiply,
        Divide,
        Modulo,
        Assign,
        PlusAssign,
        MinusAssign,
        MultiplyAssign,
        DivideAssign,
        ModuloAssign,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LogicalAnd,
        LogicalOr,
        LogicalNot,
        BitwiseAnd,
        BitwiseOr,
        BitwiseXor,
        BitwiseNot,
        LeftShift,
        RightShift,
        Increment,
        Decrement,

        // Punctuation
        LeftParen,
        RightParen,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Semicolon,
        Comma,
        Dot,
        Colon,
        QuestionMark,

        // Special
        EndOfFile,
        Error
    }
}

