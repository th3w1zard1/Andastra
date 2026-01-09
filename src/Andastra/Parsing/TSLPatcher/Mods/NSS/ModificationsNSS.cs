extern alias ResourceNCS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.NCS;
using NCSCompiler = ResourceNCS::Andastra.Parsing.Formats.NCS.Compiler.NCSCompiler;
using EntryPointError = ResourceNCS::Andastra.Parsing.Formats.NCS.Compiler.NSS.EntryPointError;
using ResourceNCSPatchLogger = ResourceNCS::Andastra.Parsing.Logger.PatchLogger;
using KnownExternalCompilers = ResourceNCS::Andastra.Parsing.Formats.NCS.NCSDecomp.KnownExternalCompilers;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using JetBrains.Annotations;

namespace Andastra.Parsing.Mods.NSS
{

    /// <summary>
    /// Mutable string wrapper for token replacement in NSS files.
    /// 1:1 port from Python MutableString in pykotor/tslpatcher/mods/nss.py
    /// </summary>
    public class MutableString
    {
        public string Value { get; set; }

        public MutableString(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;
    }

    /// <summary>
    /// Container for NSS (script source) modifications.
    /// 1:1 port from Python ModificationsNSS in pykotor/tslpatcher/mods/nss.py
    /// </summary>
    public class ModificationsNSS : PatcherModifications
    {
        public new const string DEFAULT_DESTINATION = "Override";
        public static string DefaultDestination => DEFAULT_DESTINATION;

        public new string Action { get; set; } = "Compile";
        public new bool SkipIfNotReplace { get; set; } = true;
        [CanBeNull]
        public string NwnnsscompPath { get; set; }
        [CanBeNull]
        public string TempScriptFolder { get; set; }

        public ModificationsNSS(string filename, bool replaceFile = false)
            : base(filename, replaceFile)
        {
            SaveAs = Path.ChangeExtension(filename, ".ncs");
        }

        public override object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger, BioWareGame game)
        {
            if (source is null)
            {
                logger.AddError("Invalid nss source provided to ModificationsNSS.PatchResource()");
                return true;
            }

            // Decode the NSS source bytes
            string sourceText = Encoding.GetEncoding("windows-1252").GetString(source);
            var mutableSource = new MutableString(sourceText);
            Apply(mutableSource, memory, logger, game);

            // Compile the modified NSS source to NCS bytecode
            if (Action.Equals("Compile", StringComparison.OrdinalIgnoreCase))
            {
                string tempFolder = TempScriptFolder is null ? Path.GetTempPath() : TempScriptFolder;
                Directory.CreateDirectory(tempFolder);
                string tempScriptFile = Path.Combine(tempFolder, SourceFile);
                File.WriteAllText(tempScriptFile, mutableSource.Value, Encoding.GetEncoding("windows-1252"));

                // Try built-in compiler first
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                bool builtInSucceeded = false;
                byte[] compiledBytes = null;

                try
                {
                    Andastra.Parsing.Formats.NCS.NCS ncs = NCSAuto.CompileNss(
                        mutableSource.Value,
                        game,
                        null,
                        new List<string> { tempFolder },
                        false);
                    compiledBytes = NCSAuto.BytesNcs(ncs);
                    return compiledBytes;
                }
                catch (EntryPointError e)
                {
                    logger.AddNote(e.Message);
                    return true;
                }
                catch (Exception e)
                {
                    logger.AddError($"Built-in compilation failed for '{SourceFile}': {e.Message}");
                }

                // If built-in failed and on Windows, try external compiler
                if (!builtInSucceeded && isWindows)
                {
                    bool nwnnsscompExists = !string.IsNullOrEmpty(NwnnsscompPath) && File.Exists(NwnnsscompPath);
                    if (!nwnnsscompExists)
                    {
                        logger.AddNote("nwnnsscomp.exe was not found in the 'tslpatchdata' folder, using the built-in compilers...");
                    }
                    else
                    {
                        logger.AddError($"An error occurred while compiling '{SourceFile}' with the built-in compiler, trying external compiler...");
                    }

                    if (nwnnsscompExists)
                    {
                        try
                        {
                            // Convert TSLPatcher.PatchLogger to Resource.NCS.PatchLogger
                            var resourceNcsLogger = new ResourceNCSPatchLogger();
                            // Copy log messages from TSLPatcher logger to Resource.NCS logger
                            // TODO: HACK - Logger conversion needed between TSLPatcher and Resource.NCS
                            var externalCompiler = new NCSCompiler(NwnnsscompPath, tempFolder, resourceNcsLogger);
                            // TODO: STUB - GetInfo() method not implemented in NCSCompiler
                            // For now, just validate the compiler exists
                            bool isValidCompiler = externalCompiler.ValidateCompiler();
                            if (!isValidCompiler)
                            {
                                logger.AddWarning(
                                    "The nwnnsscomp.exe in the tslpatchdata folder validation failed.\n" +
                                    "Using external compiler anyway.\n" +
                                    "PyKotor will compile regardless, but this may not yield the expected result.");
                            }

                            compiledBytes = CompileWithExternal(tempScriptFile, externalCompiler, logger, game);
                            if (compiledBytes != null)
                            {
                                return compiledBytes;
                            }
                        }
                        catch (Exception e)
                        {
                            logger.AddError(e.Message);
                        }
                    }
                }
                else if (!isWindows)
                {
                    logger.AddNote($"Patching from a unix operating system, compiling '{SourceFile}' using the built-in compilers...");
                }

                // Return compiled bytes if built-in succeeded, otherwise return source
                if (compiledBytes != null)
                {
                    return compiledBytes;
                }

                logger.AddWarning($"Could not compile '{SourceFile}'. Returning uncompiled NSS source.");
                return Encoding.GetEncoding("windows-1252").GetBytes(mutableSource.Value);
            }

            // If not compiling, just return the modified source
            return Encoding.GetEncoding("windows-1252").GetBytes(mutableSource.Value);
        }

        public override void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger, BioWareGame game)
        {
            if (mutableData is MutableString nssSource)
            {
                IterateAndReplaceTokens2DA("2DAMEMORY", memory.Memory2DA, nssSource, logger);
                IterateAndReplaceTokensStr("StrRef", memory.MemoryStr, nssSource, logger);
            }
            else
            {
                logger.AddError($"Expected MutableString for ModificationsNSS, but got {mutableData.GetType().Name}");
            }
        }

        private void IterateAndReplaceTokens2DA(string tokenName, Dictionary<int, string> memoryDict, MutableString nssSource, PatchLogger logger)
        {
            string searchPattern = $@"#{tokenName}\d+#";
            Match match = Regex.Match(nssSource.Value, searchPattern);

            while (match.Success)
            {
                int start = match.Index;
                int end = start + match.Length;

                // Extract the token ID from the match (e.g., #2DAMEMORY5# -> 5)
                string tokenIdStr = nssSource.Value.Substring(start + tokenName.Length + 1, end - start - tokenName.Length - 2);
                int tokenId = int.Parse(tokenIdStr);

                if (!memoryDict.ContainsKey(tokenId))
                {
                    throw new KeyNotFoundException($"{tokenName}{tokenId} was not defined before use in '{SourceFile}'");
                }

                string replacementValue = memoryDict[tokenId];
                logger.AddVerbose($"{SourceFile}: Replacing '#{tokenName}{tokenId}#' with '{replacementValue}'");
                nssSource.Value = nssSource.Value.Substring(0, start) + replacementValue + nssSource.Value.Substring(end);

                match = Regex.Match(nssSource.Value, searchPattern);
            }
        }

        private void IterateAndReplaceTokensStr(string tokenName, Dictionary<int, int> memoryDict, MutableString nssSource, PatchLogger logger)
        {
            string searchPattern = $@"#{tokenName}\d+#";
            Match match = Regex.Match(nssSource.Value, searchPattern);

            while (match.Success)
            {
                int start = match.Index;
                int end = start + match.Length;

                // Extract the token ID from the match (e.g., #2DAMEMORY5# -> 5)
                string tokenIdStr = nssSource.Value.Substring(start + tokenName.Length + 1, end - start - tokenName.Length - 2);
                int tokenId = int.Parse(tokenIdStr);

                if (!memoryDict.ContainsKey(tokenId))
                {
                    throw new KeyNotFoundException($"{tokenName}{tokenId} was not defined before use in '{SourceFile}'");
                }

                int replacementValue = memoryDict[tokenId];
                logger.AddVerbose($"{SourceFile}: Replacing '#{tokenName}{tokenId}#' with '{replacementValue}'");
                nssSource.Value = nssSource.Value.Substring(0, start) + replacementValue.ToString() + nssSource.Value.Substring(end);

                match = Regex.Match(nssSource.Value, searchPattern);
            }
        }

        private byte[] CompileWithExternal(
            string tempScriptFile,
            NCSCompiler nwnnsscompiler,
            PatchLogger logger, BioWareGame game)
        {
            try
            {
                // Read NSS source from file
                string nssSource = File.ReadAllText(tempScriptFile, Encoding.GetEncoding("windows-1252"));
                string filename = Path.GetFileName(tempScriptFile);
                
                // Use NCSCompiler.Compile which handles external compilation internally
                byte[] compiledBytes = nwnnsscompiler.Compile(nssSource, filename, game);
                
                return compiledBytes;
            }
            catch (EntryPointError)
            {
                return null;
            }
        }
    }
}


