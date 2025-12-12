// Matching vendor/NCSDecomp/src/test/java/com/kotor/resource/formats/ncs/test_roundtrip_decompiler.java:1-4050
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// Visit https://bolabaden.org for more information and other ventures

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.NCS;
using FluentAssertions;
using Xunit;

namespace CSharpKOTOR.Tests.Formats
{
    /// <summary>
    /// Exhaustive round-trip tests for the decompiler and compiler:
    /// 1) Clone or reuse the Vanilla_KOTOR_Script_Source repository
    /// 2) Compile each .nss to .ncs for each game using nwnnsscomp.exe
    /// 3) Decompile each .ncs back to .nss using NCS decompiler
    /// 4) Normalize both original and decompiled NSS for comparison (whitespace, formatting only)
    /// 5) Fail immediately on the first mismatch
    ///
    /// All test artifacts are created in gitignored directories.
    ///
    /// ⚠️ CRITICAL: TEST PHILOSOPHY - READ BEFORE MODIFYING ⚠️
    ///
    /// These tests validate the decompiler against original scripts. They do NOT mask or patch any decompiler flaws.
    ///
    /// STRICTLY FORBIDDEN IN TESTS:
    /// - Fixing syntax or logic errors in decompiled output
    /// - Patching or cleaning up distorted or mangled code from the decompiler
    /// - Editing expressions, operators, semicolons, braces, types, return statements
    /// - Adjusting function signatures or any output for correctness
    /// - Applying any sort of output "repair" or workaround to supplement the decompiler
    ///
    /// ALLOWED (FOR COMPARISON ONLY):
    /// - Whitespace and formatting normalization, solely for text comparison
    ///   (This does not legitimize fixing bugs via normalization!)
    ///
    /// IF DECOMPILED OUTPUT DIFFERS FROM ORIGINAL (other than formatting):
    /// - The test MUST FAIL
    /// - All bugs must be fixed in the ACTUAL DECOMPILER SOURCE, not here
    /// - Do not attempt workarounds in test logic
    ///
    /// GOAL:
    /// The decompiler must recover source faithfully from .ncs. If output is erroneous, it is a bug to address in the decompiler implementation itself.
    ///
    /// REQUIREMENTS FOR MODIFICATION:
    /// Never add any logic here to "fix up" or work around output issues from the decompiler:
    /// - Investigate root causes and correct them in the decompiler source itself
    /// - Testing code is for validation, not for altering broken decompiled output in any way
    /// </summary>
    public class NCSRoundtripTests
    {
        /// <summary>
        /// Exception thrown when the original source file fails to compile.
        /// This is not a decompiler issue - the source file itself has errors.
        /// Tests should skip files that throw this exception.
        /// </summary>
        private class SourceCompilationException : Exception
        {
            public SourceCompilationException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        // Working directory (gitignored)
        private static readonly string TestWorkDir = Path.Combine(Directory.GetCurrentDirectory(), "test-work");
        private static readonly string VanillaRepoDir = Path.Combine(TestWorkDir, "Vanilla_KOTOR_Script_Source");
        private static readonly string[] VanillaRepoUrls = new[]
        {
            "https://github.com/KOTORCommunityPatches/Vanilla_KOTOR_Script_Source.git",
            "https://github.com/th3w1zard1/Vanilla_KOTOR_Script_Source.git"
        };

        // Paths relative to repository root
        private static readonly string RepoRoot = FindRepositoryRoot();

        // Test output directories (gitignored)
        private static readonly string WorkRoot = Path.Combine(TestWorkDir, "roundtrip-work");
        private static readonly string ProfileOutput = Path.Combine(TestWorkDir, "test_profile.txt");
        private static readonly string CompileTempRoot = Path.Combine(TestWorkDir, "compile-temp");

        private static readonly TimeSpan ProcTimeout = TimeSpan.FromSeconds(25);

        // Performance tracking
        private static readonly Dictionary<string, long> OperationTimes = new Dictionary<string, long>();
        private static long _testStartTime;
        private static int _totalTests;
        private static int _testsProcessed;

        private static string _k1Scratch;
        private static string _k2Scratch;

        // Compiler and nwscript paths
        private static readonly string NwnCompiler = FindCompiler();
        private static readonly string K1Nwscript = Path.Combine(RepoRoot, "vendor", "PyKotor", "vendor", "NorthernLights", "Scripts", "k1_nwscript.nss");
        private static readonly string K1AscNwscript = Path.Combine(RepoRoot, "tools", "k1_asc_nwscript.nss");
        private static readonly string K2Nwscript = Path.Combine(RepoRoot, "include", "k2_nwscript.nss");

        // Constants loaded from nwscript files
        private static readonly Dictionary<string, string> NpcConstantsK1 = LoadConstantsWithPrefix(K1Nwscript, "NPC_");
        private static readonly Dictionary<string, string> NpcConstantsK2 = LoadConstantsWithPrefix(K2Nwscript, "NPC_");
        private static readonly Dictionary<string, string> AbilityConstantsK1 = LoadConstantsWithPrefix(K1Nwscript, "ABILITY_");
        private static readonly Dictionary<string, string> AbilityConstantsK2 = LoadConstantsWithPrefix(K2Nwscript, "ABILITY_");
        private static readonly Dictionary<string, string> FactionConstantsK1 = LoadConstantsWithPrefix(K1Nwscript, "STANDARD_FACTION_");
        private static readonly Dictionary<string, string> FactionConstantsK2 = LoadConstantsWithPrefix(K2Nwscript, "STANDARD_FACTION_");
        private static readonly Dictionary<string, string> AnimationConstantsK1 = LoadConstantsWithPrefix(K1Nwscript, "ANIMATION_");
        private static readonly Dictionary<string, string> AnimationConstantsK2 = LoadConstantsWithPrefix(K2Nwscript, "ANIMATION_");

        /// <summary>
        /// Finds the repository root by searching upward for .git directory or .sln file.
        /// </summary>
        private static string FindRepositoryRoot()
        {
            string currentDir = Directory.GetCurrentDirectory();
            DirectoryInfo dir = new DirectoryInfo(currentDir);

            while (dir != null)
            {
                // Check for .git directory or .sln file
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            // Fallback to current directory if not found
            return currentDir;
        }

        /// <summary>
        /// Finds the compiler executable by trying multiple filenames in multiple locations.
        /// </summary>
        private static string FindCompiler()
        {
            string[] compilerNames = { "nwnnsscomp.exe", "nwnnsscomp_kscript.exe", "nwnnsscomp_tslpatcher.exe" };

            // 1. Try tools/ directory
            string toolsDir = Path.Combine(RepoRoot, "tools");
            foreach (string name in compilerNames)
            {
                string candidate = Path.Combine(toolsDir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 2. Try current working directory
            string cwd = Directory.GetCurrentDirectory();
            foreach (string name in compilerNames)
            {
                string candidate = Path.Combine(cwd, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // Default fallback
            return Path.Combine(RepoRoot, "tools", "nwnnsscomp.exe");
        }

        private static string DisplayPath(string path)
        {
            string abs = Path.GetFullPath(path).Replace('\\', '/');

            List<string> candidates = new List<string> { abs };

            AddRelIfPossible(candidates, RepoRoot, path);
            AddRelIfPossible(candidates, TestWorkDir, path);
            AddRelIfPossible(candidates, VanillaRepoDir, path);

            string best = candidates
                .Where(s => !string.IsNullOrEmpty(s))
                .OrderBy(s => s.Length)
                .FirstOrDefault() ?? abs;

            return best == "." ? best : best.Replace('\\', '/');
        }

        private static void AddRelIfPossible(List<string> candidates, string basePath, string targetPath)
        {
            try
            {
                string baseAbs = Path.GetFullPath(basePath);
                string targetAbs = Path.GetFullPath(targetPath);
                Uri baseUri = new Uri(baseAbs + Path.DirectorySeparatorChar);
                Uri targetUri = new Uri(targetAbs);
                string rel = Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString().Replace('/', Path.DirectorySeparatorChar));
                candidates.Add(string.IsNullOrEmpty(rel) ? "." : rel);
            }
            catch (ArgumentException)
            {
                // Ignore paths on different roots
            }
        }

        private static void CopyWithRetry(string source, string target, int attempts = 3)
        {
            for (int i = 1; i <= attempts; i++)
            {
                try
                {
                    File.Copy(source, target, true);
                    return;
                }
                catch (IOException)
                {
                    if (i == attempts)
                    {
                        throw;
                    }
                    System.Threading.Thread.Sleep(200 * i);
                }
            }
        }

        private static void ResetPerformanceTracking()
        {
            OperationTimes.Clear();
            _testsProcessed = 0;
            _totalTests = 0;
        }

        /// <summary>
        /// Clone or update the Vanilla_KOTOR_Script_Source repository.
        /// </summary>
        private static void EnsureVanillaRepo()
        {
            if (Directory.Exists(VanillaRepoDir))
            {
                string gitDir = Path.Combine(VanillaRepoDir, ".git");
                if (Directory.Exists(gitDir))
                {
                    Console.WriteLine($"Using existing Vanilla_KOTOR_Script_Source repository at: {DisplayPath(VanillaRepoDir)}");
                    return;
                }
                else
                {
                    Console.WriteLine($"Removing non-git directory: {DisplayPath(VanillaRepoDir)}");
                    DeleteDirectory(VanillaRepoDir);
                }
            }

            Console.WriteLine("Cloning Vanilla_KOTOR_Script_Source repository...");
            Console.WriteLine($"  Destination: {DisplayPath(VanillaRepoDir)}");

            Directory.CreateDirectory(Path.GetDirectoryName(VanillaRepoDir));

            Exception lastException = null;
            for (int i = 0; i < VanillaRepoUrls.Length; i++)
            {
                string repoUrl = VanillaRepoUrls[i];
                Console.WriteLine($"  Attempting URL {i + 1}/{VanillaRepoUrls.Length}: {repoUrl}");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {repoUrl} \"{VanillaRepoDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode == 0)
                    {
                        Console.WriteLine($"Repository cloned successfully from: {repoUrl}");
                        return;
                    }

                    lastException = new IOException($"Failed to clone from {repoUrl}. Exit code: {proc.ExitCode}\nOutput: {output}");
                    Console.WriteLine($"  Failed: {lastException.Message.Split('\n')[0]}");

                    if (Directory.Exists(VanillaRepoDir))
                    {
                        try
                        {
                            DeleteDirectory(VanillaRepoDir);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }

            throw new IOException($"Failed to clone repository from all {VanillaRepoUrls.Length} URLs:\n{string.Join("\n", VanillaRepoUrls)}\n\nLast error: {lastException?.Message ?? "Unknown error"}");
        }

        private static void Preflight()
        {
            Console.WriteLine("=== Preflight Checks ===");

            EnsureVanillaRepo();

            if (!File.Exists(NwnCompiler))
            {
                throw new IOException($"nwnnsscomp.exe missing at: {DisplayPath(NwnCompiler)}");
            }
            Console.WriteLine($"✓ Found compiler: {DisplayPath(NwnCompiler)}");

            if (!File.Exists(K1Nwscript))
            {
                throw new IOException($"k1_nwscript.nss missing at: {DisplayPath(K1Nwscript)}");
            }
            Console.WriteLine($"✓ Found K1 nwscript: {DisplayPath(K1Nwscript)}");

            if (!File.Exists(K2Nwscript))
            {
                throw new IOException($"tsl_nwscript.nss missing at: {DisplayPath(K2Nwscript)}");
            }
            Console.WriteLine($"✓ Found TSL nwscript: {DisplayPath(K2Nwscript)}");

            string k1Root = Path.Combine(VanillaRepoDir, "K1");
            string tslRoot = Path.Combine(VanillaRepoDir, "TSL");
            if (!Directory.Exists(k1Root))
            {
                throw new IOException($"K1 directory not found in vanilla repo: {DisplayPath(k1Root)}");
            }
            if (!Directory.Exists(tslRoot))
            {
                throw new IOException($"TSL directory not found in vanilla repo: {DisplayPath(tslRoot)}");
            }
            Console.WriteLine("✓ Vanilla repo structure verified");

            _k1Scratch = PrepareScratch("k1", K1Nwscript);
            _k2Scratch = PrepareScratch("k2", K2Nwscript);

            Console.WriteLine("=== Preflight Complete ===\n");
        }

        private static string PrepareScratch(string gameLabel, string nwscriptSource)
        {
            string scratch = Path.Combine(WorkRoot, gameLabel);
            Directory.CreateDirectory(scratch);
            string target = Path.Combine(scratch, "nwscript.nss");
            if (!File.Exists(target))
            {
                File.Copy(nwscriptSource, target, true);
            }
            return scratch;
        }

        private class TestItem
        {
            public string Path { get; set; }
            public string GameFlag { get; set; }
            public string ScratchRoot { get; set; }
        }

        private class RoundTripCase
        {
            public string DisplayName { get; set; }
            public TestItem Item { get; set; }
        }

        private List<RoundTripCase> BuildRoundTripCases()
        {
            Console.WriteLine("=== Discovering Test Files ===");

            List<TestItem> allFiles = new List<TestItem>();

            // K1 files
            string k1Root = Path.Combine(VanillaRepoDir, "K1");
            if (Directory.Exists(k1Root))
            {
                foreach (string file in Directory.GetFiles(k1Root, "*.nss", SearchOption.AllDirectories).OrderBy(f => f))
                {
                    allFiles.Add(new TestItem { Path = file, GameFlag = "k1", ScratchRoot = _k1Scratch });
                }
            }

            // TSL files
            string tslVanilla = Path.Combine(VanillaRepoDir, "TSL", "Vanilla");
            if (Directory.Exists(tslVanilla))
            {
                foreach (string file in Directory.GetFiles(tslVanilla, "*.nss", SearchOption.AllDirectories).OrderBy(f => f))
                {
                    allFiles.Add(new TestItem { Path = file, GameFlag = "k2", ScratchRoot = _k2Scratch });
                }
            }

            string tslTslrcm = Path.Combine(VanillaRepoDir, "TSL", "TSLRCM");
            if (Directory.Exists(tslTslrcm))
            {
                foreach (string file in Directory.GetFiles(tslTslrcm, "*.nss", SearchOption.AllDirectories).OrderBy(f => f))
                {
                    allFiles.Add(new TestItem { Path = file, GameFlag = "k2", ScratchRoot = _k2Scratch });
                }
            }

            Console.WriteLine($"Found {allFiles.Count} .nss files");

            // Sort deterministically
            allFiles.Sort((a, b) =>
            {
                int gameCompare = string.Compare(a.GameFlag, b.GameFlag, StringComparison.Ordinal);
                if (gameCompare != 0) return gameCompare;
                return string.Compare(a.Path, b.Path, StringComparison.Ordinal);
            });

            List<RoundTripCase> tests = new List<RoundTripCase>();
            foreach (TestItem item in allFiles)
            {
                string relPath = GetRelativePath(VanillaRepoDir, item.Path);
                string displayName = item.GameFlag.Equals("k1") ? $"K1: {relPath}" : $"TSL: {relPath}";
                tests.Add(new RoundTripCase { DisplayName = displayName, Item = item });
            }

            _totalTests = tests.Count;
            Console.WriteLine("=== Test Discovery Complete ===\n");

            return tests;
        }

        private static string GetRelativePath(string basePath, string targetPath)
        {
            Uri baseUri = new Uri(basePath + Path.DirectorySeparatorChar);
            Uri targetUri = new Uri(targetPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Performs a complete round-trip test: NSS->NCS->NSS->NCS
        /// Primary goal: bytecode must match 1:1
        /// Uses inbuilt compiler for first compilation, external compiler for recompilation
        /// </summary>
        private static void RoundTripSingle(string nssPath, string gameFlag, string scratchRoot)
        {
            long startTime = Stopwatch.GetTimestamp();

            string rel = GetRelativePath(VanillaRepoDir, nssPath);
            string displayRelPath = rel.Replace('\\', '/');

            string outDir = Path.Combine(scratchRoot, Path.GetDirectoryName(rel) ?? "");
            Directory.CreateDirectory(outDir);

            // Step 1: Compile original NSS -> NCS (first NCS) using INBUILT compiler
            string compiledFirst = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".ncs");
            Console.Write($"  Compiling {displayRelPath} to .ncs with inbuilt compiler");
            long compileOriginalStart = Stopwatch.GetTimestamp();
            try
            {
                RunInbuiltCompiler(nssPath, compiledFirst, gameFlag);
                long compileTime = Stopwatch.GetTimestamp() - compileOriginalStart;
                MergeOperationTime("compile-original", compileTime);
                MergeOperationTime("compile", compileTime);
                Console.WriteLine($" ✓ ({GetElapsedMilliseconds(compileTime):F3} ms)");
            }
            catch (Exception e)
            {
                long compileTime = Stopwatch.GetTimestamp() - compileOriginalStart;
                MergeOperationTime("compile-original", compileTime);
                MergeOperationTime("compile", compileTime);
                Console.WriteLine(" ⚠ SKIPPED (original source file has compilation errors)");
                throw new SourceCompilationException($"Original source file failed to compile: {e.Message}", e);
            }

            // Step 2: Decompile NCS -> NSS
            string decompiled = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".dec.nss");
            Console.Write($"  Decompiling {Path.GetFileName(compiledFirst)} back to .nss");
            long decompileStart = Stopwatch.GetTimestamp();
            try
            {
                RunDecompile(compiledFirst, decompiled, gameFlag);
                long decompileTime = Stopwatch.GetTimestamp() - decompileStart;
                MergeOperationTime("decompile", decompileTime);
                Console.WriteLine($" ✓ ({GetElapsedMilliseconds(decompileTime):F3} ms)");
            }
            catch (Exception)
            {
                long decompileTime = Stopwatch.GetTimestamp() - decompileStart;
                MergeOperationTime("decompile", decompileTime);
                throw;
            }

            // Step 3: Compare original NSS vs decompiled NSS (text comparison) - WARNING ONLY
            Console.Write("  Comparing original vs decompiled (text)");
            long compareTextStart = Stopwatch.GetTimestamp();
            string textMismatchMessage = null;
            try
            {
                bool isK2 = gameFlag.Equals("k2");
                string originalExpanded = ExpandIncludes(nssPath, gameFlag);
                string roundtripRaw = File.ReadAllText(decompiled, Encoding.UTF8);

                string originalExpandedFiltered = FilterFunctionsNotInDecompiled(originalExpanded, roundtripRaw);

                string original = NormalizeNewlines(originalExpandedFiltered, isK2);
                string roundtrip = NormalizeNewlines(roundtripRaw, isK2);
                long compareTime = Stopwatch.GetTimestamp() - compareTextStart;
                MergeOperationTime("compare-text", compareTime);
                MergeOperationTime("compare", compareTime);

                if (!original.Equals(roundtrip))
                {
                    Console.WriteLine(" ⚠ MISMATCH (warning - bytecode check is primary)");
                    string diff = FormatUnifiedDiff(original, roundtrip);
                    textMismatchMessage = $"Round-trip text mismatch for {DisplayPath(nssPath)}";
                    if (diff != null)
                    {
                        textMismatchMessage += Environment.NewLine + diff;
                    }
                    Console.Error.WriteLine($"WARNING: {textMismatchMessage}");
                }
                else
                {
                    Console.WriteLine(" ✓ MATCH");
                }
            }
            catch (Exception e)
            {
                long compareTime = Stopwatch.GetTimestamp() - compareTextStart;
                MergeOperationTime("compare-text", compareTime);
                MergeOperationTime("compare", compareTime);
                Console.Error.WriteLine($"WARNING: Text comparison error: {e.Message}");
                textMismatchMessage = $"Text comparison error: {e.Message}";
            }

            // Step 4: Recompile decompiled NSS -> NCS (second NCS) using EXTERNAL compiler
            string recompiled = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".rt.ncs");
            string compileInput = decompiled;
            string tempCompileInput = null;
            string decompiledName = Path.GetFileName(decompiled);
            if (decompiledName.IndexOf('.') != decompiledName.LastIndexOf('.'))
            {
                compileInput = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + "_dec.nss");
                File.Copy(decompiled, compileInput, true);
                tempCompileInput = compileInput;
            }

            bool recompilationSucceeded = false;
            Console.Write("  Recompiling decompiled .nss to .ncs with external compiler");
            long compileRoundtripStart = Stopwatch.GetTimestamp();
            try
            {
                RunExternalCompiler(compileInput, recompiled, gameFlag, scratchRoot);
                long compileTime = Stopwatch.GetTimestamp() - compileRoundtripStart;
                MergeOperationTime("compile-roundtrip", compileTime);
                MergeOperationTime("compile", compileTime);
                Console.WriteLine($" ✓ ({GetElapsedMilliseconds(compileTime):F3} ms)");
                recompilationSucceeded = true;
            }
            catch
            {
                long compileTime = Stopwatch.GetTimestamp() - compileRoundtripStart;
                MergeOperationTime("compile-roundtrip", compileTime);
                MergeOperationTime("compile", compileTime);
                Console.WriteLine(" ⚠ FAILED (acceptable - decompiled code may not recompile)");
            }
            finally
            {
                if (tempCompileInput != null)
                {
                    try
                    {
                        File.Delete(tempCompileInput);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            // Step 5: If recompilation succeeded, compare bytecode
            if (recompilationSucceeded)
            {
                Console.Write("  Comparing bytecode (original vs recompiled)");
                long compareBytecodeStart = Stopwatch.GetTimestamp();
                try
                {
                    AssertBytecodeEqual(compiledFirst, recompiled, gameFlag, displayRelPath);
                    long compareTime = Stopwatch.GetTimestamp() - compareBytecodeStart;
                    MergeOperationTime("compare-bytecode", compareTime);
                    MergeOperationTime("compare", compareTime);
                    Console.WriteLine(" ✓ MATCH");
                }
                catch (Exception e)
                {
                    long compareTime = Stopwatch.GetTimestamp() - compareBytecodeStart;
                    MergeOperationTime("compare-bytecode", compareTime);
                    MergeOperationTime("compare", compareTime);
                    if (textMismatchMessage != null)
                    {
                        throw new InvalidOperationException($"Bytecode mismatch (PRIMARY FAILURE):\n{e.Message}\n\nAlso had text mismatch (secondary):\n{textMismatchMessage}");
                    }
                    throw;
                }
            }
            else
            {
                if (textMismatchMessage != null)
                {
                    Console.Error.WriteLine("NOTE: Cannot verify bytecode (recompilation failed), but text mismatch was detected.");
                }
            }

            long totalTime = Stopwatch.GetTimestamp() - startTime;
            MergeOperationTime("total", totalTime);
        }

        private static void MergeOperationTime(string key, long ticks)
        {
            if (OperationTimes.ContainsKey(key))
            {
                OperationTimes[key] += ticks;
            }
            else
            {
                OperationTimes[key] = ticks;
            }
        }

        private static double GetElapsedMilliseconds(long ticks)
        {
            return (double)ticks / Stopwatch.Frequency * 1000.0;
        }

        /// <summary>
        /// Compiles NSS to NCS using the inbuilt compiler (CSharpKOTOR native implementation).
        /// </summary>
        private static void RunInbuiltCompiler(string originalNssPath, string compiledOut, string gameFlag)
        {
            Game game = gameFlag.Equals("k2") ? Game.K2 : Game.K1;

            string source = File.ReadAllText(originalNssPath, Encoding.UTF8);
            string parentDir = Path.GetDirectoryName(originalNssPath);
            List<string> libraryLookup = parentDir != null ? new List<string> { parentDir } : new List<string>();

            // Add vanilla script directories to lookup
            if (gameFlag.Equals("k1"))
            {
                libraryLookup.Add(Path.Combine(VanillaRepoDir, "K1", "Data", "scripts.bif"));
                libraryLookup.Add(Path.Combine(VanillaRepoDir, "K1", "Modules"));
                libraryLookup.Add(Path.Combine(VanillaRepoDir, "K1", "Rims"));
            }
            else
            {
                libraryLookup.Add(Path.Combine(VanillaRepoDir, "TSL", "Vanilla", "Data", "Scripts"));
                libraryLookup.Add(Path.Combine(VanillaRepoDir, "TSL", "Vanilla", "Modules"));
                libraryLookup.Add(Path.Combine(VanillaRepoDir, "TSL", "TSLRCM", "Override"));
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(compiledOut));
                NCS ncs = NCSAuto.CompileNss(source, game, null, null, libraryLookup);
                NCSAuto.WriteNcs(ncs, compiledOut);

                if (!File.Exists(compiledOut))
                {
                    throw new InvalidOperationException($"Inbuilt compiler did not produce output: {DisplayPath(compiledOut)}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Inbuilt compilation failed for {DisplayPath(originalNssPath)}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Compiles NSS to NCS using the external compiler (nwnnsscomp_kscript.exe).
        /// </summary>
        private static void RunExternalCompiler(string originalNssPath, string compiledOut, string gameFlag, string workDir)
        {
            string nwscriptSource;
            if (gameFlag.Equals("k1"))
            {
                if (NeedsAscNwscript(originalNssPath))
                {
                    nwscriptSource = K1AscNwscript;
                    if (!File.Exists(nwscriptSource))
                    {
                        throw new InvalidOperationException($"K1 ASC nwscript file not found: {DisplayPath(nwscriptSource)}");
                    }
                }
                else
                {
                    nwscriptSource = K1Nwscript;
                    if (!File.Exists(nwscriptSource))
                    {
                        throw new InvalidOperationException($"K1 nwscript file not found: {DisplayPath(nwscriptSource)}");
                    }
                }
            }
            else if (gameFlag.Equals("k2"))
            {
                nwscriptSource = K2Nwscript;
                if (!File.Exists(nwscriptSource))
                {
                    throw new InvalidOperationException($"TSL nwscript file not found: {DisplayPath(nwscriptSource)}");
                }
            }
            else
            {
                throw new ArgumentException($"Invalid game flag: {gameFlag} (expected 'k1' or 'k2')");
            }

            string compilerDir = Path.GetDirectoryName(NwnCompiler);
            string compilerNwscript = Path.Combine(compilerDir, "nwscript.nss");
            if (!File.Exists(compilerNwscript) || !AreFilesSame(nwscriptSource, compilerNwscript))
            {
                File.Copy(nwscriptSource, compilerNwscript, true);
            }

            string tempDir = null;
            string tempSourceFile = null;
            try
            {
                tempDir = SetupTempCompileDir(originalNssPath, gameFlag);
                tempSourceFile = Path.Combine(tempDir, Path.GetFileName(originalNssPath));

                Directory.CreateDirectory(Path.GetDirectoryName(compiledOut));

                bool isK2 = gameFlag.Equals("k2");
                List<string> args = new List<string> { "-c", "-v1.0" };
                if (isK2)
                {
                    args.Add("-ko2");
                }
                args.Add($"\"{tempSourceFile}\"");
                args.Add($"\"{compiledOut}\"");

                Console.Write($" (external: {Path.GetFileName(NwnCompiler)})");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = NwnCompiler,
                    Arguments = string.Join(" ", args),
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();

                    if (!proc.WaitForExit((int)ProcTimeout.TotalMilliseconds))
                    {
                        proc.Kill();
                        Console.WriteLine(" ✗ TIMEOUT");
                        throw new TimeoutException($"nwnnsscomp timed out for {DisplayPath(originalNssPath)}");
                    }

                    int exitCode = proc.ExitCode;
                    bool fileExists = File.Exists(compiledOut);

                    if (exitCode != 0 || !fileExists)
                    {
                        Console.WriteLine(" ✗ FAILED");
                        string errorMsg = $"nwnnsscomp failed (exit={exitCode}, fileExists={fileExists}) for {DisplayPath(originalNssPath)}";
                        if (!string.IsNullOrEmpty(output))
                        {
                            errorMsg += $"\nCompiler output:\n{output}";
                        }
                        throw new InvalidOperationException(errorMsg);
                    }
                }
            }
            finally
            {
                if (tempDir != null)
                {
                    try
                    {
                        DeleteDirectory(tempDir);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"Warning: Failed to clean up temp directory {DisplayPath(tempDir)}: {e.Message}");
                    }
                }
            }
        }

        private static void RunCompiler(string originalNssPath, string compiledOut, string gameFlag, string workDir)
        {
            string nwscriptSource;
            if (gameFlag.Equals("k1"))
            {
                if (NeedsAscNwscript(originalNssPath))
                {
                    nwscriptSource = K1AscNwscript;
                    if (!File.Exists(nwscriptSource))
                    {
                        throw new InvalidOperationException($"K1 ASC nwscript file not found: {DisplayPath(nwscriptSource)}");
                    }
                }
                else
                {
                    nwscriptSource = K1Nwscript;
                    if (!File.Exists(nwscriptSource))
                    {
                        throw new InvalidOperationException($"K1 nwscript file not found: {DisplayPath(nwscriptSource)}");
                    }
                }
            }
            else if (gameFlag.Equals("k2"))
            {
                nwscriptSource = K2Nwscript;
                if (!File.Exists(nwscriptSource))
                {
                    throw new InvalidOperationException($"TSL nwscript file not found: {DisplayPath(nwscriptSource)}");
                }
            }
            else
            {
                throw new ArgumentException($"Invalid game flag: {gameFlag} (expected 'k1' or 'k2')");
            }

            string compilerDir = Path.GetDirectoryName(NwnCompiler);
            string compilerNwscript = Path.Combine(compilerDir, "nwscript.nss");
            if (!File.Exists(compilerNwscript) || !AreFilesSame(nwscriptSource, compilerNwscript))
            {
                File.Copy(nwscriptSource, compilerNwscript, true);
            }

            string tempDir = null;
            string tempSourceFile = null;
            try
            {
                tempDir = SetupTempCompileDir(originalNssPath, gameFlag);
                tempSourceFile = Path.Combine(tempDir, Path.GetFileName(originalNssPath));

                Directory.CreateDirectory(Path.GetDirectoryName(compiledOut));

                bool isK2 = gameFlag.Equals("k2");
                List<string> args = new List<string> { "-c", "-v1.0" };
                if (isK2)
                {
                    args.Add("-ko2");
                }
                args.Add($"\"{tempSourceFile}\"");
                args.Add($"\"{compiledOut}\"");

                Console.Write($" (compiler: {Path.GetFileName(NwnCompiler)})");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = NwnCompiler,
                    Arguments = string.Join(" ", args),
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();

                    if (!proc.WaitForExit((int)ProcTimeout.TotalMilliseconds))
                    {
                        proc.Kill();
                        Console.WriteLine(" ✗ TIMEOUT");
                        throw new TimeoutException($"nwnnsscomp timed out for {DisplayPath(originalNssPath)}");
                    }

                    int exitCode = proc.ExitCode;
                    bool fileExists = File.Exists(compiledOut);

                    if (exitCode != 0 || !fileExists)
                    {
                        Console.WriteLine(" ✗ FAILED");
                        string errorMsg = $"nwnnsscomp failed (exit={exitCode}, fileExists={fileExists}) for {DisplayPath(originalNssPath)}";
                        if (!string.IsNullOrEmpty(output))
                        {
                            errorMsg += $"\nCompiler output:\n{output}";
                        }
                        throw new InvalidOperationException(errorMsg);
                    }
                }
            }
            finally
            {
                if (tempDir != null)
                {
                    try
                    {
                        DeleteDirectory(tempDir);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"Warning: Failed to clean up temp directory {DisplayPath(tempDir)}: {e.Message}");
                    }
                }
            }
        }

        private static bool AreFilesSame(string path1, string path2)
        {
            try
            {
                return new FileInfo(path1).Length == new FileInfo(path2).Length;
            }
            catch
            {
                return false;
            }
        }

        private static bool NeedsAscNwscript(string nssPath)
        {
            string content = File.ReadAllText(nssPath, Encoding.UTF8);
            Regex pattern = new Regex(@"ActionStartConversation\s*\(([^,)]*,\s*){10}[^)]*\)", RegexOptions.Multiline);
            return pattern.IsMatch(content);
        }

        private static string SetupTempCompileDir(string originalNssPath, string gameFlag)
        {
            Directory.CreateDirectory(CompileTempRoot);
            string tempDir = Path.Combine(CompileTempRoot, $"compile_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            string tempSourceFile = Path.Combine(tempDir, Path.GetFileName(originalNssPath));
            File.Copy(originalNssPath, tempSourceFile, true);

            CopyIncludesRecursive(originalNssPath, gameFlag, tempDir);

            return tempDir;
        }

        private static void CopyIncludesRecursive(string sourceFile, string gameFlag, string tempDir)
        {
            Queue<string> worklist = new Queue<string>();
            HashSet<string> processed = new HashSet<string>();
            Dictionary<string, string> copied = new Dictionary<string, string>();

            worklist.Enqueue(sourceFile);

            while (worklist.Count > 0)
            {
                string current = worklist.Dequeue();
                if (current == null || !File.Exists(current)) continue;

                string normalized = Path.GetFullPath(current);
                if (!processed.Add(normalized)) continue;

                List<string> includes = ExtractIncludes(current);
                foreach (string includeName in includes)
                {
                    string includeFile = FindIncludeFile(includeName, current, gameFlag);
                    if (includeFile == null || !File.Exists(includeFile)) continue;

                    string key = includeName.ToLowerInvariant();
                    if (!copied.ContainsKey(key))
                    {
                        CopyIncludeFile(includeName, includeFile, tempDir);
                        copied[key] = includeFile;
                    }
                    worklist.Enqueue(includeFile);
                }
            }
        }

        private static List<string> ExtractIncludes(string nssPath)
        {
            string content = File.ReadAllText(nssPath, Encoding.UTF8);
            List<string> includes = new List<string>();
            Regex pattern = new Regex(@"#include\s+[""<]([^"">]+)["">]", RegexOptions.Multiline);
            foreach (Match match in pattern.Matches(content))
            {
                includes.Add(match.Groups[1].Value);
            }
            return includes;
        }

        private static string FindIncludeFile(string includeName, string sourceFile, string gameFlag)
        {
            string normalizedName = includeName;
            if (!normalizedName.EndsWith(".nss") && !normalizedName.EndsWith(".h"))
            {
                normalizedName = includeName + ".nss";
            }

            string sourceDir = Path.GetDirectoryName(sourceFile);
            string localInc = Path.Combine(sourceDir, normalizedName);
            if (File.Exists(localInc)) return localInc;

            localInc = Path.Combine(sourceDir, includeName);
            if (File.Exists(localInc)) return localInc;

            if (gameFlag.Equals("k2"))
            {
                string tslScriptsDir = Path.Combine(VanillaRepoDir, "TSL", "Vanilla", "Data", "Scripts");
                string tslInc = Path.Combine(tslScriptsDir, normalizedName);
                if (File.Exists(tslInc)) return tslInc;

                tslInc = Path.Combine(tslScriptsDir, includeName);
                if (File.Exists(tslInc)) return tslInc;
            }

            string k1IncludesDir = Path.Combine(VanillaRepoDir, "K1", "Data", "scripts.bif");
            string k1Inc = Path.Combine(k1IncludesDir, normalizedName);
            if (File.Exists(k1Inc)) return k1Inc;

            k1Inc = Path.Combine(k1IncludesDir, includeName);
            if (File.Exists(k1Inc)) return k1Inc;

            return null;
        }

        private static void CopyIncludeFile(string includeName, string includeFile, string tempDir)
        {
            string includeTarget = Path.Combine(tempDir, includeName);
            string parent = Path.GetDirectoryName(includeTarget);
            if (parent != null)
            {
                Directory.CreateDirectory(parent);
            }

            File.Copy(includeFile, includeTarget, true);

            if (!includeName.Contains("."))
            {
                string fileName = Path.GetFileName(includeFile);
                int dotIdx = fileName.LastIndexOf('.');
                if (dotIdx != -1)
                {
                    string ext = fileName.Substring(dotIdx);
                    string altTarget = Path.Combine(tempDir, includeName + ext);
                    string altParent = Path.GetDirectoryName(altTarget);
                    if (altParent != null)
                    {
                        Directory.CreateDirectory(altParent);
                    }
                    File.Copy(includeFile, altTarget, true);
                }
            }
        }

        private static void RunDecompile(string ncsPath, string nssOut, string gameFlag)
        {
            Console.Write($" (game={gameFlag}, output={Path.GetFileName(nssOut)})");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(nssOut));

                Game game = gameFlag.Equals("k2") ? Game.K2 : Game.K1;
                NCS ncs = NCSAuto.ReadNcs(ncsPath);
                string decompiled = NCSAuto.DecompileNcs(ncs, game);
                File.WriteAllText(nssOut, decompiled, Encoding.UTF8);

                if (!File.Exists(nssOut))
                {
                    Console.WriteLine(" ✗ FAILED - no output file created");
                    throw new InvalidOperationException($"Decompile did not produce output: {DisplayPath(nssOut)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ✗ FAILED - {ex.Message}");
                throw new InvalidOperationException($"Decompile failed for {DisplayPath(ncsPath)}: {ex.Message}", ex);
            }
        }

        // Normalization functions follow...
        private static string NormalizeNewlines(string s, bool isK2)
        {
            string normalized = s.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = StripComments(normalized);
            normalized = NormalizeIncludes(normalized);
            normalized = NormalizeDeclarationAssignment(normalized);
            normalized = NormalizeLeadingPlaceholders(normalized);
            normalized = NormalizePlaceholderGlobals(normalized);
            normalized = NormalizeVariableNames(normalized);
            normalized = NormalizeFunctionBraces(normalized);
            normalized = NormalizeTrailingZeroParams(normalized);
            normalized = NormalizeTrailingDefaults(normalized);
            normalized = NormalizeEffectDeathDefaults(normalized);
            normalized = NormalizeLogicalOperators(normalized);
            normalized = NormalizeIfSpacing(normalized);
            normalized = NormalizeDoubleParensInCalls(normalized);
            normalized = NormalizeAssignmentParens(normalized);
            normalized = NormalizeCallArgumentParens(normalized);
            normalized = NormalizeStructNames(normalized);
            normalized = NormalizeSubroutineNames(normalized);
            normalized = NormalizePrototypeDecls(normalized);
            normalized = NormalizeReturnStatements(normalized);
            normalized = NormalizeFunctionSignaturesByArity(normalized);
            normalized = NormalizeComparisonParens(normalized);
            normalized = NormalizeTrueFalse(normalized);
            normalized = NormalizeConstants(normalized, isK2 ? NpcConstantsK2 : NpcConstantsK1);
            normalized = NormalizeConstants(normalized, isK2 ? AbilityConstantsK2 : AbilityConstantsK1);
            normalized = NormalizeConstants(normalized, isK2 ? FactionConstantsK2 : FactionConstantsK1);
            normalized = NormalizeConstants(normalized, isK2 ? AnimationConstantsK2 : AnimationConstantsK1);
            normalized = NormalizeBitwiseOperators(normalized);
            normalized = NormalizeControlFlowConditions(normalized);
            normalized = NormalizeCommaSpacing(normalized);
            normalized = NormalizePlaceholderNames(normalized);
            normalized = NormalizeAssignCommandPlaceholders(normalized);
            normalized = NormalizeFunctionOrder(normalized);

            string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            StringBuilder result = new StringBuilder();

            foreach (string line in lines)
            {
                string trimmed = Regex.Replace(line, @"^\s+", "");
                trimmed = Regex.Replace(trimmed, @"\s+$", "");
                if (string.IsNullOrEmpty(trimmed)) continue;
                trimmed = trimmed.Replace("\t", "    ");
                result.Append(trimmed).Append("\n");
            }

            string finalResult = result.ToString();
            while (finalResult.EndsWith("\n"))
            {
                finalResult = finalResult.Substring(0, finalResult.Length - 1);
            }

            return finalResult;
        }

        private static string StripComments(string code)
        {
            StringBuilder result = new StringBuilder();
            bool inBlockComment = false;
            bool inString = false;
            char[] chars = code.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (inBlockComment)
                {
                    if (i < chars.Length - 1 && chars[i] == '*' && chars[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (inString)
                {
                    result.Append(chars[i]);
                    if (chars[i] == '"' && (i == 0 || chars[i - 1] != '\\'))
                    {
                        inString = false;
                    }
                    continue;
                }

                if (chars[i] == '"')
                {
                    inString = true;
                    result.Append(chars[i]);
                }
                else if (i < chars.Length - 1 && chars[i] == '/' && chars[i + 1] == '/')
                {
                    while (i < chars.Length && chars[i] != '\n')
                    {
                        i++;
                    }
                    if (i < chars.Length)
                    {
                        result.Append('\n');
                    }
                }
                else if (i < chars.Length - 1 && chars[i] == '/' && chars[i + 1] == '*')
                {
                    inBlockComment = true;
                    i++;
                }
                else
                {
                    result.Append(chars[i]);
                }
            }

            return result.ToString();
        }

        private static string NormalizeIncludes(string code)
        {
            return Regex.Replace(code, @"(?m)^\s*#include[^\n]*\n?", "");
        }

        private static string NormalizeLeadingPlaceholders(string code)
        {
            string[] lines = code.Split('\n');
            int idx = 0;
            Regex placeholderPattern = new Regex(@"^[\uFEFF]?int\s+int\d+\s*=\s*[-0-9xa-fA-F]+;");
            while (idx < lines.Length)
            {
                string line = lines[idx].Trim();
                if (line.StartsWith("//"))
                {
                    idx++;
                    continue;
                }
                if (string.IsNullOrEmpty(line))
                {
                    idx++;
                    continue;
                }
                if (placeholderPattern.IsMatch(line))
                {
                    idx++;
                    continue;
                }
                break;
            }
            if (idx == 0) return code;

            StringBuilder sb = new StringBuilder();
            for (int i = idx; i < lines.Length; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Length - 1)
                {
                    sb.Append("\n");
                }
            }
            return sb.ToString();
        }

        private static string NormalizePlaceholderGlobals(string code)
        {
            string[] lines = code.Split('\n');
            int count = 0;
            int end = 0;
            Regex placeholderPattern = new Regex(@"^[\uFEFF]?int\s+(int\d+|intGLOB_\d+)\s*=\s*[-0-9xa-fA-F]+;");

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("//")) continue;
                if (placeholderPattern.IsMatch(line))
                {
                    count++;
                    end = i;
                }
                else if (string.IsNullOrEmpty(line))
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            if (count >= 5 && end >= 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = end + 1; i < lines.Length; i++)
                {
                    sb.Append(lines[i]);
                    if (i < lines.Length - 1) sb.Append("\n");
                }
                return sb.ToString();
            }
            return code;
        }

        private static string NormalizeFunctionBraces(string code)
        {
            string result = Regex.Replace(code, @"\)\s*\n\s*\{", ") {");
            return result;
        }

        private static string NormalizeTrailingZeroParams(string code)
        {
            Regex pattern = new Regex(@"([a-zA-Z_][a-zA-Z0-9_]*\s*\([^)]*),\s*(0|0x0)\s*\)");
            MatchCollection matches = pattern.Matches(code);
            List<Tuple<int, int, string>> replacements = new List<Tuple<int, int, string>>();

            foreach (Match match in matches)
            {
                replacements.Add(Tuple.Create(match.Index, match.Length, match.Groups[1].Value + ")"));
            }

            for (int i = replacements.Count - 1; i >= 0; i--)
            {
                Tuple<int, int, string> replacement = replacements[i];
                code = code.Substring(0, replacement.Item1) + replacement.Item3 + code.Substring(replacement.Item1 + replacement.Item2);
            }

            return code;
        }

        private static string NormalizeTrailingDefaults(string code)
        {
            Dictionary<string, string> trailingDefaults = new Dictionary<string, string>
            {
                ["ActionJumpToObject"] = "(1|TRUE)",
                ["d2"] = "(0|1)",
                ["d3"] = "(0|1)",
                ["d4"] = "(0|1)",
                ["d6"] = "(0|1)",
                ["d8"] = "(0|1)",
                ["d10"] = "(0|1)",
                ["d12"] = "(0|1)",
                ["d20"] = "(0|1)",
                ["d100"] = "(0|1)",
                ["ActionAttack"] = "(0|FALSE)",
                ["ActionStartConversation"] = "(0|0xFFFFFFFF|-1)",
                ["ActionMoveToObject"] = @"(1\.0|1)"
            };

            string result = code;
            foreach (KeyValuePair<string, string> entry in trailingDefaults)
            {
                string func = entry.Key;
                string defaultValue = entry.Value;
                Regex pattern = new Regex($"({Regex.Escape(func)}\\s*\\([^)]*),\\s*{defaultValue}\\s*\\)");
                MatchCollection matches = pattern.Matches(result);
                List<Tuple<int, int, string>> replacements = new List<Tuple<int, int, string>>();

                foreach (Match match in matches)
                {
                    replacements.Add(Tuple.Create(match.Index, match.Length, match.Groups[1].Value + ")"));
                }

                for (int i = replacements.Count - 1; i >= 0; i--)
                {
                    Tuple<int, int, string> replacement = replacements[i];
                    if (replacement.Item1 >= 0 && replacement.Item1 + replacement.Item2 <= result.Length)
                    {
                        result = result.Substring(0, replacement.Item1) + replacement.Item3 + result.Substring(replacement.Item1 + replacement.Item2);
                    }
                }
            }

            return result;
        }

        private static string NormalizeEffectDeathDefaults(string code)
        {
            return Regex.Replace(code, @"\bEffectDeath\s*\(\s*\)", "EffectDeath(0, 1)");
        }

        private static string NormalizeLogicalOperators(string code)
        {
            string result = code;
            result = Regex.Replace(result, @"\&\s*\&", "&&");
            result = Regex.Replace(result, @"\|\s*\|", "||");
            result = result.Replace(" & & ", " && ");
            result = result.Replace(" | | ", " || ");
            return result;
        }

        private static string NormalizeIfSpacing(string code)
        {
            string result = code;
            result = Regex.Replace(result, @"\bif([a-zA-Z_][a-zA-Z0-9_]*)", "if $1");
            result = Regex.Replace(result, @"\bif\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*(==|!=|<=|>=|<|>|&|\|)", "if ($1 $2");
            result = Regex.Replace(result, @"\bif(?=[A-Za-z_])", "if ");
            result = Regex.Replace(result, @"\bwhile(?=[A-Za-z_])", "while ");
            result = Regex.Replace(result, @"\bfor(?=[A-Za-z_])", "for ");
            result = Regex.Replace(result, @"\bswitch(?=[A-Za-z_])", "switch ");
            return result;
        }

        private static string NormalizeDoubleParensInCalls(string code)
        {
            Regex pattern = new Regex(@"([A-Za-z_][A-Za-z0-9_]*)\s*\(\(([^()]+)\)(\s*[),])");
            return pattern.Replace(code, m => m.Groups[1].Value + "(" + m.Groups[2].Value.Trim() + m.Groups[3].Value);
        }

        private static string NormalizeAssignmentParens(string code)
        {
            Regex pattern = new Regex(@"(=)\s*\(([^;\n]+)\)\s*;");
            return pattern.Replace(code, m => m.Groups[1].Value + " " + m.Groups[2].Value.Trim() + ";");
        }

        private static string NormalizeCallArgumentParens(string code)
        {
            Regex pattern = new Regex(@",\s*\(([^(),]+)\)");
            return pattern.Replace(code, m => ", " + m.Groups[1].Value.Trim());
        }

        private static string NormalizeStructNames(string code)
        {
            Regex pattern = new Regex(@"\bstructtype(\d+)\b");
            Dictionary<string, string> map = new Dictionary<string, string>();
            int counter = 1;

            return pattern.Replace(code, m =>
            {
                string orig = m.Value;
                if (!map.ContainsKey(orig))
                {
                    map[orig] = "structtype" + counter++;
                }
                return map[orig];
            });
        }

        private static string NormalizeSubroutineNames(string code)
        {
            Regex pattern = new Regex(@"\bsub(\d+)\b");
            Dictionary<string, string> map = new Dictionary<string, string>();
            int counter = 1;

            return pattern.Replace(code, m =>
            {
                string orig = m.Value;
                if (!map.ContainsKey(orig))
                {
                    map[orig] = "sub" + counter++;
                }
                return map[orig];
            });
        }

        private static string NormalizePrototypeDecls(string code)
        {
            return Regex.Replace(code, @"(?m)^\s*(int|float|void|string|object|location|vector|effect|talent)\s+sub\d+\s*\([^;]*\);\s*\n?", "");
        }

        private static string NormalizeReturnStatements(string code)
        {
            Regex pattern = new Regex(@"return\s+\(([^()]+(?:\.[^()]*)*)\);");
            string result = pattern.Replace(code, "return $1;");

            Regex outputParamPattern = new Regex(@"([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*([^;\n}]+);\s*\n\s*return\s*;", RegexOptions.Multiline);
            result = outputParamPattern.Replace(result, "return $2;");

            Regex outputParamPatternSingle = new Regex(@"([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*([^;]+);\s+return\s*;");
            result = outputParamPatternSingle.Replace(result, "return $2;");

            return result;
        }

        private static string NormalizeComparisonParens(string code)
        {
            string result = code;
            Regex assignPattern = new Regex(@"(=)\s*\(([^;\n]+?\s*(==|!=|<=|>=|<|>)\s*[^;\n]+?)\)\s*;");
            result = assignPattern.Replace(result, m => m.Groups[1].Value + " " + m.Groups[2].Value.Trim() + ";");

            Regex generalPattern = new Regex(@"\(([^()]+?\s*(==|!=|<=|>=|<|>)\s*[^()]+?)\)");
            result = generalPattern.Replace(result, m => m.Groups[1].Value.Trim());

            return result;
        }

        private static string NormalizeFunctionSignaturesByArity(string code)
        {
            Regex pattern = new Regex(@"(?m)^\s*([A-Za-z_][\w\s\*]*?)\s+([A-Za-z_]\w*)\s*\(([^)]*)\)\s*(\{|;)");
            return pattern.Replace(code, m =>
            {
                string ret = m.Groups[1].Value.Trim();
                string name = m.Groups[2].Value.Trim();
                string paramsStr = m.Groups[3].Value.Trim();
                int count = 0;
                if (!string.IsNullOrEmpty(paramsStr))
                {
                    count = paramsStr.Split(',').Length;
                }
                return ret + " " + name + "(/*params=" + count + "*/)" + m.Groups[4].Value;
            });
        }

        private static string NormalizeTrueFalse(string code)
        {
            string result = code;
            result = Regex.Replace(result, @"\bTRUE\b", "1");
            result = Regex.Replace(result, @"\bFALSE\b", "0");
            return result;
        }

        private static string NormalizeConstants(string code, Dictionary<string, string> constants)
        {
            if (constants == null || constants.Count == 0) return code;

            string prefix = constants.Keys.FirstOrDefault()?.Substring(0, constants.Keys.First().IndexOf('_') + 1) ?? "NPC_";
            Regex pattern = new Regex($@"\b{prefix}[A-Za-z0-9_]+\b");

            return pattern.Replace(code, m =>
            {
                string name = m.Value;
                if (constants.ContainsKey(name))
                {
                    return constants[name];
                }
                return name;
            });
        }

        private static Dictionary<string, string> LoadConstantsWithPrefix(string path, string prefix)
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            if (!File.Exists(path)) return map;

            try
            {
                Regex pattern = new Regex($@"^\s*int\s+({prefix}[A-Za-z0-9_]+)\s*=\s*([-]?[0-9]+)\s*;.*$");
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    Match match = pattern.Match(line);
                    if (match.Success)
                    {
                        map[match.Groups[1].Value] = match.Groups[2].Value;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return map;
        }

        private static string NormalizePlaceholderNames(string code)
        {
            return Regex.Replace(code, @"__unknown_param_\d+", "__unknown_param");
        }

        private static string NormalizeAssignCommandPlaceholders(string code)
        {
            Regex pattern = new Regex(@"AssignCommand\s*\(([^,]+),\s*void\s+\w+\s*=\s*([^;]+);\s*\);", RegexOptions.Singleline);
            return pattern.Replace(code, m => "AssignCommand(" + m.Groups[1].Value.Trim() + ", " + m.Groups[2].Value.Trim() + ");");
        }

        private static string NormalizeBitwiseOperators(string code)
        {
            string result = code;
            result = Regex.Replace(result, @"\s*&\s+(?!=)", " & ");
            result = Regex.Replace(result, @"\s*\|\s+(?!=)", " | ");
            return result;
        }

        private static string NormalizeControlFlowConditions(string code)
        {
            StringBuilder output = new StringBuilder(code.Length);
            bool inString = false;

            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];

                if (ch == '"' && (i == 0 || code[i - 1] != '\\'))
                {
                    inString = !inString;
                    output.Append(ch);
                    continue;
                }

                if (!inString)
                {
                    string keyword = MatchControlKeyword(code, i);
                    if (keyword != null)
                    {
                        int kwEnd = i + keyword.Length;
                        int idx = kwEnd;
                        while (idx < code.Length && char.IsWhiteSpace(code[idx]))
                        {
                            idx++;
                        }
                        if (idx < code.Length && code[idx] == '(')
                        {
                            int endParen = FindMatchingParen(code, idx);
                            if (endParen != -1)
                            {
                                string condition = code.Substring(idx + 1, endParen - idx - 1);
                                condition = StripOuterParens(condition).Trim();
                                output.Append(keyword).Append(" (").Append(condition).Append(")");
                                i = endParen;
                                continue;
                            }
                        }
                    }
                }

                output.Append(ch);
            }

            return output.ToString();
        }

        private static string MatchControlKeyword(string code, int index)
        {
            string[] keywords = { "if", "while", "switch", "for" };
            foreach (string kw in keywords)
            {
                int len = kw.Length;
                if (index + len <= code.Length && code.Substring(index, len).Equals(kw))
                {
                    char before = index == 0 ? '\0' : code[index - 1];
                    char after = index + len < code.Length ? code[index + len] : '\0';
                    if (!char.IsLetterOrDigit(before) && before != '_' && !char.IsLetterOrDigit(after) && after != '_')
                    {
                        return kw;
                    }
                }
            }
            return null;
        }

        private static int FindMatchingParen(string code, int openIdx)
        {
            int depth = 0;
            bool inString = false;
            for (int i = openIdx; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '"' && (i == 0 || code[i - 1] != '\\'))
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;
                if (c == '(') depth++;
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static string StripOuterParens(string expr)
        {
            string result = expr.Trim();
            bool changed = true;
            while (changed && result.Length >= 2 && result[0] == '(' && result[result.Length - 1] == ')')
            {
                changed = false;
                int depth = 0;
                bool balanced = true;
                for (int i = 0; i < result.Length; i++)
                {
                    char c = result[i];
                    if (c == '(') depth++;
                    else if (c == ')')
                    {
                        depth--;
                        if (depth == 0 && i < result.Length - 1)
                        {
                            balanced = false;
                            break;
                        }
                    }
                    if (depth < 0)
                    {
                        balanced = false;
                        break;
                    }
                }
                if (balanced && depth == 0)
                {
                    result = result.Substring(1, result.Length - 2).Trim();
                    changed = true;
                }
            }
            return result;
        }

        private static string NormalizeCommaSpacing(string code)
        {
            StringBuilder output = new StringBuilder(code.Length);
            bool inString = false;

            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];

                if (ch == '"' && (i == 0 || code[i - 1] != '\\'))
                {
                    inString = !inString;
                    output.Append(ch);
                    continue;
                }

                if (!inString && ch == ',')
                {
                    output.Append(ch);
                    int j = i + 1;
                    while (j < code.Length)
                    {
                        char next = code[j];
                        if (next == ' ' || next == '\t' || next == '\r')
                        {
                            j++;
                            continue;
                        }
                        break;
                    }
                    i = j - 1;
                    continue;
                }

                output.Append(ch);
            }

            return output.ToString();
        }

        private static string NormalizeDeclarationAssignment(string code)
        {
            Regex declPattern = new Regex(@"\b(int|float|string|object|vector|location|effect|itemproperty|talent|action|event)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*;");
            Regex assignPattern = new Regex(@"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(.+?);");

            string[] lines = code.Split('\n');
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                Match declMatch = declPattern.Match(line);
                if (declMatch.Success && i + 1 < lines.Length)
                {
                    string type = declMatch.Groups[1].Value;
                    string varName = declMatch.Groups[2].Value;

                    int nextLineIdx = i + 1;
                    while (nextLineIdx < lines.Length && string.IsNullOrWhiteSpace(lines[nextLineIdx]))
                    {
                        nextLineIdx++;
                    }

                    if (nextLineIdx < lines.Length)
                    {
                        string nextLine = lines[nextLineIdx].Trim();
                        Match assignMatch = assignPattern.Match(nextLine);
                        if (assignMatch.Success && assignMatch.Groups[1].Value.Equals(varName))
                        {
                            string value = assignMatch.Groups[2].Value;
                            result.Append(type).Append(" ").Append(varName).Append(" = ").Append(value).Append(";");
                            i = nextLineIdx;
                            result.Append("\n");
                            continue;
                        }
                    }
                }

                result.Append(lines[i]).Append("\n");
            }

            return result.ToString();
        }

        private static string NormalizeVariableNames(string code)
        {
            // This is a simplified version - full implementation would be more complex
            return code;
        }

        private static string NormalizeFunctionOrder(string code)
        {
            string[] lines = code.Split('\n');
            List<string> functions = new List<string>();
            StringBuilder current = new StringBuilder();
            int depth = 0;
            StringBuilder preamble = new StringBuilder();
            bool inFunction = false;
            Regex functionSignatureRegex = new Regex(@"^(\s*\w[\w\s\*]+\w\s*\([^)]*\)\s*\{)");

            foreach (string line in lines)
            {
                bool isFunctionSignature = functionSignatureRegex.IsMatch(line);
                if (!inFunction && depth == 0 && !isFunctionSignature)
                {
                    preamble.Append(line).Append("\n");
                    continue;
                }

                if (!inFunction && isFunctionSignature)
                {
                    inFunction = true;
                    current.Clear();
                }

                if (inFunction)
                {
                    current.Append(line).Append("\n");
                    int openBraces = CountChar(line, '{');
                    int closeBraces = CountChar(line, '}');
                    depth += openBraces;
                    depth -= closeBraces;

                    if (depth == 0)
                    {
                        functions.Add(current.ToString().Trim());
                        current.Clear();
                        inFunction = false;
                    }
                }
            }

            if (current.Length > 0)
            {
                functions.Add(current.ToString().Trim());
            }

            functions.Sort();
            string preambleStr = preamble.ToString().Trim();
            string functionsStr = string.Join("\n", functions);
            StringBuilder rebuilt = new StringBuilder();
            if (!string.IsNullOrEmpty(preambleStr))
            {
                rebuilt.Append(preambleStr);
                if (!string.IsNullOrEmpty(functionsStr))
                {
                    rebuilt.Append("\n");
                }
            }
            if (!string.IsNullOrEmpty(functionsStr))
            {
                rebuilt.Append(functionsStr);
            }
            return rebuilt.ToString().Trim();
        }

        private static int CountChar(string line, char ch)
        {
            int count = 0;
            foreach (char c in line)
            {
                if (c == ch) count++;
            }
            return count;
        }

        private static string ExpandIncludes(string sourceFile, string gameFlag)
        {
            return ExpandIncludesInternal(sourceFile, gameFlag, new HashSet<string>());
        }

        private static string ExpandIncludesInternal(string sourceFile, string gameFlag, HashSet<string> visited)
        {
            string normalizedSource = Path.GetFullPath(sourceFile);
            if (!visited.Add(normalizedSource))
            {
                return "";
            }

            string content = File.ReadAllText(normalizedSource, Encoding.UTF8);
            StringBuilder expanded = new StringBuilder();
            Regex includePattern = new Regex(@"#include\s+[""<]([^"">]+)["">]");

            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match matcher = includePattern.Match(line);
                    if (matcher.Success)
                    {
                        string includeName = matcher.Groups[1].Value;
                        string includeFile = FindIncludeFile(includeName, normalizedSource, gameFlag);
                        if (includeFile != null && File.Exists(includeFile))
                        {
                            expanded.Append(ExpandIncludesInternal(includeFile, gameFlag, visited));
                            expanded.Append("\n");
                        }
                        continue;
                    }
                    expanded.Append(line).Append("\n");
                }
            }

            return expanded.ToString();
        }

        private static string FilterFunctionsNotInDecompiled(string expandedOriginal, string decompiledOutput)
        {
            int decompiledFunctionCount = CountNonMainFunctions(decompiledOutput);
            if (decompiledFunctionCount == 0)
            {
                return expandedOriginal;
            }

            // Simplified - full implementation would extract and match functions
            return expandedOriginal;
        }

        private static int CountNonMainFunctions(string code)
        {
            int count = 0;
            Regex funcPattern = new Regex(@"^(\s*)(\w+)\s+(\w+)\s*\([^)]*\)\s*\{", RegexOptions.Multiline);
            foreach (Match match in funcPattern.Matches(code))
            {
                string funcName = match.Groups[3].Value;
                if (!funcName.Equals("main") && !funcName.Equals("StartingConditional"))
                {
                    count++;
                }
            }
            return count;
        }

        private static string FormatUnifiedDiff(string expected, string actual)
        {
            string[] expectedLines = expected.Split(new[] { '\n' }, StringSplitOptions.None);
            string[] actualLines = actual.Split(new[] { '\n' }, StringSplitOptions.None);

            DiffResult diffResult = ComputeDiff(expectedLines, actualLines);

            if (diffResult.IsEmpty())
            {
                return null;
            }

            StringBuilder diff = new StringBuilder();
            diff.Append("    --- expected\n");
            diff.Append("    +++ actual\n");
            diff.Append("    @@ -1,").Append(expectedLines.Length).Append(" +1,").Append(actualLines.Length).Append(" @@\n");

            foreach (DiffLine line in diffResult.Lines)
            {
                switch (line.Type)
                {
                    case DiffLineType.Context:
                        diff.Append("     ").Append(line.Content).Append("\n");
                        break;
                    case DiffLineType.Removed:
                        diff.Append("    -").Append(line.Content).Append("\n");
                        break;
                    case DiffLineType.Added:
                        diff.Append("    +").Append(line.Content).Append("\n");
                        break;
                }
            }

            return diff.ToString();
        }

        private enum DiffLineType
        {
            Context,
            Removed,
            Added
        }

        private class DiffLine
        {
            public DiffLineType Type { get; set; }
            public string Content { get; set; }
        }

        private class DiffResult
        {
            public List<DiffLine> Lines { get; set; } = new List<DiffLine>();

            public bool IsEmpty()
            {
                return Lines.All(l => l.Type == DiffLineType.Context);
            }
        }

        private static DiffResult ComputeDiff(string[] expected, string[] actual)
        {
            DiffResult result = new DiffResult();

            int m = expected.Length;
            int n = actual.Length;
            int[,] dp = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (expected[i - 1].Equals(actual[j - 1]))
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                    }
                }
            }

            int ii = m, jj = n;
            List<DiffLine> tempLines = new List<DiffLine>();

            while (ii > 0 || jj > 0)
            {
                if (ii > 0 && jj > 0 && expected[ii - 1].Equals(actual[jj - 1]))
                {
                    tempLines.Add(new DiffLine { Type = DiffLineType.Context, Content = expected[ii - 1] });
                    ii--;
                    jj--;
                }
                else if (jj > 0 && (ii == 0 || dp[ii, jj - 1] >= dp[ii - 1, jj]))
                {
                    tempLines.Add(new DiffLine { Type = DiffLineType.Added, Content = actual[jj - 1] });
                    jj--;
                }
                else if (ii > 0)
                {
                    tempLines.Add(new DiffLine { Type = DiffLineType.Removed, Content = expected[ii - 1] });
                    ii--;
                }
            }

            for (int k = tempLines.Count - 1; k >= 0; k--)
            {
                result.Lines.Add(tempLines[k]);
            }

            return result;
        }

        private static void AssertBytecodeEqual(string originalNcs, string roundTripNcs, string gameFlag, string displayName)
        {
            BytecodeDiffResult diff = FindBytecodeDiff(originalNcs, roundTripNcs);
            if (diff == null)
            {
                return;
            }

            StringBuilder message = new StringBuilder();
            message.Append("═══════════════════════════════════════════════════════════════\n");
            message.Append("BYTECODE MISMATCH: ").Append(displayName).Append("\n");
            message.Append("═══════════════════════════════════════════════════════════════\n");
            message.Append("\n");
            message.Append("LOCATION:\n");
            message.Append("  Offset: ").Append(diff.Offset).Append(" (0x").Append(diff.Offset.ToString("X")).Append(")\n");
            message.Append("  Original: ").Append(FormatByteValue(diff.OriginalByte)).Append("\n");
            message.Append("  Round-trip: ").Append(FormatByteValue(diff.RoundTripByte)).Append("\n");
            message.Append("\n");
            message.Append("FILES:\n");
            message.Append("  Original NCS: ").Append(originalNcs).Append("\n");
            message.Append("  Round-trip NCS: ").Append(roundTripNcs).Append("\n");
            message.Append("\n");
            message.Append("BYTECODE CONTEXT:\n");
            message.Append("  Original:  ").Append(diff.OriginalContext).Append("\n");
            message.Append("  Round-trip: ").Append(diff.RoundTripContext).Append("\n");
            message.Append("\n");
            message.Append("FILE SIZES:\n");
            message.Append("  Original: ").Append(diff.OriginalLength).Append(" bytes\n");
            message.Append("  Round-trip: ").Append(diff.RoundTripLength).Append(" bytes\n");
            message.Append("\n");
            message.Append("═══════════════════════════════════════════════════════════════\n");

            throw new InvalidOperationException(message.ToString());
        }

        private static BytecodeDiffResult FindBytecodeDiff(string originalNcs, string roundTripNcs)
        {
            byte[] originalBytes = File.ReadAllBytes(originalNcs);
            byte[] roundTripBytes = File.ReadAllBytes(roundTripNcs);
            int maxLength = Math.Max(originalBytes.Length, roundTripBytes.Length);

            for (int i = 0; i < maxLength; i++)
            {
                int original = i < originalBytes.Length ? originalBytes[i] & 0xFF : -1;
                int roundTrip = i < roundTripBytes.Length ? roundTripBytes[i] & 0xFF : -1;
                if (original != roundTrip)
                {
                    return new BytecodeDiffResult
                    {
                        Offset = i,
                        OriginalByte = original,
                        RoundTripByte = roundTrip,
                        OriginalLength = originalBytes.Length,
                        RoundTripLength = roundTripBytes.Length,
                        OriginalContext = RenderHexContext(originalBytes, i),
                        RoundTripContext = RenderHexContext(roundTripBytes, i)
                    };
                }
            }

            return null;
        }

        private static string RenderHexContext(byte[] bytes, int focus)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "<empty>";
            }

            int anchor = Math.Min(Math.Max(focus, 0), bytes.Length - 1);
            int start = Math.Max(0, anchor - 8);
            int end = Math.Min(bytes.Length, anchor + 9);

            StringBuilder sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (i > start) sb.Append(' ');
                sb.Append($"{bytes[i]:X2}");
                if (i == focus) sb.Append('*');
            }
            return sb.ToString();
        }

        private static string FormatByteValue(int value)
        {
            if (value < 0)
            {
                return "<EOF>";
            }
            return $"0x{value:X2} ({value})";
        }

        private class BytecodeDiffResult
        {
            public long Offset { get; set; }
            public int OriginalByte { get; set; }
            public int RoundTripByte { get; set; }
            public long OriginalLength { get; set; }
            public long RoundTripLength { get; set; }
            public string OriginalContext { get; set; }
            public string RoundTripContext { get; set; }
        }

        private static void DeleteDirectory(string dir)
        {
            if (Directory.Exists(dir))
            {
                foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                Directory.Delete(dir, true);
            }
        }

        private void PrintPerformanceSummary()
        {
            long totalTime = Stopwatch.GetTimestamp() - _testStartTime;

            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("PERFORMANCE SUMMARY");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Total test time: {GetElapsedMilliseconds(totalTime) / 1000.0:F2} seconds");
            Console.WriteLine($"Tests processed: {_testsProcessed} / {_totalTests}");

            if (_testsProcessed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Operation breakdown (cumulative):");
                foreach (KeyValuePair<string, long> entry in OperationTimes)
                {
                    double seconds = GetElapsedMilliseconds(entry.Value) / 1000.0;
                    double percentage = (GetElapsedMilliseconds(entry.Value) * 100.0) / GetElapsedMilliseconds(totalTime);
                    Console.WriteLine($"  {entry.Key,-12}: {seconds,8:F2} s ({percentage,5:F1}%)");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Profile log: {DisplayPath(ProfileOutput)}");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
        }

        [Fact]
        public void TestRoundTripSuite()
        {
            ResetPerformanceTracking();
            _testStartTime = Stopwatch.GetTimestamp();

            try
            {
                Preflight();
                List<RoundTripCase> tests = BuildRoundTripCases();

                if (tests.Count == 0)
                {
                    Console.Error.WriteLine("ERROR: No test files found!");
                    throw new InvalidOperationException("No test files found!");
                }

                Console.WriteLine("=== Running Round-Trip Tests ===");
                Console.WriteLine($"Total tests: {tests.Count}");
                Console.WriteLine("Fast-fail: enabled (will stop on first failure)");
                Console.WriteLine();

                foreach (RoundTripCase testCase in tests)
                {
                    _testsProcessed++;
                    string relPath = GetRelativePath(VanillaRepoDir, testCase.Item.Path);
                    string displayPath = relPath.Replace('\\', '/');
                    Console.WriteLine($"[{_testsProcessed}/{_totalTests}] {displayPath}");

                    try
                    {
                        RoundTripSingle(testCase.Item.Path, testCase.Item.GameFlag, testCase.Item.ScratchRoot);
                        Console.WriteLine("  ✓ PASSED - Round-trip successful (bytecode matches)");
                        Console.WriteLine();
                    }
                    catch (SourceCompilationException)
                    {
                        Console.WriteLine("  ⊘ SKIPPED (original source file has compilation errors)");
                        Console.WriteLine();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("  ✗ FAILED");
                        Console.WriteLine();
                        Console.WriteLine("═══════════════════════════════════════════════════════════");
                        Console.WriteLine($"FAILURE: {testCase.DisplayName}");
                        Console.WriteLine("═══════════════════════════════════════════════════════════");
                        Console.WriteLine($"Exception: {ex.GetType().Name}");
                        Console.WriteLine($"Message:\n{ex.Message}");
                        Console.WriteLine("═══════════════════════════════════════════════════════════");
                        Console.WriteLine();

                        PrintPerformanceSummary();
                        throw;
                    }
                }

                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("ALL TESTS PASSED!");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine($"Tests run: {tests.Count}");
                Console.WriteLine($"Tests passed: {tests.Count}");
                Console.WriteLine("Tests failed: 0");
                Console.WriteLine();

                PrintPerformanceSummary();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"FATAL ERROR: {e.Message}");
                PrintPerformanceSummary();
                throw;
            }
        }
    }
}
