using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Compiler;
using BioWare.NET.Resource.Formats.NCS.Decomp;
using BioWare.NET.Resource.Formats.NCS.Decomp.Utils;
using BioWare.NET.Resource.Formats.NCS.Optimizers;
using BioWare.NET.Common.Script;
using Andastra.Script;
using JetBrains.Annotations;
using NcsFile = BioWare.NET.Resource.Formats.NCS.Decomp.NcsFile;
using FileDecompiler = BioWare.NET.Resource.Formats.NCS.Decomp.FileDecompiler;

namespace Andastra
{

    /// <summary>
    /// Auto-loading functions for NCS files.
    /// </summary>
    public static class NCSAuto
    {
        /// <summary>
        /// Parse #include directives from NSS source code.
        /// Matches nwnnsscomp.exe behavior of selective include processing.
        /// </summary>
        private static List<string> ParseIncludeDirectives(string source)
        {
            var includes = new List<string>();
            var lines = source.Split('\n');

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#include"))
                {
                    // Extract include file name from #include "filename" or #include <filename>
                    var startQuote = trimmed.IndexOf('"');
                    if (startQuote == -1)
                    {
                        startQuote = trimmed.IndexOf('<');
                    }

                    if (startQuote != -1)
                    {
                        var endChar = trimmed[startQuote] == '"' ? '"' : '>';
                        var endQuote = trimmed.IndexOf(endChar, startQuote + 1);
                        if (endQuote != -1)
                        {
                            var includeFile = trimmed.Substring(startQuote + 1, endQuote - startQuote - 1);
                            // Remove .nss extension if present
                            if (includeFile.EndsWith(".nss"))
                            {
                                includeFile = includeFile.Substring(0, includeFile.Length - 4);
                            }
                            if (!includes.Contains(includeFile))
                            {
                                includes.Add(includeFile);
                            }
                        }
                    }
                }
            }

            return includes;
        }

        /// <summary>
        /// Analyzes NSS source code to identify which symbols (functions and constants) are actually used.
        /// Performs comprehensive analysis to match nwnnsscomp.exe's selective loading behavior.
        /// </summary>
        private static SymbolUsageInfo AnalyzeSymbolUsageInSource(string source)
        {
            var usage = new SymbolUsageInfo();
            var lines = source.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip #include directives (they're handled separately)
                if (trimmed.StartsWith("#include"))
                {
                    continue;
                }

                // Skip comments
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*"))
                {
                    continue;
                }

                // Extract symbol usage from the line
                ExtractSymbolUsageFromLine(trimmed, usage);
            }

            return usage;
        }

        /// <summary>
        /// Extracts function calls and constant references from a line of NSS source code.
        /// Uses pattern matching to identify function calls (identifier followed by '(') and constants (all uppercase identifiers).
        /// </summary>
        private static void ExtractSymbolUsageFromLine(string line, SymbolUsageInfo usage)
        {
            // Use regex to find identifiers that could be function calls or constants
            // Function calls: identifier followed by '(' (possibly with whitespace)
            // Constants: all uppercase identifiers (possibly with underscores and digits)

            // First, handle function calls - look for identifier( pattern
            var functionCallPattern = new Regex(@"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\(");
            var functionMatches = functionCallPattern.Matches(line);
            foreach (Match match in functionMatches)
            {
                string functionName = match.Groups[1].Value;
                if (!usage.usedFunctions.Contains(functionName))
                {
                    usage.usedFunctions.Add(functionName);
                }
            }

            // Then, handle constants - all uppercase identifiers that aren't function calls
            // Look for identifiers that are all uppercase and not followed by '('
            var constantPattern = new Regex(@"\b([A-Z][A-Z0-9_]*)\b(?!\s*\()");
            var constantMatches = constantPattern.Matches(line);
            foreach (Match match in constantMatches)
            {
                string constantName = match.Groups[1].Value;
                // Filter out keywords and already-identified function names
                if (!IsKeyword(constantName) &&
                    !usage.usedFunctions.Contains(constantName) &&
                    !usage.usedConstants.Contains(constantName))
                {
                    usage.usedConstants.Add(constantName);
                }
            }
        }

        /// <summary>
        /// Checks if an identifier is a reserved keyword in NSS.
        /// </summary>
        private static bool IsKeyword(string identifier)
        {
            var keywords = new HashSet<string>
            {
                "int", "float", "string", "void", "object", "vector", "location",
                "effect", "event", "talent", "action", "itemproperty", "struct",
                "if", "else", "for", "while", "do", "switch", "case", "default",
                "break", "continue", "return", "const"
            };
            return keywords.Contains(identifier.ToLowerInvariant());
        }

        /// <summary>
        /// Parses an include file (NSS source) to extract its function and constant definitions.
        /// Uses NwscriptParser by writing content to a temporary file.
        /// </summary>
        private static (List<ScriptFunction> functions, List<ScriptConstant> constants) ParseIncludeFileSymbols(
            string includeFileSource,
            BioWareGame game)
        {
            try
            {
                // Write content to temporary file and use NwscriptParser
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".nss");
                File.WriteAllText(tempFile, includeFileSource, Encoding.UTF8);

                try
                {
                    var parsed = NwscriptParser.ParseNwscriptFile(tempFile, game);
                    return (parsed.functions, parsed.constants);
                }
                finally
                {
                    // Clean up temporary file
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch
            {
                // If parsing fails for any reason, return empty lists (will fallback to full file)
                return (new List<ScriptFunction>(), new List<ScriptConstant>());
            }
        }


        /// <summary>
        /// Filters a list of functions to only include those that are in the used names list.
        /// Always includes essential functions that nwnnsscomp.exe includes by default.
        /// </summary>
        private static List<ScriptFunction> FilterSymbols(
            List<ScriptFunction> allFunctions, List<string> usedNames)
        {
            var essentialFunctions = new HashSet<string>
            {
                "main", "StartingConditional", "GetLastPerceived", "GetEnteringObject",
                "GetExitingObject", "GetIsDead", "GetHitDice", "GetTag", "GetName"
            };

            var filtered = new List<ScriptFunction>();
            var usedSet = new HashSet<string>(usedNames, StringComparer.OrdinalIgnoreCase);

            foreach (var func in allFunctions)
            {
                if (essentialFunctions.Contains(func.Name) || usedSet.Contains(func.Name))
                {
                    filtered.Add(func);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Filters a list of constants to only include those that are in the used names list.
        /// Always includes essential constants.
        /// </summary>
        private static List<ScriptConstant> FilterSymbols(
            List<ScriptConstant> allConstants, List<string> usedNames)
        {
            var essentialConstants = new HashSet<string>
            {
                "TRUE", "FALSE", "OBJECT_INVALID", "OBJECT_SELF"
            };

            var filtered = new List<ScriptConstant>();
            var usedSet = new HashSet<string>(usedNames, StringComparer.OrdinalIgnoreCase);

            foreach (var constant in allConstants)
            {
                if (essentialConstants.Contains(constant.Name) || usedSet.Contains(constant.Name))
                {
                    filtered.Add(constant);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Generates filtered NSS source code containing only the specified functions and constants.
        /// Matches the format expected by the NSS parser.
        /// </summary>
        private static string GenerateFilteredNssSource(
            List<ScriptFunction> functions, List<ScriptConstant> constants)
        {
            var lines = new List<string>();

            // Add constants first
            foreach (var constant in constants)
            {
                string valueStr = FormatConstantValue(constant.Value, constant.DataType);
                lines.Add($"{constant.DataType.ToScriptString()} {constant.Name} = {valueStr};");
            }

            // Add functions
            foreach (var function in functions)
            {
                string paramStr = string.Join(", ", function.Params.Select(p => p.ToString()));
                lines.Add($"{function.ReturnType.ToScriptString()} {function.Name}({paramStr});");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Formats a constant value for NSS source code output.
        /// </summary>
        private static string FormatConstantValue(object value, DataType dataType)
        {
            if (value == null)
            {
                return "0";
            }

            switch (dataType)
            {
                case DataType.Int:
                    return value.ToString();
                case DataType.Float:
                    return ((float)value).ToString("F", System.Globalization.CultureInfo.InvariantCulture);
                case DataType.String:
                    return "\"" + value.ToString().Replace("\"", "\\\"") + "\"";
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Information about symbol usage in source code.
        /// </summary>
        private class SymbolUsageInfo
        {
            public List<string> usedFunctions = new List<string>();
            public List<string> usedConstants = new List<string>();
        }

        /// <summary>
        /// Returns an NCS instance from the source.
        ///
        /// Args:
        ///     source: The source of the data (file path, byte array, or stream).
        ///     offset: The byte offset of the file inside the data.
        ///     size: Number of bytes to allowed to read from the stream. If not specified, uses the whole stream.
        ///
        /// Raises:
        ///     InvalidDataException: If the file was corrupted or in an unsupported format.
        ///
        /// Returns:
        ///     An NCS instance.
        /// </summary>
        public static NCS ReadNcs(string filepath, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(filepath, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        public static NCS ReadNcs(byte[] data, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(data, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        public static NCS ReadNcs(Stream source, int offset = 0, int? size = null)
        {
            using (var reader = new NCSBinaryReader(source, offset, size ?? 0))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Writes the NCS data to the target location with the specified format (NCS only).
        ///
        /// Args:
        ///     ncs: The NCS file being written.
        ///     target: The location to write the data to (file path or stream).
        ///     fileFormat: The file format (currently only NCS is supported).
        ///
        /// Raises:
        ///     ArgumentException: If an unsupported file format was given.
        /// </summary>
        public static void WriteNcs(NCS ncs, string filepath, [CanBeNull] ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            byte[] data = new NCSBinaryWriter(ncs).Write();
            System.IO.File.WriteAllBytes(filepath, data);
        }

        public static void WriteNcs(NCS ncs, [CanBeNull] Stream target, ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            byte[] data = new NCSBinaryWriter(ncs).Write();
            target.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Returns the NCS data in the specified format (NCS only) as a byte array.
        ///
        /// This is a convenience method that wraps the WriteNcs() method.
        ///
        /// Args:
        ///     ncs: The target NCS object.
        ///     fileFormat: The file format (currently only NCS is supported).
        ///
        /// Raises:
        ///     ArgumentException: If an unsupported file format was given.
        ///
        /// Returns:
        ///     The NCS data as a byte array.
        /// </summary>
        public static byte[] BytesNcs(NCS ncs, [CanBeNull] ResourceType fileFormat = null)
        {
            fileFormat = fileFormat ?? ResourceType.NCS;
            if (fileFormat != ResourceType.NCS)
            {
                throw new ArgumentException("Unsupported format specified; use NCS.", nameof(fileFormat));
            }

            return new NCSBinaryWriter(ncs).Write();
        }

        /// <summary>
        /// Compile NSS source code to NCS bytecode.
        ///
        /// Args:
        ///     source: The NSS source code string to compile
        ///     game: Target game (K1 or TSL) - determines which function/constant definitions to use
        ///     library: Optional dictionary of include file names to their byte content.
        ///             If not provided, uses selective loading from nwscript.nss only (matches nwnnsscomp.exe behavior)
        ///     optimizers: Optional list of post-compilation optimizers to apply (nwnnsscomp.exe applies none)
        ///     libraryLookup: Paths to search for #include files
        ///     errorlog: Optional error logger for parser (not yet implemented in C#)
        ///     debug: Enable debug output from parser
        ///     nwscriptPath: Optional path to nwscript.nss file. If provided, functions and constants will be parsed from this file.
        ///                   If not provided, falls back to ScriptDefs.KOTOR_FUNCTIONS/CONSTANTS or ScriptDefs.TSL_FUNCTIONS/CONSTANTS
        ///
        /// Returns:
        ///     NCS: Compiled NCS bytecode object
        ///
        /// Raises:
        ///     CompileError: If source code has syntax errors or semantic issues
        ///     EntryPointError: If script has no main() or StartingConditional() entry point
        ///
        /// Note:
        ///     Unlike previous versions, this now matches nwnnsscomp.exe behavior:
        ///     - Only includes functions/constants that are actually referenced
        ///     - Does not dump entire library files exhaustively
        ///     - Applies no default optimizations (nwnnsscomp.exe produces optimized bytecode directly)
        /// </summary>
        public static NCS CompileNss(
            string source, BioWareGame game,
            Dictionary<string, byte[]> library = null,
            List<NCSOptimizer> optimizers = null,
            [CanBeNull] List<string> libraryLookup = null,
            [CanBeNull] object errorlog = null,
            bool debug = false,
            [CanBeNull] string nwscriptPath = null)
        {
            List<ScriptFunction> functions = null;
            List<ScriptConstant> constants = null;

            // Parse nwscript.nss if provided
            if (!string.IsNullOrEmpty(nwscriptPath))
            {
                try
                {
                    var parsed = NwscriptParser.ParseNwscriptFile(nwscriptPath, game);
                    functions = parsed.functions;
                    constants = parsed.constants;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse nwscript.nss file: {nwscriptPath}. Error: {ex.Message}", ex);
                }
            }

            // IMPLEMENTATION: Selective include loading (matches nwnnsscomp.exe behavior)
            // nwnnsscomp.exe does NOT dump entire library files exhaustively.
            // It only includes functions/constants that are actually referenced.
            var compiler = new NssCompiler(game, libraryLookup, debug, functions, constants);

            // If no library provided, use selective loading from nwscript.nss only
            if (library == null)
            {
                // Old pykotor logic: includes the whole library
                //library = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;

                // nwnnsscomp.exe logic: only includes functions/constants that are actually referenced
                // Parse #include directives from source to determine what needs to be loaded
                var includeFiles = ParseIncludeDirectives(source);
                var selectiveLibrary = new Dictionary<string, byte[]>();

                // Only load nwscript.nss initially (matches nwnnsscomp.exe behavior)
                var baseLibrary = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;
                if (baseLibrary.ContainsKey("nwscript"))
                {
                    selectiveLibrary["nwscript"] = baseLibrary["nwscript"];
                }

                // For other includes, perform selective symbol extraction based on usage analysis
                // This matches nwnnsscomp.exe behavior of only including symbols that are actually referenced
                var symbolUsage = AnalyzeSymbolUsageInSource(source);
                foreach (var includeFile in includeFiles)
                {
                    if (baseLibrary.ContainsKey(includeFile))
                    {
                        // Parse the include file to extract its functions and constants
                        byte[] includeFileContent = baseLibrary[includeFile];
                        string includeFileSource = Encoding.UTF8.GetString(includeFileContent);
                        var includeSymbols = ParseIncludeFileSymbols(includeFileSource, game);

                        // If parsing succeeded and we have symbols, filter them
                        if (includeSymbols.functions.Count > 0 || includeSymbols.constants.Count > 0)
                        {
                            // Filter to only include symbols that are actually used in the source code
                            var filteredFunctions = FilterSymbols(includeSymbols.functions, symbolUsage.usedFunctions);
                            var filteredConstants = FilterSymbols(includeSymbols.constants, symbolUsage.usedConstants);

                            // Generate filtered NSS source code with only the used symbols
                            string filteredSource = GenerateFilteredNssSource(filteredFunctions, filteredConstants);
                            selectiveLibrary[includeFile] = Encoding.UTF8.GetBytes(filteredSource);
                        }
                        else
                        {
                            // If parsing failed, fall back to full file (matches original behavior)
                            selectiveLibrary[includeFile] = includeFileContent;
                        }
                    }
                }

                library = selectiveLibrary;
            }
            NCS ncs = compiler.Compile(source, library);

            // Always remove NOP instructions to match external compiler behavior
            // The external compiler (nwnnsscomp.exe) does not produce NOP instructions in its output
            // Matching PyKotor behavior: automatically add RemoveNopOptimizer if not in the list
            List<NCSOptimizer> optimizersToApply = optimizers != null ? new List<NCSOptimizer>(optimizers) : new List<NCSOptimizer>();
            bool hasRemoveNop = optimizersToApply.Any(o => o is RemoveNopOptimizer);
            if (!hasRemoveNop)
            {
                optimizersToApply.Insert(0, new RemoveNopOptimizer());
            }

            // Apply optimizers
            if (optimizersToApply.Count > 0)
            {
                foreach (NCSOptimizer optimizer in optimizersToApply)
                {
                    optimizer.Reset();
                }
                ncs.Optimize(optimizersToApply);
            }

            if (System.Environment.GetEnvironmentVariable("NCS_INTERPRETER_DEBUG") == "true")
            {
                System.Console.WriteLine("=== NCS after optimize ===");
                for (int i = 0; i < ncs.Instructions.Count; i++)
                {
                    NCSInstruction inst = ncs.Instructions[i];
                    int jumpIdx = inst.Jump != null ? ncs.GetInstructionIndex(inst.Jump) : -1;
                    System.Console.WriteLine($"{i}: {inst.InsType} args=[{string.Join(",", inst.Args ?? new List<object>())}] jumpIdx={jumpIdx}");
                }
            }

            return ncs;
        }

        /// <summary>
        /// Decompile NCS bytecode to NSS source code.
        /// Uses the DeNCS decompiler (1:1 port from vendor/DeNCS) for accurate decompilation.
        /// </summary>
        public static string DecompileNcs(
            [CanBeNull] NCS ncs, BioWareGame game,
            List<ScriptFunction> functions = null,
            [CanBeNull] List<ScriptConstant> constants = null,
            [CanBeNull] string nwscriptPath = null)
        {
            if (ncs == null)
            {
                throw new ArgumentNullException(nameof(ncs));
            }

            // Use FileDecompiler (DeNCS port) for 1:1 accurate decompilation
            // FileDecompiler is in Resource assembly (not ResourceNCS), use NCS from Resource assembly
            FileDecompiler fileDecompiler;
            if (!string.IsNullOrEmpty(nwscriptPath) && File.Exists(nwscriptPath))
            {
                // Use nwscript file directly if provided
                var nwscriptFile = new NcsFile(nwscriptPath);
                fileDecompiler = new FileDecompiler(nwscriptFile);
            }
            else
            {
                // Fall back to lazy loading (will search for nwscript file)
                NWScriptLocator.GameType gameType = game.IsK2() ? NWScriptLocator.GameType.TSL : NWScriptLocator.GameType.K1;
                fileDecompiler = new FileDecompiler(null, gameType);
            }

            FileScriptData data = null;
            try
            {
                data = fileDecompiler.DecompileNcsObject(ncs);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Decompilation failed with exception: " + ex.Message +
                    (ex.InnerException != null ? " (Inner: " + ex.InnerException.Message + ")" : "") +
                    ". FileDecompiler returned null. " +
                    "This usually means the decompiler couldn't analyze the NCS bytecode structure. " +
                    "Check console output for detailed error messages.", ex);
            }

            if (data == null)
            {
                throw new InvalidOperationException(
                    "Decompilation failed - FileDecompiler returned null. " +
                    "This usually means the decompiler couldn't analyze the NCS bytecode structure. " +
                    "Possible causes: no main subroutine found, actions file not loaded, or exception during decompilation. " +
                    "Check console output for detailed error messages.");
            }

            data.GenerateCode();
            string code = data.GetCode();

            // Clean up
            data.Close();
            fileDecompiler.CloseAllFiles();

            return code ?? string.Empty;
        }
    }
}
