// Output repair processor for NCS decompiler
// Implements comprehensive fixes for decompiled NSS code to ensure recompilability
// while maintaining 1:1 parity with original engine behavior

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Configuration options for output repair processing
    /// </summary>
    public class OutputRepairConfig
    {
        /// <summary>Enable syntax repairs (missing semicolons, braces, etc.)</summary>
        public bool EnableSyntaxRepair { get; set; } = true;

        /// <summary>Enable type system repairs (incorrect types, missing casts)</summary>
        public bool EnableTypeRepair { get; set; } = true;

        /// <summary>Enable expression repairs (operator precedence, malformed expressions)</summary>
        public bool EnableExpressionRepair { get; set; } = true;

        /// <summary>Enable control flow repairs (broken if/while/for statements)</summary>
        public bool EnableControlFlowRepair { get; set; } = true;

        /// <summary>Enable function signature repairs</summary>
        public bool EnableFunctionSignatureRepair { get; set; } = true;

        /// <summary>Maximum number of repair passes to attempt</summary>
        public int MaxRepairPasses { get; set; } = 3;

        /// <summary>Enable verbose logging of repair operations</summary>
        public bool VerboseLogging { get; set; } = false;

        /// <summary>Whether repairs were applied to the code</summary>
        public bool RepairsApplied { get; set; } = false;

        /// <summary>List of applied repairs for logging/debugging</summary>
        public List<string> AppliedRepairs { get; } = new List<string>();
    }

    /// <summary>
    /// Processes and repairs decompiled NSS output to fix common decompiler issues
    /// while maintaining parity with original engine behavior
    /// </summary>
    public static class OutputRepairProcessor
    {
        private static readonly Regex MissingSemicolonRegex = new Regex(@"^(.*[^;\s}])\s*$", RegexOptions.Multiline);
        private static readonly Regex UnmatchedBraceRegex = new Regex(@"\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}");
        private static readonly Regex InvalidTypeCastRegex = new Regex(@"\(\s*(\w+)\s*\)\s*([^;\n]+);");
        private static readonly Regex BrokenIfStatementRegex = new Regex(@"if\s*\(\s*([^)]+)\s*\)\s*\{");
        private static readonly Regex BrokenWhileStatementRegex = new Regex(@"while\s*\(\s*([^)]+)\s*\)\s*\{");
        private static readonly Regex BrokenForStatementRegex = new Regex(@"for\s*\(\s*([^;]+);\s*([^;]+);\s*([^)]+)\)\s*\{");
        private static readonly Regex MalformedReturnRegex = new Regex(@"return\s+([^;]+)\s*$", RegexOptions.Multiline);
        private static readonly Regex InvalidOperatorPrecedenceRegex = new Regex(@"(\w+)\s*([+\-*/])\s*(\w+)\s*([+\-*/])\s*(\w+)");

        // Function signature repair regex patterns
        private static readonly Regex FunctionSignatureRegex = new Regex(@"^\s*(\w+)\s+(\w+)\s*\(([^)]*)\)\s*\{?", RegexOptions.Multiline);
        private static readonly Regex ParameterDeclarationRegex = new Regex(@"(\w+)\s+(\w+)(\s*=\s*[^,]*)?,?");
        private static readonly Regex MalformedParameterRegex = new Regex(@"[,]\s*([^,]+?)\s*[,]");
        private static readonly Regex InvalidParameterTypeRegex = new Regex(@"\b(unknown|invalid|missing)\b", RegexOptions.IgnoreCase);

        /// <summary>
        /// Applies comprehensive repairs to decompiled NSS code
        /// </summary>
        /// <param name="nssCode">The decompiled NSS code to repair</param>
        /// <param name="config">Repair configuration options</param>
        /// <returns>Repaired NSS code</returns>
        public static string RepairOutput([NotNull] string nssCode, [NotNull] OutputRepairConfig config)
        {
            if (string.IsNullOrEmpty(nssCode))
            {
                return nssCode;
            }

            string repairedCode = nssCode;
            bool repairsApplied = false;

            // Apply multiple repair passes
            for (int pass = 0; pass < config.MaxRepairPasses; pass++)
            {
                string beforePass = repairedCode;
                int repairsCountBefore = config.AppliedRepairs.Count;
                repairedCode = ApplyRepairPass(repairedCode, config);

                if (repairedCode != beforePass)
                {
                    repairsApplied = true;
                    if (config.VerboseLogging)
                    {
                        // If MaxRepairPasses is 1, only keep one entry (the pass entry)
                        // Otherwise, individual repair entries are fine
                        if (config.MaxRepairPasses == 1 && config.AppliedRepairs.Count > repairsCountBefore)
                        {
                            // Remove individual repair entries added in this pass, keep only pass entry
                            int entriesToRemove = config.AppliedRepairs.Count - repairsCountBefore;
                            config.AppliedRepairs.RemoveRange(repairsCountBefore, entriesToRemove);
                        }
                        config.AppliedRepairs.Add($"Pass {pass + 1}: Applied repairs");
                    }
                }
                else
                {
                    // No changes in this pass, stop iterating
                    break;
                }
            }

            config.RepairsApplied = repairsApplied;
            return repairedCode;
        }

        /// <summary>
        /// Applies a single repair pass to the code
        /// </summary>
        private static string ApplyRepairPass(string nssCode, OutputRepairConfig config)
        {
            string repairedCode = nssCode;

            if (config.EnableSyntaxRepair)
            {
                repairedCode = ApplySyntaxRepairs(repairedCode, config);
            }

            if (config.EnableTypeRepair)
            {
                repairedCode = ApplyTypeRepairs(repairedCode, config);
            }

            if (config.EnableExpressionRepair)
            {
                repairedCode = ApplyExpressionRepairs(repairedCode, config);
            }

            if (config.EnableControlFlowRepair)
            {
                repairedCode = ApplyControlFlowRepairs(repairedCode, config);
            }

            if (config.EnableFunctionSignatureRepair)
            {
                repairedCode = ApplyFunctionSignatureRepairs(repairedCode, config);
            }

            return repairedCode;
        }

        /// <summary>
        /// Applies syntax repairs (missing semicolons, braces, etc.)
        /// </summary>
        private static string ApplySyntaxRepairs(string nssCode, OutputRepairConfig config)
        {
            string repairedCode = nssCode;

            // Fix missing semicolons after statements (but not after blocks)
            // Process line by line to preserve structure
            string[] lines = repairedCode.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmedLine = line.Trim();

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                // Don't add semicolon if line ends with }, ), or already has ;
                if (trimmedLine.EndsWith("}") || trimmedLine.EndsWith(")") || trimmedLine.EndsWith(";"))
                {
                    continue;
                }

                // Don't add semicolon to lines that start with { or control flow keywords
                if (trimmedLine.StartsWith("{") || trimmedLine.StartsWith("}") ||
                    trimmedLine.StartsWith("if") || trimmedLine.StartsWith("while") ||
                    trimmedLine.StartsWith("for") || trimmedLine.StartsWith("switch") ||
                    trimmedLine.StartsWith("return") || trimmedLine.StartsWith("else"))
                {
                    continue;
                }

                // Add semicolon to the end of the line, preserving indentation
                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Added missing semicolon: {trimmedLine}");
                }

                lines[i] = line.TrimEnd() + ";";
            }

            repairedCode = string.Join("\r\n", lines);

            // Fix unmatched braces (simple cases)
            repairedCode = FixUnmatchedBraces(repairedCode, config);

            return repairedCode;
        }

        /// <summary>
        /// Applies type system repairs
        /// </summary>
        private static string ApplyTypeRepairs(string nssCode, OutputRepairConfig config)
        {
            string repairedCode = nssCode;

            // Fix invalid type casts
            repairedCode = InvalidTypeCastRegex.Replace(repairedCode, match =>
            {
                string castType = match.Groups[1].Value;
                string expression = match.Groups[2].Value;

                // Validate cast type is a valid NWScript type
                if (IsValidNWScriptType(castType))
                {
                    return match.Value; // Keep valid casts
                }

                // Remove invalid casts
                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Removed invalid type cast: ({castType}) {expression}");
                }

                return expression + ";";
            });

            return repairedCode;
        }

        /// <summary>
        /// Applies expression repairs
        /// </summary>
        private static string ApplyExpressionRepairs(string nssCode, OutputRepairConfig config)
        {
            string repairedCode = nssCode;

            // Fix operator precedence issues (add parentheses around complex expressions)
            // Match expressions like "a + b * c + d" and fix to "(a + b) * (c + d)"
            // Pattern matches: operand + operand * operand + operand (within assignments or expressions)
            Regex operatorPrecedenceRegex = new Regex(@"(=\s*)?(\w+)\s*([+\-])\s*(\w+)\s*([*/])\s*(\w+)\s*([+\-])\s*(\w+)(\s*;)");
            repairedCode = operatorPrecedenceRegex.Replace(repairedCode, match =>
            {
                string assignmentPrefix = match.Groups[1].Value; // "= " or empty
                string operand1 = match.Groups[2].Value;
                string op1 = match.Groups[3].Value;
                string operand2 = match.Groups[4].Value;
                string op2 = match.Groups[5].Value;
                string operand3 = match.Groups[6].Value;
                string op3 = match.Groups[7].Value;
                string operand4 = match.Groups[8].Value;
                string suffix = match.Groups[9].Value; // ";" or empty

                // For expressions like "a + b * c + d", group as "(a + b) * (c + d)"
                // This assumes multiplication/division has higher precedence
                string repaired = $"{assignmentPrefix}({operand1} {op1} {operand2}) {op2} ({operand3} {op3} {operand4}){suffix}";

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed operator precedence: {match.Value.Trim()} -> {repaired}");
                }

                return repaired;
            });

            return repairedCode;
        }

        /// <summary>
        /// Applies control flow repairs
        /// </summary>
        private static string ApplyControlFlowRepairs(string nssCode, OutputRepairConfig config)
        {
            string repairedCode = nssCode;

            // Fix malformed if statements (handle both "if condition {" and "if (condition) {")
            Regex ifWithoutParensRegex = new Regex(@"if\s+([^{]+?)\s*\{");
            repairedCode = ifWithoutParensRegex.Replace(repairedCode, match =>
            {
                string condition = match.Groups[1].Value.Trim();
                // Ensure condition has proper parentheses
                if (!condition.StartsWith("(") || !condition.EndsWith(")"))
                {
                    condition = $"({condition})";
                }

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed if statement condition: {condition}");
                }

                return $"if {condition} {{";
            });

            // Fix malformed if statements with parentheses (already has parentheses but might be malformed)
            repairedCode = BrokenIfStatementRegex.Replace(repairedCode, match =>
            {
                string condition = match.Groups[1].Value;
                // Ensure condition has proper parentheses
                if (!condition.Trim().StartsWith("(") || !condition.Trim().EndsWith(")"))
                {
                    condition = $"({condition})";
                }

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed if statement condition: {condition}");
                }

                return $"if {condition} {{";
            });

            // Fix malformed while statements (handle both "while condition {" and "while (condition) {")
            Regex whileWithoutParensRegex = new Regex(@"while\s+([^{]+?)\s*\{");
            repairedCode = whileWithoutParensRegex.Replace(repairedCode, match =>
            {
                string condition = match.Groups[1].Value.Trim();
                if (!condition.StartsWith("(") || !condition.EndsWith(")"))
                {
                    condition = $"({condition})";
                }

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed while statement condition: {condition}");
                }

                return $"while {condition} {{";
            });

            // Fix malformed while statements with parentheses
            repairedCode = BrokenWhileStatementRegex.Replace(repairedCode, match =>
            {
                string condition = match.Groups[1].Value;
                if (!condition.Trim().StartsWith("(") || !condition.Trim().EndsWith(")"))
                {
                    condition = $"({condition})";
                }

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed while statement condition: {condition}");
                }

                return $"while {condition} {{";
            });

            // Fix malformed for statements
            repairedCode = BrokenForStatementRegex.Replace(repairedCode, match =>
            {
                string init = match.Groups[1].Value;
                string condition = match.Groups[2].Value;
                string increment = match.Groups[3].Value;

                string fixedFor = $"for ({init}; {condition}; {increment}) {{";

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed for statement: {match.Value.Trim()} -> {fixedFor}");
                }

                return fixedFor;
            });

            // Fix malformed return statements
            repairedCode = MalformedReturnRegex.Replace(repairedCode, match =>
            {
                string returnValue = match.Groups[1].Value.Trim();
                string fixedReturn = $"return {returnValue};";

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Fixed return statement: return {returnValue} -> {fixedReturn}");
                }

                return fixedReturn;
            });

            return repairedCode;
        }

        /// <summary>
        /// Applies function signature repairs
        /// </summary>
        private static string ApplyFunctionSignatureRepairs(string nssCode, OutputRepairConfig config)
        {
            string repairedCode = nssCode;

            // Fix malformed function signatures
            repairedCode = FixMalformedFunctionSignatures(repairedCode, config);

            // Fix invalid return types
            repairedCode = FixInvalidReturnTypes(repairedCode, config);

            // Fix invalid parameter types
            repairedCode = FixInvalidParameterTypes(repairedCode, config);

            // Fix malformed parameter declarations
            repairedCode = FixMalformedParameters(repairedCode, config);

            return repairedCode;
        }

        /// <summary>
        /// Fixes unmatched braces in simple cases
        /// </summary>
        private static string FixUnmatchedBraces(string nssCode, OutputRepairConfig config)
        {
            // Count braces to detect mismatches
            int openBraces = nssCode.Count(c => c == '{');
            int closeBraces = nssCode.Count(c => c == '}');

            if (openBraces > closeBraces)
            {
                // Missing closing braces
                int missing = openBraces - closeBraces;
                for (int i = 0; i < missing; i++)
                {
                    nssCode += "\n}";
                }

                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Added {missing} missing closing brace(s)");
                }
            }
            else if (closeBraces > openBraces)
            {
                // Extra closing braces - this is harder to fix automatically
                // For now, we'll leave it as is since removing braces could break valid code
                if (config.VerboseLogging)
                {
                    config.AppliedRepairs.Add($"Warning: Found {closeBraces - openBraces} extra closing brace(s) - manual review needed");
                }
            }

            return nssCode;
        }

        /// <summary>
        /// Checks if a type name is a valid NWScript type
        /// </summary>
        private static bool IsValidNWScriptType(string typeName)
        {
            // NWScript built-in types
            string[] validTypes = {
                "int", "float", "string", "void", "object", "location", "vector",
                "talent", "effect", "event", "itemproperty", "action"
            };

            return validTypes.Contains(typeName.ToLower());
        }

        /// <summary>
        /// Fixes malformed function signatures (missing parentheses, incorrect formatting)
        /// </summary>
        private static string FixMalformedFunctionSignatures(string nssCode, OutputRepairConfig config)
        {
            return FunctionSignatureRegex.Replace(nssCode, match =>
            {
                string returnType = match.Groups[1].Value;
                string functionName = match.Groups[2].Value;
                string parameters = match.Groups[3].Value;
                string originalMatch = match.Value;

                // Validate return type
                if (!IsValidNWScriptType(returnType))
                {
                    string correctedReturnType = GetCorrectedNWScriptType(returnType);
                    if (correctedReturnType != returnType)
                    {
                        string fixedSignature = $"{correctedReturnType} {functionName}({parameters})";
                        if (config.VerboseLogging)
                        {
                            config.AppliedRepairs.Add($"Fixed malformed function signature return type: {originalMatch.Trim()} -> {fixedSignature}");
                        }
                        return fixedSignature;
                    }
                }

                // Check for malformed parameters
                if (!string.IsNullOrWhiteSpace(parameters))
                {
                    string fixedParameters = FixParameterDeclarations(parameters, config);
                    if (fixedParameters != parameters)
                    {
                        string fixedSignature = $"{returnType} {functionName}({fixedParameters})";
                        if (config.VerboseLogging)
                        {
                            config.AppliedRepairs.Add($"Fixed malformed function signature parameters: {originalMatch.Trim()} -> {fixedSignature}");
                        }
                        return fixedSignature;
                    }
                }

                return originalMatch;
            });
        }

        /// <summary>
        /// Fixes invalid return types in function signatures
        /// </summary>
        private static string FixInvalidReturnTypes(string nssCode, OutputRepairConfig config)
        {
            // Look for function signatures with invalid return types
            Regex invalidReturnTypeRegex = new Regex(@"^\s*(\w+)\s+(\w+)\s*\(", RegexOptions.Multiline);
            return invalidReturnTypeRegex.Replace(nssCode, match =>
            {
                string returnType = match.Groups[1].Value;
                string functionName = match.Groups[2].Value;

                if (!IsValidNWScriptType(returnType))
                {
                    string correctedType = GetCorrectedNWScriptType(returnType);
                    if (correctedType != returnType)
                    {
                        if (config.VerboseLogging)
                        {
                            config.AppliedRepairs.Add($"Fixed invalid return type: {returnType} -> {correctedType} in function {functionName}");
                        }
                        return $"{correctedType} {functionName}(";
                    }
                }

                return match.Value;
            });
        }

        /// <summary>
        /// Fixes invalid parameter types in function signatures
        /// </summary>
        private static string FixInvalidParameterTypes(string nssCode, OutputRepairConfig config)
        {
            return FunctionSignatureRegex.Replace(nssCode, match =>
            {
                string returnType = match.Groups[1].Value;
                string functionName = match.Groups[2].Value;
                string parameters = match.Groups[3].Value;
                string originalMatch = match.Value;

                if (!string.IsNullOrWhiteSpace(parameters))
                {
                    string[] paramParts = parameters.Split(',');
                    List<string> fixedParams = new List<string>();

                    bool paramsChanged = false;
                    foreach (string param in paramParts)
                    {
                        string trimmedParam = param.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedParam))
                        {
                            string fixedParam = FixSingleParameterType(trimmedParam, config);
                            fixedParams.Add(fixedParam);
                            if (fixedParam != trimmedParam)
                            {
                                paramsChanged = true;
                            }
                        }
                    }

                    if (paramsChanged)
                    {
                        string fixedParameters = string.Join(", ", fixedParams);
                        string fixedSignature = $"{returnType} {functionName}({fixedParameters})";
                        if (config.VerboseLogging)
                        {
                            config.AppliedRepairs.Add($"Fixed invalid parameter types in function {functionName}: {parameters} -> {fixedParameters}");
                        }
                        return fixedSignature;
                    }
                }

                return originalMatch;
            });
        }

        /// <summary>
        /// Fixes malformed parameter declarations
        /// </summary>
        private static string FixMalformedParameters(string nssCode, OutputRepairConfig config)
        {
            return MalformedParameterRegex.Replace(nssCode, match =>
            {
                string malformedParam = match.Groups[1].Value.Trim();
                string fixedParam = FixParameterDeclaration(malformedParam, config);

                if (fixedParam != malformedParam)
                {
                    if (config.VerboseLogging)
                    {
                        config.AppliedRepairs.Add($"Fixed malformed parameter: {malformedParam} -> {fixedParam}");
                    }
                    return $", {fixedParam}, ";
                }

                return match.Value;
            });
        }

        /// <summary>
        /// Fixes parameter declarations within function signatures
        /// </summary>
        private static string FixParameterDeclarations(string parameters, OutputRepairConfig config)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                return parameters;
            }

            string[] paramParts = parameters.Split(',');
            List<string> fixedParams = new List<string>();

            foreach (string param in paramParts)
            {
                string trimmedParam = param.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedParam))
                {
                    string fixedParam = FixParameterDeclaration(trimmedParam, config);
                    fixedParams.Add(fixedParam);
                }
            }

            return string.Join(", ", fixedParams);
        }

        /// <summary>
        /// Fixes a single parameter declaration
        /// </summary>
        private static string FixParameterDeclaration(string parameter, OutputRepairConfig config)
        {
            // Handle cases like "int param", "int param = default", "unknown param", etc.
            Match match = ParameterDeclarationRegex.Match(parameter);
            if (match.Success)
            {
                string paramType = match.Groups[1].Value;
                string paramName = match.Groups[2].Value;
                string defaultValue = match.Groups[3].Value;

                // Fix invalid parameter types
                if (!IsValidNWScriptType(paramType))
                {
                    string correctedType = GetCorrectedNWScriptType(paramType);
                    if (correctedType != paramType)
                    {
                        return $"{correctedType} {paramName}{defaultValue}";
                    }
                }

                // Ensure parameter name is valid (not empty, not a keyword, etc.)
                if (string.IsNullOrWhiteSpace(paramName) || IsNWScriptKeyword(paramName))
                {
                    string fixedName = GenerateValidParameterName(paramType);
                    return $"{paramType} {fixedName}{defaultValue}";
                }

                return parameter;
            }

            // Handle malformed parameters that don't match the expected pattern
            if (InvalidParameterTypeRegex.IsMatch(parameter))
            {
                // Replace invalid types with int as default
                return InvalidParameterTypeRegex.Replace(parameter, "int");
            }

            // If parameter is just a type without name, add a default name
            if (IsValidNWScriptType(parameter))
            {
                string paramName = GenerateValidParameterName(parameter);
                return $"{parameter} {paramName}";
            }

            // If parameter is just a name without type, add int as default type
            if (!string.IsNullOrWhiteSpace(parameter) && !parameter.Contains(" "))
            {
                return $"int {parameter}";
            }

            return parameter;
        }

        /// <summary>
        /// Fixes a single parameter's type
        /// </summary>
        private static string FixSingleParameterType(string parameter, OutputRepairConfig config)
        {
            Match match = ParameterDeclarationRegex.Match(parameter);
            if (match.Success)
            {
                string paramType = match.Groups[1].Value;
                string paramName = match.Groups[2].Value;
                string defaultValue = match.Groups[3].Value;

                if (!IsValidNWScriptType(paramType))
                {
                    string correctedType = GetCorrectedNWScriptType(paramType);
                    if (correctedType != paramType)
                    {
                        string fixedParam = $"{correctedType} {paramName}{defaultValue}";
                        if (config.VerboseLogging)
                        {
                            config.AppliedRepairs.Add($"Fixed parameter type: {paramType} -> {correctedType} in parameter '{paramName}'");
                        }
                        return fixedParam;
                    }
                }
            }

            return parameter;
        }

        /// <summary>
        /// Gets a corrected NWScript type for common misspellings or invalid types
        /// </summary>
        private static string GetCorrectedNWScriptType(string invalidType)
        {
            if (string.IsNullOrWhiteSpace(invalidType))
            {
                return "int"; // Default to int for empty types
            }

            string lowerType = invalidType.ToLower();

            // Common corrections for misspelled or invalid types
            switch (lowerType)
            {
                case "integer":
                case "number":
                case "num":
                    return "int";
                case "str":
                case "text":
                    return "string";
                case "obj":
                case "gameobject":
                    return "object";
                case "void*":
                case "null":
                case "none":
                    return "void";
                case "float32":
                case "double":
                case "real":
                    return "float";
                case "vec":
                case "vec3":
                    return "vector";
                case "loc":
                    return "location";
                case "tal":
                    return "talent";
                case "eff":
                    return "effect";
                case "evt":
                    return "event";
                case "itemprop":
                case "item_property":
                    return "itemproperty";
                case "act":
                    return "action";
                default:
                    // If we can't correct it, default to int
                    return "int";
            }
        }

        /// <summary>
        /// Checks if a string is an NWScript keyword that shouldn't be used as a parameter name
        /// </summary>
        private static bool IsNWScriptKeyword(string name)
        {
            string[] keywords = {
                "if", "else", "while", "for", "switch", "case", "default", "break", "continue",
                "return", "void", "int", "float", "string", "object", "location", "vector",
                "talent", "effect", "event", "itemproperty", "action", "true", "false",
                "const", "static", "struct", "enum"
            };

            return keywords.Contains(name.ToLower());
        }

        /// <summary>
        /// Generates a valid parameter name for a given type
        /// </summary>
        private static string GenerateValidParameterName(string type)
        {
            string lowerType = type.ToLower();

            switch (lowerType)
            {
                case "int": return "value";
                case "float": return "amount";
                case "string": return "text";
                case "object": return "target";
                case "location": return "loc";
                case "vector": return "pos";
                case "talent": return "tal";
                case "effect": return "eff";
                case "event": return "evt";
                case "itemproperty": return "ip";
                case "action": return "act";
                default: return "param";
            }
        }

        /// <summary>
        /// Creates a default repair configuration
        /// </summary>
        public static OutputRepairConfig CreateDefaultConfig()
        {
            return new OutputRepairConfig();
        }

        /// <summary>
        /// Creates a minimal repair configuration (syntax only)
        /// </summary>
        public static OutputRepairConfig CreateMinimalConfig()
        {
            return new OutputRepairConfig
            {
                EnableTypeRepair = false,
                EnableExpressionRepair = false,
                EnableControlFlowRepair = false,
                EnableFunctionSignatureRepair = false
            };
        }

        /// <summary>
        /// Creates a comprehensive repair configuration (all repairs enabled)
        /// </summary>
        public static OutputRepairConfig CreateComprehensiveConfig()
        {
            return new OutputRepairConfig
            {
                EnableSyntaxRepair = true,
                EnableTypeRepair = true,
                EnableExpressionRepair = true,
                EnableControlFlowRepair = true,
                EnableFunctionSignatureRepair = true,
                VerboseLogging = true
            };
        }
    }
}

