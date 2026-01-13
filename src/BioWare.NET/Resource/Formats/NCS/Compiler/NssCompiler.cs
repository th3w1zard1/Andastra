using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using BioWare.NET.Resource.Formats.NCS.Optimizers;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS.Compiler
{

    /// <summary>
    /// NSS to NCS compiler.
    /// </summary>
    public class NssCompiler
    {
        private readonly BioWareGame _game;
        [CanBeNull]
        private readonly List<string> _libraryLookup;
        private readonly bool _debug;
        [CanBeNull]
        private readonly List<ScriptFunction> _functions;
        [CanBeNull]
        private readonly List<ScriptConstant> _constants;

        public NssCompiler(BioWareGame game, [CanBeNull] List<string> libraryLookup = null, bool debug = false,
            [CanBeNull] List<ScriptFunction> functions = null, [CanBeNull] List<ScriptConstant> constants = null)
        {
            _game = game;
            _libraryLookup = libraryLookup;
            _debug = debug;
            _functions = functions;
            _constants = constants;
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        /// Implements selective symbol loading to match nwnnsscomp.exe behavior.
        /// </summary>
        public NCS Compile(string source, Dictionary<string, byte[]> library = null)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("Source cannot be null or empty", nameof(source));
            }

            // MATCHES nwnnsscomp.exe: Two-pass selective symbol loading
            // Pass 1: Analyze symbol usage to determine what needs to be included
            var symbolUsage = AnalyzeSymbolUsage(source);

            // Pass 2: Filter functions/constants to only include referenced symbols
            List<ScriptFunction> functions = FilterUsedFunctions(
                _functions ?? (_game.IsK1() ? ScriptDefs.KOTOR_FUNCTIONS : ScriptDefs.TSL_FUNCTIONS),
                symbolUsage.usedFunctions);

            List<ScriptConstant> constants = FilterUsedConstants(
                _constants ?? (_game.IsK1() ? ScriptDefs.KOTOR_CONSTANTS : ScriptDefs.TSL_CONSTANTS),
                symbolUsage.usedConstants);

            // Filter library to only include referenced include files
            var filteredLibrary = FilterLibrary(library, symbolUsage.includeFiles);

            var parser = new NssParser(functions, constants, filteredLibrary, _libraryLookup);
            CodeRoot root = parser.Parse(source);

            var ncs = new NCS();
            root.Compile(ncs);

            return ncs;
        }

        /// <summary>
        /// Analyze NSS source to determine which symbols are actually used.
        /// Matches nwnnsscomp.exe's selective loading behavior.
        /// 
        /// Implementation uses token-based analysis for accurate symbol extraction:
        /// - Uses NssLexer to properly tokenize source code
        /// - Skips comments and string literals
        /// - Identifies function calls by identifier followed by open paren
        /// - Identifies constants by pattern matching and context
        /// - Handles different contexts (declarations vs usage)
        /// 
        /// Based on nwnnsscomp.exe: Two-pass compilation with selective symbol loading.
        /// First pass analyzes symbol usage, second pass filters symbols to include only used ones.
        /// </summary>
        private static SymbolUsage AnalyzeSymbolUsage(string source)
        {
            var usage = new SymbolUsage();

            // Use NssLexer for proper tokenization (matches nwnnsscomp.exe tokenization)
            var lexer = new NssLexer();
            int lexResult = lexer.Analyse(source);

            if (lexResult != 0 || lexer.Tokens == null)
            {
                // Lexer failed - fall back to line-based analysis for includes only
                var lines = source.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("#include"))
                    {
                        var includeFile = ExtractIncludeFileName(trimmed);
                        if (!string.IsNullOrEmpty(includeFile) && !usage.includeFiles.Contains(includeFile))
                        {
                            usage.includeFiles.Add(includeFile);
                        }
                    }
                }
                return usage;
            }

            // Track context for better symbol identification
            bool inFunctionDeclaration = false;
            bool inStructDeclaration = false;
            int braceDepth = 0;
            int parenDepth = 0;

            // Known NSS keywords that should not be treated as symbols
            var keywords = new HashSet<string>
            {
                "void", "int", "float", "string", "object", "vector", "action", "effect",
                "event", "itemproperty", "location", "talent", "struct", "if", "else",
                "for", "while", "do", "switch", "case", "default", "break", "continue",
                "return", "true", "false", "OBJECT_SELF", "OBJECT_INVALID"
            };

            // Process tokens to extract symbols
            for (int i = 0; i < lexer.Tokens.Count; i++)
            {
                var token = lexer.Tokens[i];

                // Skip comments and string literals (they don't contain symbols)
                if (token is NssComment || (token is NssLiteral literal && literal.LiteralType == NssLiteralType.String))
                {
                    continue;
                }

                // Track preprocessor directives (#include)
                if (token is NssPreprocessor preprocessor)
                {
                    if (preprocessor.PreprocessorType == NssPreprocessorType.Include)
                    {
                        var includeFile = ExtractIncludeFileName(preprocessor.Data);
                        if (!string.IsNullOrEmpty(includeFile) && !usage.includeFiles.Contains(includeFile))
                        {
                            usage.includeFiles.Add(includeFile);
                        }
                    }
                    continue;
                }

                // Track brace depth to understand scope
                if (token is NssSeparator separator)
                {
                    if (separator.Separator == NssSeparators.OpenCurlyBrace)
                    {
                        braceDepth++;
                    }
                    else if (separator.Separator == NssSeparators.CloseCurlyBrace)
                    {
                        braceDepth--;
                        if (braceDepth == 0)
                        {
                            inFunctionDeclaration = false;
                            inStructDeclaration = false;
                        }
                    }
                    else if (separator.Separator == NssSeparators.OpenParen)
                    {
                        parenDepth++;
                    }
                    else if (separator.Separator == NssSeparators.CloseParen)
                    {
                        parenDepth--;
                    }
                    continue;
                }

                // Track keywords that indicate declarations
                if (token is NssKeyword keyword)
                {
                    if (keyword.Keyword == NssKeywords.Struct)
                    {
                        inStructDeclaration = true;
                    }
                    else if (keyword.Keyword == NssKeywords.Void || keyword.Keyword == NssKeywords.Int ||
                             keyword.Keyword == NssKeywords.Float || keyword.Keyword == NssKeywords.String ||
                             keyword.Keyword == NssKeywords.Object || keyword.Keyword == NssKeywords.Vector ||
                             keyword.Keyword == NssKeywords.Action || keyword.Keyword == NssKeywords.Effect ||
                             keyword.Keyword == NssKeywords.Event || keyword.Keyword == NssKeywords.ItemProperty ||
                             keyword.Keyword == NssKeywords.Location || keyword.Keyword == NssKeywords.Talent)
                    {
                        // Type keyword - next identifier might be a function declaration
                        // Check if next non-whitespace token is an identifier followed by open paren
                        if (i + 1 < lexer.Tokens.Count)
                        {
                            int nextIdx = i + 1;
                            // Skip whitespace/separators
                            while (nextIdx < lexer.Tokens.Count &&
                                   (lexer.Tokens[nextIdx] is NssSeparator sep &&
                                    (sep.Separator == NssSeparators.Space || sep.Separator == NssSeparators.Tab || sep.Separator == NssSeparators.NewLine)))
                            {
                                nextIdx++;
                            }

                            if (nextIdx < lexer.Tokens.Count && lexer.Tokens[nextIdx] is NssIdentifier)
                            {
                                // Check if followed by open paren (function declaration)
                                int parenIdx = nextIdx + 1;
                                while (parenIdx < lexer.Tokens.Count &&
                                       (lexer.Tokens[parenIdx] is NssSeparator sep2 &&
                                        (sep2.Separator == NssSeparators.Space || sep2.Separator == NssSeparators.Tab || sep2.Separator == NssSeparators.NewLine)))
                                {
                                    parenIdx++;
                                }

                                if (parenIdx < lexer.Tokens.Count &&
                                    lexer.Tokens[parenIdx] is NssSeparator sep3 &&
                                    sep3.Separator == NssSeparators.OpenParen)
                                {
                                    inFunctionDeclaration = true;
                                }
                            }
                        }
                    }
                    continue;
                }

                // Process identifiers to extract function calls and constants
                if (token is NssIdentifier identifier)
                {
                    string identName = identifier.Identifier;

                    if (string.IsNullOrEmpty(identName) || keywords.Contains(identName))
                    {
                        continue;
                    }

                    // Check if this is a function call (identifier followed by open paren)
                    // Skip if we're in a function declaration (this would be the function name itself)
                    if (!inFunctionDeclaration && !inStructDeclaration)
                    {
                        // Look ahead for open paren (skip whitespace)
                        int nextIdx = i + 1;
                        while (nextIdx < lexer.Tokens.Count &&
                               (lexer.Tokens[nextIdx] is NssSeparator sep &&
                                (sep.Separator == NssSeparators.Space || sep.Separator == NssSeparators.Tab || sep.Separator == NssSeparators.NewLine)))
                        {
                            nextIdx++;
                        }

                        if (nextIdx < lexer.Tokens.Count &&
                            lexer.Tokens[nextIdx] is NssSeparator sep2 &&
                            sep2.Separator == NssSeparators.OpenParen)
                        {
                            // This is a function call
                            if (!usage.usedFunctions.Contains(identName))
                            {
                                usage.usedFunctions.Add(identName);
                            }
                            continue;
                        }
                    }

                    // Check if this is a constant
                    // Constants in NSS are typically:
                    // 1. All uppercase with underscores (e.g., OBJECT_SELF, EVENT_TYPE_ACTIVATE)
                    // 2. TRUE/FALSE (handled as keywords, but check anyway)
                    // 3. Named constants from script definitions
                    bool isConstant = false;

                    // Pattern: All uppercase letters, digits, and underscores
                    if (identName.Length > 0 &&
                        identName.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '_') &&
                        identName.Any(c => char.IsUpper(c)))
                    {
                        isConstant = true;
                    }

                    // Additional check: If it's used in a context that suggests a constant
                    // (e.g., after comparison operators, in switch cases, etc.)
                    if (!isConstant && i > 0)
                    {
                        var prevToken = lexer.Tokens[i - 1];
                        // Check if previous token suggests constant usage
                        if (prevToken is NssOperator op)
                        {
                            // Constants often appear after comparison operators
                            // Check for comparison operators: ==, !=, <, >, <=, >=
                            if (op.Operator == NssOperators.Equals ||
                                op.Operator == NssOperators.NotEqual ||
                                op.Operator == NssOperators.LessThan ||
                                op.Operator == NssOperators.GreaterThan ||
                                op.Operator == NssOperators.LessThanOrEqual ||
                                op.Operator == NssOperators.GreaterThanOrEqual)
                            {
                                isConstant = true;
                            }
                        }
                        else if (prevToken is NssKeyword kw &&
                                 (kw.Keyword == NssKeywords.Case || kw.Keyword == NssKeywords.Switch))
                        {
                            // Constants in switch cases
                            isConstant = true;
                        }
                    }

                    if (isConstant)
                    {
                        if (!usage.usedConstants.Contains(identName))
                        {
                            usage.usedConstants.Add(identName);
                        }
                    }
                }
            }

            return usage;
        }

        private static string ExtractIncludeFileName(string includeLine)
        {
            var startQuote = includeLine.IndexOf('"');
            if (startQuote == -1) startQuote = includeLine.IndexOf('<');
            if (startQuote == -1) return null;

            var endChar = includeLine[startQuote] == '"' ? '"' : '>';
            var endQuote = includeLine.IndexOf(endChar, startQuote + 1);
            if (endQuote == -1) return null;

            var filename = includeLine.Substring(startQuote + 1, endQuote - startQuote - 1);
            return filename.EndsWith(".nss") ? filename.Substring(0, filename.Length - 4) : filename;
        }


        private static List<ScriptFunction> FilterUsedFunctions(List<ScriptFunction> allFunctions, List<string> usedNames)
        {
            // Always include essential functions that nwnnsscomp.exe includes by default
            var essentialFunctions = new HashSet<string> {
                "main", "StartingConditional", "GetLastPerceived", "GetEnteringObject",
                "GetExitingObject", "GetIsDead", "GetHitDice", "GetTag", "GetName",
                "GetStringLength", "GetStringLeft", "GetStringRight", "GetStringMid",
                "IntToString", "FloatToString", "GetLocalInt", "SetLocalInt"
            };

            var filtered = new List<ScriptFunction>();
            foreach (var func in allFunctions)
            {
                if (essentialFunctions.Contains(func.Name) || usedNames.Contains(func.Name))
                {
                    filtered.Add(func);
                }
            }

            return filtered;
        }

        private static List<ScriptConstant> FilterUsedConstants(List<ScriptConstant> allConstants, List<string> usedNames)
        {
            // Always include essential constants
            var essentialConstants = new HashSet<string> {
                "TRUE", "FALSE", "OBJECT_INVALID", "OBJECT_SELF"
            };

            var filtered = new List<ScriptConstant>();
            foreach (var constant in allConstants)
            {
                if (essentialConstants.Contains(constant.Name) || usedNames.Contains(constant.Name))
                {
                    filtered.Add(constant);
                }
            }

            return filtered;
        }

        private static Dictionary<string, byte[]> FilterLibrary(Dictionary<string, byte[]> library, List<string> includeFiles)
        {
            if (library == null) return null;

            var filtered = new Dictionary<string, byte[]>();
            foreach (var includeFile in includeFiles)
            {
                if (library.ContainsKey(includeFile))
                {
                    filtered[includeFile] = library[includeFile];
                }
            }

            // Always include nwscript if available
            if (library.ContainsKey("nwscript"))
            {
                filtered["nwscript"] = library["nwscript"];
            }

            return filtered;
        }

        private class SymbolUsage
        {
            public List<string> usedFunctions = new List<string>();
            public List<string> usedConstants = new List<string>();
            public List<string> includeFiles = new List<string>();
        }
    }

    // NssParser is now in NSS/NssParser.cs
}
