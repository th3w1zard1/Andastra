// Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py
// Original: """Generate scriptdefs.py from NSS files using the actual NCS lexer/parser."""
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS.Compiler;
using CSharpKOTOR.Formats.NCS.Compiler.NSS;

namespace GenerateScriptDefs
{
    /// <summary>
    /// Generate ScriptDefs.cs from NSS files using the actual NCS lexer/parser.
    /// 1:1 port from vendor/PyKotor/scripts/generate_scriptdefs.py
    /// </summary>
    public class Program
    {
        private static string TokenTypeToDataType(NssTokenBase token)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:60-75
            // Original: def token_type_to_datatype(token_type: str) -> str | None:
            if (token is NssKeyword keyword)
            {
                switch (keyword.Keyword)
                {
                    case NssKeywords.Int: return "int";
                    case NssKeywords.Float: return "float";
                    case NssKeywords.String: return "string";
                    case NssKeywords.Void: return "void";
                    case NssKeywords.Object: return "object";
                    case NssKeywords.Vector: return "vector";
                    case NssKeywords.Location: return "location";
                    case NssKeywords.Effect: return "effect";
                    case NssKeywords.Event: return "event";
                    case NssKeywords.Talent: return "talent";
                    case NssKeywords.Action: return "action";
                    default: return null;
                }
            }
            return null;
        }

        private static int SkipWhitespace(List<NssTokenBase> tokens, int idx)
        {
            while (idx < tokens.Count)
            {
                if (tokens[idx] is NssSeparator sep &&
                    (sep.Separator == NssSeparators.Space || sep.Separator == NssSeparators.NewLine || sep.Separator == NssSeparators.Tab))
                {
                    idx++;
                }
                else if (tokens[idx] is NssComment || tokens[idx] is NssPreprocessor)
                {
                    idx++;
                }
                else
                {
                    break;
                }
            }
            return idx;
        }

        private static (ConstantInfo info, int nextIdx)? ParseConstantFromTokens(
            List<NssTokenBase> tokens, int startIdx, List<string> lines)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:78-220
            // Original: def parse_constant_from_tokens(tokens: list, start_idx: int, lines: list[str]) -> tuple[dict, int] | None:
            int idx = SkipWhitespace(tokens, startIdx);
            if (idx >= tokens.Count)
            {
                return null;
            }

            // Pattern: TYPE [whitespace] IDENTIFIER [whitespace] = [whitespace] VALUE [whitespace] ;
            if (!(tokens[idx] is NssKeyword typeToken &&
                  (typeToken.Keyword == NssKeywords.Int || typeToken.Keyword == NssKeywords.Float || typeToken.Keyword == NssKeywords.String)))
            {
                return null;
            }

            string datatype = TokenTypeToDataType(tokens[idx]);
            if (datatype == null)
            {
                return null;
            }

            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count)
            {
                return null;
            }

            NssTokenBase nameToken = tokens[idx];
            if (!(nameToken is NssIdentifier ||
                  (nameToken is NssKeyword kw && (kw.Keyword == NssKeywords.ObjectSelf || kw.Keyword == NssKeywords.ObjectInvalid))))
            {
                return null;
            }

            string name = ExtractNameFromToken(nameToken);
            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count)
            {
                return null;
            }

            // Check for assignment operator
            if (!(tokens[idx] is NssOperator assignOp && assignOp.Operator == NssOperators.Assignment))
            {
                return null;
            }

            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count)
            {
                return null;
            }

            // Check for negative number pattern: MINUS VALUE
            bool isNegative = false;
            if (tokens[idx] is NssOperator minusOp && minusOp.Operator == NssOperators.Subtraction)
            {
                isNegative = true;
                idx = SkipWhitespace(tokens, idx + 1);
                if (idx >= tokens.Count)
                {
                    return null;
                }
            }

            NssTokenBase valueToken = tokens[idx];
            string value = null;

            if (datatype == "string" && valueToken is NssLiteral strLit && strLit.LiteralType == NssLiteralType.String)
            {
                value = strLit.Literal; // Raw string without quotes
            }
            else if (datatype == "int" && valueToken is NssLiteral intLit && intLit.LiteralType == NssLiteralType.Int)
            {
                if (int.TryParse(intLit.Literal, out int intVal))
                {
                    value = (isNegative ? "-" : "") + intVal.ToString();
                }
                else if (intLit.Literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    string hexText = intLit.Literal.Substring(2);
                    if (int.TryParse(hexText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int hexVal))
                    {
                        value = (isNegative ? "-" : "") + hexVal.ToString();
                    }
                }
            }
            else if (datatype == "int" && valueToken is NssKeyword boolKw)
            {
                if (boolKw.Keyword == NssKeywords.ObjectSelf)
                {
                    value = "1"; // TRUE
                }
                else if (boolKw.Keyword == NssKeywords.ObjectInvalid)
                {
                    value = "0"; // FALSE
                }
            }
            else if (datatype == "float" && valueToken is NssLiteral floatLit && floatLit.LiteralType == NssLiteralType.Float)
            {
                string floatStr = floatLit.Literal.TrimEnd('f', 'F');
                if (float.TryParse(floatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                {
                    value = (isNegative ? "-" : "") + floatVal.ToString(CultureInfo.InvariantCulture);
                }
            }

            if (value == null)
            {
                return null;
            }

            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count)
            {
                return null;
            }

            // Check for semicolon
            if (!(tokens[idx] is NssSeparator semicolon && semicolon.Separator == NssSeparators.Semicolon))
            {
                return null;
            }

            return (new ConstantInfo { DataType = datatype, Name = name, Value = value }, idx + 1);
        }

        private static string ExtractNameFromToken(NssTokenBase token)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:108-120
            // Original: Extract name - handle special tokens
            if (token is NssKeyword kw)
            {
                if (kw.Keyword == NssKeywords.ObjectSelf)
                {
                    return "OBJECT_SELF";
                }
                else if (kw.Keyword == NssKeywords.ObjectInvalid)
                {
                    return "OBJECT_INVALID";
                }
            }
            else if (token is NssIdentifier ident)
            {
                return ident.Identifier;
            }
            return token?.ToString() ?? "";
        }

        private static (FunctionInfo info, int nextIdx)? ParseFunctionFromTokens(
            List<NssTokenBase> tokens, int startIdx, List<string> lines, Dictionary<NssTokenBase, int> lineNumbers)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:223-287
            // Original: def parse_function_from_tokens(tokens: list, start_idx: int, lines: list[str], line_numbers: dict) -> tuple[dict, int] | None:
            int idx = SkipWhitespace(tokens, startIdx);
            if (idx >= tokens.Count)
            {
                return null;
            }

            // Check if this looks like a function declaration: TYPE IDENTIFIER ( ...
            if (!(tokens[idx] is NssKeyword typeKw && TokenTypeToDataType(tokens[idx]) != null))
            {
                // Also check for void
                if (!(tokens[idx] is NssKeyword voidKw && voidKw.Keyword == NssKeywords.Void))
                {
                    return null;
                }
            }

            string returnType = TokenTypeToDataType(tokens[idx]) ?? "void";
            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count || !(tokens[idx] is NssIdentifier))
            {
                return null;
            }

            string name = ((NssIdentifier)tokens[idx]).Identifier;
            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count || !(tokens[idx] is NssSeparator openParen && openParen.Separator == NssSeparators.OpenParen))
            {
                return null;
            }

            // Find matching closing paren and semicolon
            int parenCount = 1;
            int paramStartIdx = idx + 1;
            idx++;
            var paramTokens = new List<NssTokenBase>();

            while (idx < tokens.Count && parenCount > 0)
            {
                if (tokens[idx] is NssSeparator sep)
                {
                    if (sep.Separator == NssSeparators.OpenParen)
                    {
                        parenCount++;
                        paramTokens.Add(tokens[idx]);
                    }
                    else if (sep.Separator == NssSeparators.CloseParen)
                    {
                        parenCount--;
                        if (parenCount == 0)
                        {
                            break;
                        }
                        paramTokens.Add(tokens[idx]);
                    }
                    else
                    {
                        paramTokens.Add(tokens[idx]);
                    }
                }
                else
                {
                    paramTokens.Add(tokens[idx]);
                }
                idx++;
            }

            if (parenCount != 0)
            {
                return null;
            }

            // Check for semicolon after closing paren
            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count || !(tokens[idx] is NssSeparator semicolon && semicolon.Separator == NssSeparators.Semicolon))
            {
                return null;
            }

            // Parse parameters
            var @params = ParseFunctionParams(paramTokens);

            // Get function documentation from original lines
            int funcLineNum = lineNumbers.ContainsKey(tokens[startIdx]) ? lineNumbers[tokens[startIdx]] : 0;
            var funcDoc = ExtractFunctionDocumentationFromLine(lines, funcLineNum, name);

            return (new FunctionInfo
            {
                ReturnType = returnType,
                Name = name,
                Params = @params,
                Description = funcDoc.description,
                Raw = funcDoc.raw
            }, idx + 1);
        }

        private static List<ParamInfo> ParseFunctionParams(List<NssTokenBase> paramTokens)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:290-494
            // Original: def parse_function_params(param_tokens: list) -> list[dict]:
            var @params = new List<ParamInfo>();

            if (paramTokens.Count == 0)
            {
                return @params;
            }

            // Split by commas (but handle nested structures)
            var paramGroups = new List<List<NssTokenBase>>();
            var currentGroup = new List<NssTokenBase>();
            int parenDepth = 0;
            int bracketDepth = 0;

            foreach (var token in paramTokens)
            {
                if (token is NssSeparator sep)
                {
                    if (sep.Separator == NssSeparators.OpenParen)
                    {
                        parenDepth++;
                        currentGroup.Add(token);
                    }
                    else if (sep.Separator == NssSeparators.CloseParen)
                    {
                        parenDepth--;
                        currentGroup.Add(token);
                    }
                    else if (sep.Separator == NssSeparators.OpenSquareBracket)
                    {
                        bracketDepth++;
                        currentGroup.Add(token);
                    }
                    else if (sep.Separator == NssSeparators.CloseSquareBracket)
                    {
                        bracketDepth--;
                        currentGroup.Add(token);
                    }
                    else if (sep.Separator == NssSeparators.Comma && parenDepth == 0 && bracketDepth == 0)
                    {
                        if (currentGroup.Count > 0)
                        {
                            paramGroups.Add(currentGroup);
                        }
                        currentGroup = new List<NssTokenBase>();
                    }
                    else if (sep.Separator != NssSeparators.Space && sep.Separator != NssSeparators.NewLine && sep.Separator != NssSeparators.Tab)
                    {
                        currentGroup.Add(token);
                    }
                }
                else
                {
                    currentGroup.Add(token);
                }
            }

            if (currentGroup.Count > 0)
            {
                paramGroups.Add(currentGroup);
            }

            // Parse each parameter group
            foreach (var group in paramGroups)
            {
                // Remove whitespace from group
                var cleanGroup = group.Where(t => !(t is NssSeparator s &&
                    (s.Separator == NssSeparators.Space || s.Separator == NssSeparators.NewLine || s.Separator == NssSeparators.Tab))).ToList();

                if (cleanGroup.Count < 2)
                {
                    continue;
                }

                // Pattern: TYPE IDENTIFIER [= VALUE]
                if (cleanGroup[0] is NssKeyword typeKw && TokenTypeToDataType(cleanGroup[0]) != null &&
                    cleanGroup[1] is NssIdentifier)
                {
                    string paramType = TokenTypeToDataType(cleanGroup[0]) ?? "int";
                    string paramName = ((NssIdentifier)cleanGroup[1]).Identifier;
                    string defaultValue = null;

                    // Check for default value
                    if (cleanGroup.Count >= 4 && cleanGroup[2] is NssOperator assignOp && assignOp.Operator == NssOperators.Assignment)
                    {
                        NssTokenBase defaultToken = cleanGroup[3];

                        // Special handling: negative number defaults
                        if (cleanGroup.Count >= 5 &&
                            defaultToken is NssOperator minusOp && minusOp.Operator == NssOperators.Subtraction &&
                            cleanGroup[4] is NssLiteral lit)
                        {
                            if (lit.LiteralType == NssLiteralType.Int && int.TryParse(lit.Literal, out int intVal))
                            {
                                defaultValue = $"-{intVal}";
                            }
                            else if (lit.LiteralType == NssLiteralType.Float)
                            {
                                string floatStr = lit.Literal.TrimEnd('f', 'F');
                                if (float.TryParse(floatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                                {
                                    defaultValue = $"-{floatVal.ToString(CultureInfo.InvariantCulture)}";
                                }
                            }
                        }
                        // Special handling: vector defaults like [0.0,0.0,0.0]
                        else if (paramType == "vector" && defaultToken is NssSeparator bracket && bracket.Separator == NssSeparators.OpenSquareBracket)
                        {
                            // Try to parse vector literal: [ FLOAT_VALUE , FLOAT_VALUE , FLOAT_VALUE ]
                            if (cleanGroup.Count >= 10 &&
                                cleanGroup[3] is NssSeparator openBracket && openBracket.Separator == NssSeparators.OpenSquareBracket &&
                                cleanGroup[4] is NssLiteral xLit && xLit.LiteralType == NssLiteralType.Float &&
                                cleanGroup[5] is NssSeparator comma1 && comma1.Separator == NssSeparators.Comma &&
                                cleanGroup[6] is NssLiteral yLit && yLit.LiteralType == NssLiteralType.Float &&
                                cleanGroup[7] is NssSeparator comma2 && comma2.Separator == NssSeparators.Comma &&
                                cleanGroup[8] is NssLiteral zLit && zLit.LiteralType == NssLiteralType.Float &&
                                cleanGroup[9] is NssSeparator closeBracket && closeBracket.Separator == NssSeparators.CloseSquareBracket)
                            {
                                string xStr = xLit.Literal.TrimEnd('f', 'F');
                                string yStr = yLit.Literal.TrimEnd('f', 'F');
                                string zStr = zLit.Literal.TrimEnd('f', 'F');
                                if (float.TryParse(xStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                                    float.TryParse(yStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                                    float.TryParse(zStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                                {
                                    defaultValue = $"new Vector3({x.ToString(CultureInfo.InvariantCulture)}f, {y.ToString(CultureInfo.InvariantCulture)}f, {z.ToString(CultureInfo.InvariantCulture)}f)";
                                }
                            }
                        }
                        else if (defaultToken is NssLiteral lit2)
                        {
                            if (lit2.LiteralType == NssLiteralType.Int)
                            {
                                if (int.TryParse(lit2.Literal, out int intVal))
                                {
                                    defaultValue = intVal.ToString();
                                }
                                else if (lit2.Literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                {
                                    string hexText = lit2.Literal.Substring(2);
                                    if (int.TryParse(hexText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int hexVal))
                                    {
                                        defaultValue = hexVal.ToString();
                                    }
                                }
                            }
                            else if (lit2.LiteralType == NssLiteralType.Float)
                            {
                                string floatStr = lit2.Literal.TrimEnd('f', 'F');
                                if (float.TryParse(floatStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                                {
                                    defaultValue = floatVal.ToString(CultureInfo.InvariantCulture);
                                }
                            }
                            else if (lit2.LiteralType == NssLiteralType.String)
                            {
                                defaultValue = $"\"{lit2.Literal.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
                            }
                        }
                        else if (defaultToken is NssKeyword kw)
                        {
                            if (kw.Keyword == NssKeywords.ObjectInvalid)
                            {
                                defaultValue = "OBJECT_INVALID";
                            }
                            else if (kw.Keyword == NssKeywords.ObjectSelf)
                            {
                                defaultValue = "OBJECT_SELF";
                            }
                        }
                        else if (defaultToken is NssIdentifier ident)
                        {
                            defaultValue = ident.Identifier;
                        }
                    }

                    @params.Add(new ParamInfo
                    {
                        Type = paramType,
                        Name = paramName,
                        Default = defaultValue
                    });
                }
            }

            return @params;
        }

        private static (string description, string raw) ExtractFunctionDocumentationFromLine(
            List<string> lines, int lineNum, string funcName)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:497-528
            // Original: def extract_function_documentation_from_line(lines: list[str], line_num: int, func_name: str) -> dict:
            int funcLineIdx = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i], $@"\b{Regex.Escape(funcName)}\s*\("))
                {
                    funcLineIdx = i;
                    break;
                }
            }

            if (funcLineIdx == -1)
            {
                return ("", "");
            }

            // Collect comment lines before function
            var comments = new List<string>();
            int j = funcLineIdx - 1;
            while (j >= 0 && j >= funcLineIdx - 50)
            {
                string line = lines[j].Trim();
                if (line.StartsWith("//"))
                {
                    comments.Insert(0, lines[j].TrimEnd());
                }
                else if (line == "")
                {
                    // Skip empty lines
                }
                else
                {
                    break;
                }
                j--;
            }

            // Get function signature line
            string sigLine = lines[funcLineIdx].Trim();
            var allLines = new List<string>(comments) { sigLine };
            string description = string.Join("\r\n", allLines);
            string raw = description;

            return (description, raw);
        }

        private static string PreprocessNss(string content)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:531-543
            // Original: def preprocess_nss(content: str) -> str:
            var lines = content.Split('\n');
            var processedLines = new List<string>();

            foreach (string line in lines)
            {
                // Skip preprocessor directives
                if (line.Trim().StartsWith("#"))
                {
                    continue;
                }
                processedLines.Add(line);
            }

            return string.Join("\n", processedLines);
        }

        private static (List<ConstantInfo> constants, List<FunctionInfo> functions) ParseNssFile(
            string nssPath, Game game)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:546-590
            // Original: def parse_nss_file(nss_path: Path, game: Game) -> tuple[list[dict], list[dict]]:
            string content = File.ReadAllText(nssPath, Encoding.UTF8);
            var lines = content.Split('\n').ToList();

            // Preprocess to remove preprocessor directives
            string processedContent = PreprocessNss(content);

            // Use lexer to tokenize
            var lexer = new NssLexer();
            int result = lexer.Analyse(processedContent);
            if (result != 0)
            {
                throw new Exception($"Failed to tokenize NSS file: {nssPath}");
            }

            // Create line number mapping for tokens
            var lineNumbers = new Dictionary<NssTokenBase, int>();
            // Note: Line number mapping would require more sophisticated tracking
            // For now, we'll use a simplified approach in ExtractFunctionDocumentationFromLine

            var constants = new List<ConstantInfo>();
            var functions = new List<FunctionInfo>();

            // Parse constants and functions from tokens
            int i = 0;
            while (i < lexer.Tokens.Count)
            {
                // Try to parse as constant
                var constResult = ParseConstantFromTokens(lexer.Tokens, i, lines);
                if (constResult.HasValue)
                {
                    constants.Add(constResult.Value.info);
                    i = constResult.Value.nextIdx;
                    continue;
                }

                // Try to parse as function
                var funcResult = ParseFunctionFromTokens(lexer.Tokens, i, lines, lineNumbers);
                if (funcResult.HasValue)
                {
                    functions.Add(funcResult.Value.info);
                    i = funcResult.Value.nextIdx;
                    continue;
                }

                i++;
            }

            return (constants, functions);
        }

        private static string CSharpTypeFromNss(string datatype)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:593-608
            // Original: def python_type_from_nss(datatype: str) -> str:
            switch (datatype.ToLower())
            {
                case "int": return "DataType.Int";
                case "float": return "DataType.Float";
                case "string": return "DataType.String";
                case "void": return "DataType.Void";
                case "object": return "DataType.Object";
                case "vector": return "DataType.Vector";
                case "location": return "DataType.Location";
                case "effect": return "DataType.Effect";
                case "event": return "DataType.Event";
                case "talent": return "DataType.Talent";
                case "action": return "DataType.Action";
                default: return $"DataType.{datatype}";
            }
        }

        private static string GenerateConstantCSharp(ConstantInfo constant)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:611-624
            // Original: def generate_constant_python(constant: dict) -> str:
            string datatypeCs = CSharpTypeFromNss(constant.DataType);
            string name = constant.Name;
            string value = constant.Value;

            if (constant.DataType == "string")
            {
                // Value is the raw string content, need to escape for C#
                value = $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            }
            else if (constant.DataType == "float")
            {
                value = value + "f";
            }

            return $"        new ScriptConstant({datatypeCs}, \"{name}\", {value}),";
        }

        private static string GenerateFunctionCSharp(FunctionInfo func, List<ConstantInfo> constants)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:645-730
            // Original: def generate_function_python(func: dict, constants: list[dict]) -> str:
            string returnTypeCs = CSharpTypeFromNss(func.ReturnType);
            string name = func.Name;

            var paramsCs = new List<string>();
            foreach (var param in func.Params)
            {
                string paramTypeCs = CSharpTypeFromNss(param.Type);
                string paramName = param.Name;
                if (param.Default != null)
                {
                    string defaultFormatted = param.Default;
                    // Handle special cases
                    if (param.Default == "OBJECT_SELF" || param.Default == "OBJECT_INVALID")
                    {
                        defaultFormatted = param.Default; // Use constant name directly
                    }
                    else if (param.Default.StartsWith("\""))
                    {
                        // Already a string literal
                        defaultFormatted = param.Default;
                    }
                    else if (param.Default.StartsWith("new Vector3("))
                    {
                        // Already formatted as C# code
                        defaultFormatted = param.Default;
                    }
                    else if (param.Type == "string")
                    {
                        defaultFormatted = $"\"{param.Default.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
                    }
                    else if (param.Type == "float" && !param.Default.EndsWith("f"))
                    {
                        defaultFormatted = param.Default + "f";
                    }

                    paramsCs.Add($"new ScriptParam({paramTypeCs}, \"{paramName}\", {defaultFormatted})");
                }
                else
                {
                    paramsCs.Add($"new ScriptParam({paramTypeCs}, \"{paramName}\", null)");
                }
            }

            string paramsStr = paramsCs.Count > 0 ? "new List<ScriptParam> { " + string.Join(", ", paramsCs) + " }" : "new List<ScriptParam>()";

            // Escape strings for regular C# string literals
            string description = func.Description
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
            string raw = func.Raw
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");

            return $"        new ScriptFunction(\n" +
                   $"            {returnTypeCs},\n" +
                   $"            \"{name}\",\n" +
                   $"            {paramsStr},\n" +
                   $"            \"{description}\",\n" +
                   $"            \"{raw}\",\n" +
                   $"        ),";
        }

        private static string GenerateScriptDefs(
            List<ConstantInfo> k1Constants, List<FunctionInfo> k1Functions,
            List<ConstantInfo> k2Constants, List<FunctionInfo> k2Functions)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:733-769
            // Original: def generate_scriptdefs(k1_constants: list[dict], k1_functions: list[dict], k2_constants: list[dict], k2_functions: list[dict]) -> str:
            var sb = new StringBuilder();
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using CSharpKOTOR.Common;");
            sb.AppendLine("using CSharpKOTOR.Common.Script;");
            sb.AppendLine();
            sb.AppendLine("namespace CSharpKOTOR.Common.Script");
            sb.AppendLine("{");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// NWScript constant and function definitions for KOTOR and TSL.");
            sb.AppendLine("    /// Generated from k1_nwscript.nss and tsl_nwscript.nss using GenerateScriptDefs tool.");
            sb.AppendLine("    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/scriptdefs.py");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class ScriptDefs");
            sb.AppendLine("    {");
            sb.AppendLine("        // Built-in object constants (not defined in NSS files but used as defaults)");
            sb.AppendLine("        public const int OBJECT_SELF = 0;");
            sb.AppendLine("        public const int OBJECT_INVALID = 1;");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// KOTOR (Knights of the Old Republic) script constants.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly List<ScriptConstant> KOTOR_CONSTANTS = new List<ScriptConstant>()");
            sb.AppendLine("        {");
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/scriptdefs.py:14-15
            // Original: ScriptConstant(DataType.INT, "TRUE", 1), ScriptConstant(DataType.INT, "FALSE", 0),
            // Add built-in TRUE and FALSE constants (not in NSS files but used in scripts)
            sb.AppendLine("        new ScriptConstant(DataType.Int, \"TRUE\", 1),");
            sb.AppendLine("        new ScriptConstant(DataType.Int, \"FALSE\", 0),");
            foreach (var constant in k1Constants)
            {
                sb.AppendLine(GenerateConstantCSharp(constant));
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// TSL (The Sith Lords) script constants.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly List<ScriptConstant> TSL_CONSTANTS = new List<ScriptConstant>()");
            sb.AppendLine("        {");
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/scriptdefs.py:1507-1508
            // Original: ScriptConstant(DataType.INT, "TRUE", 1), ScriptConstant(DataType.INT, "FALSE", 0),
            // Add built-in TRUE and FALSE constants (not in NSS files but used in scripts)
            sb.AppendLine("        new ScriptConstant(DataType.Int, \"TRUE\", 1),");
            sb.AppendLine("        new ScriptConstant(DataType.Int, \"FALSE\", 0),");
            foreach (var constant in k2Constants)
            {
                sb.AppendLine(GenerateConstantCSharp(constant));
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// KOTOR (Knights of the Old Republic) script functions.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly List<ScriptFunction> KOTOR_FUNCTIONS = new List<ScriptFunction>()");
            sb.AppendLine("        {");
            foreach (var func in k1Functions)
            {
                sb.AppendLine(GenerateFunctionCSharp(func, k1Constants));
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// TSL (The Sith Lords) script functions.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly List<ScriptFunction> TSL_FUNCTIONS = new List<ScriptFunction>()");
            sb.AppendLine("        {");
            foreach (var func in k2Functions)
            {
                sb.AppendLine(GenerateFunctionCSharp(func, k2Constants));
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public static void Main(string[] args)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/scripts/generate_scriptdefs.py:772-795
            // Original: def main():
            // Calculate repo root: from scripts/GenerateScriptDefs/bin/Debug/net8.0/ go up to repo root
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string repoRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));
            string k1Nss = Path.Combine(repoRoot, "vendor", "DeNCS", "k1_nwscript.nss");
            string k2Nss = Path.Combine(repoRoot, "vendor", "DeNCS", "tsl_nwscript.nss");
            string outputFile = Path.Combine(repoRoot, "src", "CSharpKOTOR", "Common", "Script", "ScriptDefs.cs");

            // Verify files exist
            if (!File.Exists(k1Nss))
            {
                Console.WriteLine($"Error: K1 NSS file not found at {k1Nss}");
                Environment.Exit(1);
            }
            if (!File.Exists(k2Nss))
            {
                Console.WriteLine($"Error: K2 NSS file not found at {k2Nss}");
                Environment.Exit(1);
            }

            Console.WriteLine($"Parsing {k1Nss}...");
            var (k1Constants, k1Functions) = ParseNssFile(k1Nss, Game.K1);
            Console.WriteLine($"  Found {k1Constants.Count} constants and {k1Functions.Count} functions");

            Console.WriteLine($"Parsing {k2Nss}...");
            var (k2Constants, k2Functions) = ParseNssFile(k2Nss, Game.K2);
            Console.WriteLine($"  Found {k2Constants.Count} constants and {k2Functions.Count} functions");

            Console.WriteLine($"Generating {outputFile}...");
            string content = GenerateScriptDefs(k1Constants, k1Functions, k2Constants, k2Functions);

            File.WriteAllText(outputFile, content, Encoding.UTF8);
            Console.WriteLine($"Done! Generated {outputFile}");
        }

        private class ConstantInfo
        {
            public string DataType { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private class FunctionInfo
        {
            public string ReturnType { get; set; }
            public string Name { get; set; }
            public List<ParamInfo> Params { get; set; }
            public string Description { get; set; }
            public string Raw { get; set; }
        }

        private class ParamInfo
        {
            public string Type { get; set; }
            public string Name { get; set; }
            public string Default { get; set; }
        }
    }
}
