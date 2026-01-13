using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS.Compiler;

namespace Andastra.Script
{
    /// <summary>
    /// Utility class to parse nwscript.nss files and extract function and constant definitions.
    /// Used by the compiler to support custom nwscript.nss files.
    /// </summary>
    public static class NwscriptParser
    {
        /// <summary>
        /// Parses an nwscript.nss file and extracts function and constant definitions.
        /// </summary>
        /// <param name="nwscriptPath">Path to the nwscript.nss file</param>
        /// <param name="game">Game version (K1 or K2) for context</param>
        /// <returns>Tuple containing lists of constants and functions</returns>
        public static (List<ScriptConstant> constants, List<ScriptFunction> functions) ParseNwscriptFile(
            string nwscriptPath, BioWareGame game)
        {
            if (!File.Exists(nwscriptPath))
            {
                throw new FileNotFoundException($"nwscript.nss file not found: {nwscriptPath}");
            }

            string content = File.ReadAllText(nwscriptPath, Encoding.UTF8);
            string processedContent = PreprocessNss(content);
            var lines = content.Split('\n').ToList();

            var lexer = new NssLexer();
            int result = lexer.Analyse(processedContent);
            if (result != 0)
            {
                throw new Exception($"Failed to tokenize nwscript.nss file: {nwscriptPath}");
            }

            var constants = new List<ScriptConstant>();
            var functions = new List<ScriptFunction>();

            int i = 0;
            while (i < lexer.Tokens.Count)
            {
                var constResult = ParseConstantFromTokens(lexer.Tokens, i, lines);
                if (constResult.HasValue)
                {
                    var constInfo = constResult.Value.info;
                    DataType dataType = ConvertDataType(constInfo.DataType);
                    object value = ConvertConstantValue(constInfo.DataType, constInfo.Value);
                    constants.Add(new ScriptConstant(dataType, constInfo.Name, value));
                    i = constResult.Value.nextIdx;
                    continue;
                }

                var funcResult = ParseFunctionFromTokens(lexer.Tokens, i, lines);
                if (funcResult.HasValue)
                {
                    var funcInfo = funcResult.Value.info;
                    DataType returnType = ConvertDataType(funcInfo.ReturnType);
                    var parameters = funcInfo.Params.Select(p => new ScriptParam(
                        ConvertDataType(p.DataType),
                        p.Name,
                        p.DefaultValue != null ? ConvertConstantValue(p.DataType, p.DefaultValue) : null
                    )).ToList();
                    functions.Add(new ScriptFunction(returnType, funcInfo.Name, parameters, "", ""));
                    i = funcResult.Value.nextIdx;
                    continue;
                }

                i++;
            }

            return (constants, functions);
        }

        private static string PreprocessNss(string content)
        {
            // Remove preprocessor directives and comments for parsing
            var lines = content.Split('\n');
            var processed = new List<string>();
            bool inBlockComment = false;

            foreach (string line in lines)
            {
                string processedLine = line;

                // Handle block comments
                if (inBlockComment)
                {
                    int endComment = processedLine.IndexOf("*/");
                    if (endComment >= 0)
                    {
                        processedLine = processedLine.Substring(endComment + 2);
                        inBlockComment = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                while (processedLine.Contains("/*"))
                {
                    int startComment = processedLine.IndexOf("/*");
                    int endComment = processedLine.IndexOf("*/", startComment + 2);
                    if (endComment >= 0)
                    {
                        processedLine = processedLine.Substring(0, startComment) + processedLine.Substring(endComment + 2);
                    }
                    else
                    {
                        processedLine = processedLine.Substring(0, startComment);
                        inBlockComment = true;
                        break;
                    }
                }

                // Remove line comments
                int lineComment = processedLine.IndexOf("//");
                if (lineComment >= 0)
                {
                    processedLine = processedLine.Substring(0, lineComment);
                }

                // Remove preprocessor directives
                if (processedLine.TrimStart().StartsWith("#"))
                {
                    continue;
                }

                processed.Add(processedLine);
            }

            return string.Join("\n", processed);
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

        private static string TokenTypeToDataType(NssTokenBase token)
        {
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

        private static (ConstantInfo info, int nextIdx)? ParseConstantFromTokens(
            List<NssTokenBase> tokens, int startIdx, List<string> lines)
        {
            int idx = SkipWhitespace(tokens, startIdx);
            if (idx >= tokens.Count)
            {
                return null;
            }

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
            if (!(nameToken is NssIdentifier || nameToken is NssKeyword))
            {
                return null;
            }

            string name = ExtractNameFromToken(nameToken);
            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count || !(tokens[idx] is NssOperator assignOp && assignOp.Operator == NssOperators.Assignment))
            {
                return null;
            }

            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count)
            {
                return null;
            }

            var valueTokens = new List<NssTokenBase>();
            while (idx < tokens.Count)
            {
                if (tokens[idx] is NssSeparator semicolon && semicolon.Separator == NssSeparators.Semicolon)
                {
                    break;
                }
                valueTokens.Add(tokens[idx]);
                idx++;
            }

            if (idx >= tokens.Count || !(tokens[idx] is NssSeparator semicolon2 && semicolon2.Separator == NssSeparators.Semicolon))
            {
                return null;
            }

            string value = string.Join("", valueTokens.Select(t => t.ToString()));

            return (new ConstantInfo
            {
                DataType = datatype,
                Name = name,
                Value = value
            }, idx + 1);
        }

        private static (FunctionInfo info, int nextIdx)? ParseFunctionFromTokens(
            List<NssTokenBase> tokens, int startIdx, List<string> lines)
        {
            int idx = SkipWhitespace(tokens, startIdx);
            if (idx >= tokens.Count)
            {
                return null;
            }

            if (!(tokens[idx] is NssKeyword typeKw && TokenTypeToDataType(tokens[idx]) != null) &&
                !(tokens[idx] is NssKeyword voidKw && voidKw.Keyword == NssKeywords.Void))
            {
                return null;
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

            idx = SkipWhitespace(tokens, idx + 1);
            if (idx >= tokens.Count || !(tokens[idx] is NssSeparator semicolon && semicolon.Separator == NssSeparators.Semicolon))
            {
                return null;
            }

            var @params = ParseFunctionParams(paramTokens);

            return (new FunctionInfo
            {
                ReturnType = returnType,
                Name = name,
                Params = @params
            }, idx + 1);
        }

        private static List<ParamInfo> ParseFunctionParams(List<NssTokenBase> paramTokens)
        {
            var @params = new List<ParamInfo>();

            if (paramTokens.Count == 0)
            {
                return @params;
            }

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

            foreach (var group in paramGroups)
            {
                var cleanGroup = group.Where(t => !(t is NssSeparator s &&
                    (s.Separator == NssSeparators.Space || s.Separator == NssSeparators.NewLine || s.Separator == NssSeparators.Tab))).ToList();

                if (cleanGroup.Count < 2)
                {
                    continue;
                }

                if (cleanGroup[0] is NssKeyword typeKw && TokenTypeToDataType(cleanGroup[0]) != null &&
                    cleanGroup[1] is NssIdentifier paramName)
                {
                    string paramType = TokenTypeToDataType(cleanGroup[0]);
                    string name = paramName.Identifier;
                    string defaultValue = null;

                    if (cleanGroup.Count > 3 && cleanGroup[2] is NssOperator assignOp && assignOp.Operator == NssOperators.Assignment)
                    {
                        var defaultTokens = cleanGroup.Skip(3).ToList();
                        defaultValue = string.Join("", defaultTokens.Select(t => t.ToString()));
                    }

                    @params.Add(new ParamInfo
                    {
                        DataType = paramType,
                        Name = name,
                        DefaultValue = defaultValue
                    });
                }
            }

            return @params;
        }

        private static string ExtractNameFromToken(NssTokenBase token)
        {
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

        private static DataType ConvertDataType(string datatype)
        {
            switch (datatype?.ToLower())
            {
                case "int": return DataType.Int;
                case "float": return DataType.Float;
                case "string": return DataType.String;
                case "void": return DataType.Void;
                case "object": return DataType.Object;
                case "vector": return DataType.Vector;
                case "location": return DataType.Location;
                case "effect": return DataType.Effect;
                case "event": return DataType.Event;
                case "talent": return DataType.Talent;
                case "action": return DataType.Action;
                default: throw new ArgumentException($"Unknown data type: {datatype}");
            }
        }

        private static object ConvertConstantValue(string datatype, string value)
        {
            value = value.Trim();
            switch (datatype?.ToLower())
            {
                case "int":
                    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(value, 16);
                    }
                    return int.Parse(value);
                case "float":
                    return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                case "string":
                    // Remove quotes
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        return value.Substring(1, value.Length - 2);
                    }
                    return value;
                default:
                    return value;
            }
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
        }

        private class ParamInfo
        {
            public string DataType { get; set; }
            public string Name { get; set; }
            public string DefaultValue { get; set; }
        }
    }
}
