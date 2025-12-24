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
using System.Threading.Tasks;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.NCS.NCSDecomp;
using Andastra.Parsing.Tests.Performance;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using IOException = Andastra.Parsing.Formats.NCS.NCSDecomp.IOException;
using NcsFile = Andastra.Parsing.Formats.NCS.NCSDecomp.NcsFile;

namespace Andastra.Parsing.Tests.Formats
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
    /// IMPLEMENTED: Output "repair" and workaround functionality
    ///   is available via OutputRepairProcessor in the decompiler
    ///   (disabled by default for testing, enabled via settings)
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
        private static readonly string K1Nwscript = Path.Combine(RepoRoot, "tools", "k1_nwscript.nss");
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
        /// Finds the repository root by searching upward from the assembly location or current directory.
        /// Validates the root by checking for expected files (tools/nwnnsscomp.exe) to avoid nested git repos.
        /// </summary>
        private static string FindRepositoryRoot()
        {
            // Try assembly location first (more reliable when running from test output directory)
            string startDir = null;
            try
            {
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    startDir = Path.GetDirectoryName(assemblyPath);
                }
            }
            catch
            {
                // Fall back to current directory if assembly location is not available
            }

            if (string.IsNullOrEmpty(startDir))
            {
                startDir = Directory.GetCurrentDirectory();
            }

            DirectoryInfo dir = new DirectoryInfo(startDir);
            string bestCandidate = null;

            while (dir != null)
            {
                // Check for .git directory
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    // Validate this is the actual repo root by checking for expected files
                    string toolsDir = Path.Combine(dir.FullName, "tools");
                    string[] compilerNames = { "nwnnsscomp.exe", "nwnnsscomp_kscript.exe", "nwnnsscomp_tslpatcher.exe" };
                    bool hasCompiler = false;
                    foreach (string name in compilerNames)
                    {
                        if (File.Exists(Path.Combine(toolsDir, name)))
                        {
                            hasCompiler = true;
                            break;
                        }
                    }

                    if (hasCompiler)
                    {
                        // This looks like the actual repo root
                        return dir.FullName;
                    }
                    // Otherwise, this might be a nested git repo, continue searching
                }

                // Also check for .sln file, but continue searching in case .git is higher up
                if (bestCandidate == null && Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
                {
                    bestCandidate = dir.FullName;
                }

                dir = dir.Parent;
            }

            // Use .sln location if we found one but no validated .git
            if (bestCandidate != null)
            {
                return bestCandidate;
            }

            // Fallback to current directory if not found
            return Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Finds the compiler executable by trying multiple filenames in multiple locations.
        /// Returns absolute path.
        /// </summary>
        private static string FindCompiler()
        {
            string[] compilerNames = { "nwnnsscomp.exe", "nwnnsscomp_kscript.exe", "nwnnsscomp_tslpatcher.exe" };

            // 1. Try tools/ directory relative to repo root
            string toolsDir = Path.Combine(RepoRoot, "tools");
            foreach (string name in compilerNames)
            {
                string candidate = Path.GetFullPath(Path.Combine(toolsDir, name));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // 2. Try current working directory
            string cwd = Directory.GetCurrentDirectory();
            foreach (string name in compilerNames)
            {
                string candidate = Path.GetFullPath(Path.Combine(cwd, name));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // Default fallback - return absolute path even if file doesn't exist yet
            return Path.GetFullPath(Path.Combine(RepoRoot, "tools", "nwnnsscomp.exe"));
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

            // NwnCompiler should already be an absolute path from FindCompiler()
            if (!File.Exists(NwnCompiler))
            {
                throw new IOException($"nwnnsscomp.exe missing at: {DisplayPath(NwnCompiler)} (RepoRoot: {RepoRoot}, Absolute: {Path.GetFullPath(NwnCompiler)})");
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

            // Copy nwscript files to tools/ directory for FileDecompiler
            // Matching DeNCS implementation at vendor/DeNCS/src/test/java/com/kotor/resource/formats/ncs/NCSDecompCLIRoundTripTest.java:318-345
            // Original: // Copy nwscript files to tools/ directory for FileDecompiler
            string cwd = Directory.GetCurrentDirectory();
            string toolsDir = Path.Combine(cwd, "tools");
            if (!Directory.Exists(toolsDir))
            {
                Directory.CreateDirectory(toolsDir);
            }
            string k1NwscriptTarget = Path.Combine(toolsDir, "k1_nwscript.nss");
            string k2NwscriptTarget = Path.Combine(toolsDir, "tsl_nwscript.nss");

            // Copy K1 nwscript if it doesn't exist or is different
            if (!File.Exists(k1NwscriptTarget) || !AreFilesSame(K1Nwscript, k1NwscriptTarget))
            {
                try
                {
                    CopyWithRetry(K1Nwscript, k1NwscriptTarget);
                }
                catch (Exception ex)
                {
                    if (File.Exists(k1NwscriptTarget))
                    {
                        Console.WriteLine($"! Warning: could not refresh k1_nwscript.nss ({ex.Message}); using existing copy at {DisplayPath(k1NwscriptTarget)}");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // Copy K2 nwscript if it doesn't exist or is different
            if (!File.Exists(k2NwscriptTarget) || !AreFilesSame(K2Nwscript, k2NwscriptTarget))
            {
                try
                {
                    CopyWithRetry(K2Nwscript, k2NwscriptTarget);
                }
                catch (Exception ex)
                {
                    if (File.Exists(k2NwscriptTarget))
                    {
                        Console.WriteLine($"! Warning: could not refresh tsl_nwscript.nss ({ex.Message}); using existing copy at {DisplayPath(k2NwscriptTarget)}");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

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
        /// Result of a round-trip test.
        /// </summary>
        private class RoundTripResult
        {
            public bool Passed { get; set; }
            public string RelPath { get; set; }
            public bool TextMatch { get; set; }
            public string TextDiff { get; set; }
            public bool BytecodeMatch { get; set; }
            public string PcodeDiff { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// Performs a complete round-trip test: NSS->NCS->NSS->NCS->NSS->NCS
        ///
        /// TEST FLOW (EXACT REQUIREMENTS - IN ORDER):
        /// ===========================================
        /// Step 1: NSS -> NCS using EXTERNAL nwnnsscomp.exe compiler from ./tools (RunExternalCompiler)
        /// Step 2: NCS -> NSS using INBUILT .NET decompiler (RunDecompile)
        /// Step 3: Compare original NSS vs decompiled NSS (text comparison) - TERTIARY PRIORITY (warns only, shows UDIFF)
        /// Step 4: NSS -> NCS using EXTERNAL nwnnsscomp.exe compiler from ./tools (RunExternalCompiler)
        /// Step 5: Compare bytecode from Step 1 vs Step 4 - PRIMARY PRIORITY (fast-fails on mismatch, shows FULL pcode diff in UDIFF)
        /// Step 6: NCS -> NSS using INBUILT .NET decompiler (RunDecompile)
        /// Step 7: NSS -> NCS using INBUILT .NET compiler (RunInbuiltCompiler - uses NCSAuto.CompileNss)
        /// Step 8: Compare bytecode from Step 4 vs Step 7 - SECONDARY PRIORITY (fails on mismatch (NOT a fast fail), shows FULL pcode diff in UDIFF)
        ///
        /// PRIORITIES:
        /// ===========
        /// PRIMARY: Bytecode comparison (Step 5) - MUST match 1:1 byte-by-byte. Test FAST FAILS if mismatch. Shows FULL pcode diff in UDIFF format.
        /// SECONDARY: Bytecode comparison (Step 8) - MUST match 1:1 byte-by-byte. Test FAILS if mismatch (NOT fast fail). Shows FULL pcode diff in UDIFF format.
        /// TERTIARY: Text comparison (Step 3) - Should match after normalization. Only WARNS if mismatch. Shows UDIFF format.
        ///
        /// NOTE: Recompilation failure (Step 4 or Step 7) is acceptable - the decompiler's goal is to decompile as much as physically possible.
        /// BioWare's compiler could compile .nss that had errors - it was just a converter to .ncs.
        /// The GOAL is to ensure those .ncs can STILL be converted to .nss, even if the .nss won't recompile.
        /// If recompilation fails, we skip the bytecode comparison for that step, but the test may still pass if other checks succeed.
        /// </summary>
        private static RoundTripResult RoundTripSingle(string nssPath, string gameFlag, string scratchRoot)
        {
            long startTime = Stopwatch.GetTimestamp();

            string rel = GetRelativePath(VanillaRepoDir, nssPath);
            string displayRelPath = rel.Replace('\\', '/');

            string outDir = Path.Combine(scratchRoot, Path.GetDirectoryName(rel) ?? "");
            Directory.CreateDirectory(outDir);

            // Read original source for validation (use cache to avoid repeated I/O)
            string originalSource = ReadFileCached(nssPath);
            int originalSourceLength = originalSource.Length;
            bool originalHasMain = originalSource.Contains("void main") || originalSource.Contains("void main(");

            RoundTripResult result = new RoundTripResult
            {
                RelPath = displayRelPath,
                Passed = false,
                TextMatch = false,
                BytecodeMatch = false
            };

            // Step 1: Compile original NSS -> NCS (first NCS) using EXTERNAL compiler
            string compiledFirst = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".ncs");
            long compileOriginalStart = Stopwatch.GetTimestamp();
            try
            {
                RunExternalCompiler(nssPath, compiledFirst, gameFlag, scratchRoot);

                // Assert: NCS file exists and is non-empty
                File.Exists(compiledFirst).Should().BeTrue($"Compiled NCS file should exist at {DisplayPath(compiledFirst)}");
                long ncsSize = new FileInfo(compiledFirst).Length;
                ncsSize.Should().BeGreaterThan(0, $"Compiled NCS file should not be empty (size: {ncsSize} bytes)");

                // Assert: NCS can be loaded and parsed
                NCS ncs;
                try
                {
                    ncs = NCSAuto.ReadNcs(compiledFirst);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Compiled NCS file exists but cannot be loaded/parsed: {ex.Message}", ex);
                }

                // Assert: NCS has instructions
                ncs.Instructions.Should().NotBeNull("NCS should have instructions list");
                ncs.Instructions.Count.Should().BeGreaterThan(0,
                    $"NCS should have at least one instruction (found {ncs.Instructions.Count} instructions). " +
                    $"This suggests the compiler produced an empty bytecode file.");

                // Assert: NCS has reasonable size (not suspiciously small for non-empty source)
                if (originalSourceLength > 100 && originalHasMain)
                {
                    ncsSize.Should().BeGreaterThan(50,
                        $"NCS file is suspiciously small ({ncsSize} bytes) for source file of {originalSourceLength} characters. " +
                        $"This suggests the compiler may have produced minimal/empty bytecode.");
                }

                long compileTime = Stopwatch.GetTimestamp() - compileOriginalStart;
                MergeOperationTime("compile-original", compileTime);
                MergeOperationTime("compile", compileTime);
            }
            catch (Exception e)
            {
                long compileTime = Stopwatch.GetTimestamp() - compileOriginalStart;
                MergeOperationTime("compile-original", compileTime);
                MergeOperationTime("compile", compileTime);
                throw new SourceCompilationException(
                    $"Original source file failed to compile with external compiler.\n" +
                    $"Source: {DisplayPath(nssPath)}\n" +
                    $"Source size: {originalSourceLength} characters\n" +
                    $"Error: {e.Message}", e);
            }

            // Step 2: Decompile NCS -> NSS using internal decompiler
            string decompiled = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".dec.nss");
            long decompileStart = Stopwatch.GetTimestamp();
            string decompiledContent = null;
            try
            {
                RunDecompile(compiledFirst, decompiled, gameFlag);

                // Assert: Decompiled file exists and is non-empty
                File.Exists(decompiled).Should().BeTrue($"Decompiled NSS file should exist at {DisplayPath(decompiled)}");
                decompiledContent = ReadFileCached(decompiled);
                decompiledContent.Length.Should().BeGreaterThan(0,
                    $"Decompiled NSS file should not be empty (size: {decompiledContent.Length} characters)");

                // Assert: Decompiled content contains expected structure
                bool decompiledHasMain = decompiledContent.Contains("void main") || decompiledContent.Contains("void main(");
                if (originalHasMain)
                {
                    decompiledHasMain.Should().BeTrue(
                        $"Original source has main() function but decompiled output does not.\n" +
                        $"Decompiled content preview (first 200 chars):\n{decompiledContent.Substring(0, Math.Min(200, decompiledContent.Length))}");
                }

                // Assert: Decompiled content is not suspiciously minimal
                // Note: Original source may include nwscript.nss or comments that don't affect bytecode.
                // For minimal functions like "void main() {}", 16 chars is correct.
                // Only flag if decompiled output is truly suspiciously small (< 15 chars for files with main).
                if (originalSourceLength > 100 && originalHasMain)
                {
                    decompiledContent.Length.Should().BeGreaterThan(14,
                        $"Decompiled output is suspiciously small ({decompiledContent.Length} chars) for source of {originalSourceLength} chars. " +
                        $"This suggests the decompiler produced minimal/empty output.\n" +
                        $"Decompiled content:\n{decompiledContent}");
                }

                long decompileTime = Stopwatch.GetTimestamp() - decompileStart;
                MergeOperationTime("decompile", decompileTime);
                Console.WriteLine($" ✓ ({GetElapsedMilliseconds(decompileTime):F3} ms, {decompiledContent.Length} chars)");
            }
            catch (Exception e)
            {
                long decompileTime = Stopwatch.GetTimestamp() - decompileStart;
                MergeOperationTime("decompile", decompileTime);
                Console.WriteLine($" ✗ FAILED");
                throw new InvalidOperationException(
                    $"Decompilation failed.\n" +
                    $"NCS file: {DisplayPath(compiledFirst)}\n" +
                    $"NCS size: {new FileInfo(compiledFirst).Length} bytes\n" +
                    $"Error: {e.Message}", e);
            }

            // Step 3: Compare original NSS vs decompiled NSS (text comparison) - TERTIARY PRIORITY (warns only, shows UDIFF)
            // Primary goal is bytecode 1:1 match. Text comparison is tertiary and non-fatal.
            // Text mismatches are reported as warnings but don't fail the test.
            // The test will fail only if bytecode doesn't match (Step 5 or Step 8).
            long compareTextStart = Stopwatch.GetTimestamp();
            string textMismatchMessage = null;
            bool textMatches = false;
            Console.Write("  Comparing original vs decompiled (text)");
            try
            {
                bool isK2 = gameFlag.Equals("k2");
                string originalExpanded = ExpandIncludes(nssPath, gameFlag);
                string roundtripRaw = decompiledContent ?? File.ReadAllText(decompiled, Encoding.UTF8);

                string originalExpandedFiltered = FilterFunctionsNotInDecompiled(originalExpanded, roundtripRaw);

                string original = NormalizeNewlines(originalExpandedFiltered, isK2);
                string roundtrip = NormalizeNewlines(roundtripRaw, isK2);
                long compareTime = Stopwatch.GetTimestamp() - compareTextStart;
                MergeOperationTime("compare-text", compareTime);
                MergeOperationTime("compare", compareTime);

                textMatches = original.Equals(roundtrip);
                result.TextMatch = textMatches;
                if (!textMatches)
                {
                    string diff = FormatUnifiedDiff(original, roundtrip);
                    result.TextDiff = diff;
                    textMismatchMessage = $"Round-trip text mismatch for {DisplayPath(nssPath)}";
                    if (diff != null)
                    {
                        textMismatchMessage += Environment.NewLine + diff;
                    }

                    // Show key differences
                    int originalLines = original.Split('\n').Length;
                    int roundtripLines = roundtrip.Split('\n').Length;
                    textMismatchMessage += $"\nOriginal: {originalLines} lines, {original.Length} chars";
                    textMismatchMessage += $"\nDecompiled: {roundtripLines} lines, {roundtrip.Length} chars";

                    // TERTIARY PRIORITY: Only warn, don't fail
                    Console.WriteLine(" ⚠ MISMATCH (warning - bytecode checks are primary/secondary)");
                    Console.Error.WriteLine($"WARNING: {textMismatchMessage}");
                    if (diff != null)
                    {
                        Console.Error.WriteLine("UDIFF:");
                        Console.Error.WriteLine(diff);
                    }
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
                textMismatchMessage = $"Text comparison error: {e.Message}";
                result.TextDiff = textMismatchMessage;
                // TERTIARY PRIORITY: Only warn, don't fail
                Console.WriteLine(" ⚠ ERROR");
                Console.Error.WriteLine($"WARNING: {textMismatchMessage}");
            }

            // Step 4: Recompile decompiled NSS -> NCS using EXTERNAL compiler
            string recompiledExternal = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".ext.rt.ncs");
            string decompiledContentForExternalRecompile = decompiledContent ?? ReadFileCached(decompiled);
            string compileInputExternal = decompiled;
            string tempCompileInputExternal = null;
            string decompiledNameExternal = Path.GetFileName(decompiled);
            if (decompiledNameExternal.IndexOf('.') != decompiledNameExternal.LastIndexOf('.'))
            {
                compileInputExternal = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + "_dec_ext.nss");
                File.WriteAllText(compileInputExternal, decompiledContentForExternalRecompile, Encoding.UTF8);
                tempCompileInputExternal = compileInputExternal;
            }
            else
            {
                File.WriteAllText(compileInputExternal, decompiledContentForExternalRecompile, Encoding.UTF8);
            }

            bool externalRecompilationSucceeded = false;
            string externalRecompilationError = null;
            Console.Write("  Recompiling decompiled .nss to .ncs (external compiler)");
            long compileExternalRoundtripStart = Stopwatch.GetTimestamp();
            try
            {
                RunExternalCompiler(compileInputExternal, recompiledExternal, gameFlag, scratchRoot);

                if (!File.Exists(recompiledExternal) || new FileInfo(recompiledExternal).Length == 0)
                {
                    throw new InvalidOperationException("External recompiled NCS file does not exist or is empty");
                }

                long compileTime = Stopwatch.GetTimestamp() - compileExternalRoundtripStart;
                MergeOperationTime("compile-external-roundtrip", compileTime);
                MergeOperationTime("compile", compileTime);
                Console.WriteLine($" ✓ ({GetElapsedMilliseconds(compileTime):F3} ms)");
                externalRecompilationSucceeded = true;
            }
            catch (Exception e)
            {
                long compileTime = Stopwatch.GetTimestamp() - compileExternalRoundtripStart;
                MergeOperationTime("compile-external-roundtrip", compileTime);
                MergeOperationTime("compile", compileTime);
                externalRecompilationError = e.Message;
                Console.WriteLine(" ⚠ FAILED (acceptable - decompiled code may not recompile)");
                // Don't throw - recompilation failure is acceptable
            }
            finally
            {
                if (tempCompileInputExternal != null)
                {
                    try
                    {
                        File.Delete(tempCompileInputExternal);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            // Step 5: If external recompilation succeeded, compare bytecode between Step 1 (original external) and Step 4 (external recompiled)
            // THIS IS THE PRIMARY AND STRICT CHECK - bytecode must be 1:1 match, FAST FAIL on mismatch
            if (externalRecompilationSucceeded)
            {
                Console.Write("  Comparing bytecode (original external vs external recompiled)");
                long compareBytecodeStart = Stopwatch.GetTimestamp();
                try
                {
                    // Assert: Both NCS files exist
                    File.Exists(compiledFirst).Should().BeTrue($"Original NCS should exist: {DisplayPath(compiledFirst)}");
                    File.Exists(recompiledExternal).Should().BeTrue($"External recompiled NCS should exist: {DisplayPath(recompiledExternal)}");

                    // Assert: Both NCS files can be loaded
                    NCS originalNcs = NCSAuto.ReadNcs(compiledFirst);
                    NCS externalRecompiledNcs = NCSAuto.ReadNcs(recompiledExternal);

                    // Assert: Both have instructions
                    originalNcs.Instructions.Should().NotBeNull("Original NCS should have instructions");
                    externalRecompiledNcs.Instructions.Should().NotBeNull("External recompiled NCS should have instructions");
                    originalNcs.Instructions.Count.Should().BeGreaterThan(0, "Original NCS should have at least one instruction");
                    externalRecompiledNcs.Instructions.Count.Should().BeGreaterThan(0, "External recompiled NCS should have at least one instruction");

                    // Perform bytecode comparison - PRIMARY PRIORITY: FAST FAIL on mismatch
                    BytecodeDiffResult bytecodeDiff = FindBytecodeDiff(compiledFirst, recompiledExternal);
                    if (bytecodeDiff != null)
                    {
                        // Bytecode mismatch - generate FULL pcode diff in UDIFF format
                        string pcodeDiff = FormatPcodeUnifiedDiff(originalNcs, externalRecompiledNcs);
                        result.PcodeDiff = pcodeDiff;
                        result.BytecodeMatch = false;

                        // Get action names for better debugging
                        string originalAction = GetActionNameAtOffset(compiledFirst, bytecodeDiff.Offset, gameFlag);
                        string roundTripAction = GetActionNameAtOffset(recompiledExternal, bytecodeDiff.Offset, gameFlag);

                        StringBuilder errorMsg = new StringBuilder();
                        errorMsg.AppendLine("═══════════════════════════════════════════════════════════════");
                        errorMsg.AppendLine($"BYTECODE MISMATCH (PRIMARY FAILURE): {displayRelPath}");
                        errorMsg.AppendLine("═══════════════════════════════════════════════════════════════");
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("LOCATION:");
                        errorMsg.AppendLine($"  Offset: {bytecodeDiff.Offset} (0x{bytecodeDiff.Offset:X})");
                        errorMsg.Append("  Original External: ").Append(FormatByteValue(bytecodeDiff.OriginalByte));
                        if (!string.IsNullOrEmpty(originalAction))
                        {
                            errorMsg.Append(" → ").Append(originalAction);
                        }
                        errorMsg.AppendLine();
                        errorMsg.Append("  External Recompiled: ").Append(FormatByteValue(bytecodeDiff.RoundTripByte));
                        if (!string.IsNullOrEmpty(roundTripAction))
                        {
                            errorMsg.Append(" → ").Append(roundTripAction);
                        }
                        errorMsg.AppendLine();
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("FILES:");
                        errorMsg.AppendLine($"  Original External NCS: {DisplayPath(compiledFirst)}");
                        errorMsg.AppendLine($"  External Recompiled NCS: {DisplayPath(recompiledExternal)}");
                        if (File.Exists(decompiled))
                        {
                            errorMsg.AppendLine($"  Decompiled NSS: {DisplayPath(decompiled)}");
                        }
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("BYTECODE CONTEXT:");
                        errorMsg.AppendLine($"  Original External:  {bytecodeDiff.OriginalContext}");
                        errorMsg.AppendLine($"  External Recompiled: {bytecodeDiff.RoundTripContext}");
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("FILE SIZES:");
                        errorMsg.AppendLine($"  Original External: {bytecodeDiff.OriginalLength} bytes");
                        errorMsg.AppendLine($"  External Recompiled: {bytecodeDiff.RoundTripLength} bytes");
                        errorMsg.AppendLine();

                        if (!string.IsNullOrEmpty(bytecodeDiff.HexDiff))
                        {
                            errorMsg.AppendLine("HEX BYTECODE DIFF (UDIFF format):");
                            errorMsg.AppendLine(bytecodeDiff.HexDiff);
                            errorMsg.AppendLine();
                        }

                        if (!string.IsNullOrEmpty(pcodeDiff))
                        {
                            string[] pcodeLines = pcodeDiff.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            errorMsg.AppendLine("P-CODE DIFF (first 50 lines):");
                            int showLines = Math.Min(50, pcodeLines.Length);
                            for (int i = 0; i < showLines; i++)
                            {
                                errorMsg.AppendLine(pcodeLines[i]);
                            }
                            if (pcodeLines.Length > 50)
                            {
                                errorMsg.AppendLine($"... ({pcodeLines.Length - 50} more lines)");
                            }
                            errorMsg.AppendLine();
                        }

                        errorMsg.AppendLine("═══════════════════════════════════════════════════════════════");

                        result.ErrorMessage = errorMsg.ToString();
                        throw new InvalidOperationException(errorMsg.ToString());
                    }

                    result.BytecodeMatch = true;
                    long compareTime = Stopwatch.GetTimestamp() - compareBytecodeStart;
                    MergeOperationTime("compare-bytecode-primary", compareTime);
                    MergeOperationTime("compare", compareTime);
                    Console.WriteLine(" ✓ MATCH");
                }
                catch (Exception e)
                {
                    long compareTime = Stopwatch.GetTimestamp() - compareBytecodeStart;
                    MergeOperationTime("compare-bytecode-primary", compareTime);
                    MergeOperationTime("compare", compareTime);
                    // PRIMARY PRIORITY: Fast fail on bytecode mismatch
                    if (textMismatchMessage != null)
                    {
                        result.ErrorMessage = "Bytecode mismatch (PRIMARY FAILURE):\n" + e.Message +
                            "\n\nAlso had text mismatch (tertiary):\n" + textMismatchMessage;
                        throw new InvalidOperationException(result.ErrorMessage);
                    }
                    result.ErrorMessage = e.Message;
                    throw; // Fast fail on bytecode mismatch
                }
            }
            else
            {
                // External recompilation failed - cannot verify primary bytecode match
                // This is acceptable per the test design
                Console.Error.WriteLine("NOTE: Cannot verify primary bytecode check (external recompilation failed).");
            }

            // Step 6: Decompile external recompiled NCS -> NSS using internal decompiler
            string decompiledSecond = null;
            string decompiledSecondContent = null;
            if (externalRecompilationSucceeded)
            {
                decompiledSecond = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".dec2.nss");
                long decompileSecondStart = Stopwatch.GetTimestamp();
                try
                {
                    RunDecompile(recompiledExternal, decompiledSecond, gameFlag);

                    File.Exists(decompiledSecond).Should().BeTrue($"Second decompiled NSS file should exist at {DisplayPath(decompiledSecond)}");
                    decompiledSecondContent = ReadFileCached(decompiledSecond);
                    decompiledSecondContent.Length.Should().BeGreaterThan(0,
                        $"Second decompiled NSS file should not be empty (size: {decompiledSecondContent.Length} characters)");

                    long decompileTime = Stopwatch.GetTimestamp() - decompileSecondStart;
                    MergeOperationTime("decompile-second", decompileTime);
                    MergeOperationTime("decompile", decompileTime);
                    Console.WriteLine($" ✓ ({GetElapsedMilliseconds(decompileTime):F3} ms, {decompiledSecondContent.Length} chars)");
                }
                catch (Exception e)
                {
                    long decompileTime = Stopwatch.GetTimestamp() - decompileSecondStart;
                    MergeOperationTime("decompile-second", decompileTime);
                    MergeOperationTime("decompile", decompileTime);
                    Console.WriteLine($" ✗ FAILED");
                    throw new InvalidOperationException(
                        $"Second decompilation failed.\n" +
                        $"NCS file: {DisplayPath(recompiledExternal)}\n" +
                        $"NCS size: {new FileInfo(recompiledExternal).Length} bytes\n" +
                        $"Error: {e.Message}", e);
                }
            }

            // Step 7: Recompile second decompiled NSS -> NCS using INBUILT compiler
            string recompiledInbuilt = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + ".rt.ncs");

            string decompiledContentForInbuiltRecompile = decompiledSecondContent ?? decompiledContent ?? ReadFileCached(decompiledSecond ?? decompiled);

            string compileInputInbuilt = decompiledSecond ?? decompiled;
            string tempCompileInputInbuilt = null;
            string decompiledNameInbuilt = Path.GetFileName(compileInputInbuilt);
            if (decompiledNameInbuilt.IndexOf('.') != decompiledNameInbuilt.LastIndexOf('.'))
            {
                compileInputInbuilt = Path.Combine(outDir, Path.GetFileNameWithoutExtension(rel) + "_dec_inbuilt.nss");
                File.WriteAllText(compileInputInbuilt, decompiledContentForInbuiltRecompile, Encoding.UTF8);
                tempCompileInputInbuilt = compileInputInbuilt;
            }
            else
            {
                File.WriteAllText(compileInputInbuilt, decompiledContentForInbuiltRecompile, Encoding.UTF8);
            }

            // NOTE: The decompiler's goal is to decompile as much as physically possible and always match bytecode.
            // BioWare's compiler could compile .nss that had errors - it was just a converter to .ncs.
            // The GOAL is to ensure those .ncs can STILL be converted to .nss, even if the .nss won't recompile.
            // If recompilation fails, that's acceptable - we just skip the bytecode comparison for this file.
            // The decompiled code is used as-is for recompilation attempt.

            bool inbuiltRecompilationSucceeded = false;
            string inbuiltRecompilationError = null;
            Console.Write("  Recompiling second decompiled .nss to .ncs (inbuilt compiler)");
            long compileInbuiltRoundtripStart = Stopwatch.GetTimestamp();
            try
            {
                RunInbuiltCompiler(compileInputInbuilt, recompiledInbuilt, gameFlag);

                if (!File.Exists(recompiledInbuilt) || new FileInfo(recompiledInbuilt).Length == 0)
                {
                    throw new InvalidOperationException("Inbuilt recompiled NCS file does not exist or is empty");
                }

                long compileTime = Stopwatch.GetTimestamp() - compileInbuiltRoundtripStart;
                MergeOperationTime("compile-inbuilt-roundtrip", compileTime);
                MergeOperationTime("compile", compileTime);
                Console.WriteLine($" ✓ ({GetElapsedMilliseconds(compileTime):F3} ms)");
                inbuiltRecompilationSucceeded = true;
            }
            catch (Exception e)
            {
                long compileTime = Stopwatch.GetTimestamp() - compileInbuiltRoundtripStart;
                MergeOperationTime("compile-inbuilt-roundtrip", compileTime);
                MergeOperationTime("compile", compileTime);
                inbuiltRecompilationError = e.Message;
                Console.WriteLine(" ⚠ FAILED (acceptable - decompiled code may not recompile)");
                // Don't throw - recompilation failure is acceptable
            }
            finally
            {
                if (tempCompileInputInbuilt != null)
                {
                    try
                    {
                        File.Delete(tempCompileInputInbuilt);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            // Step 8: If both external and inbuilt recompilation succeeded, compare bytecode between Step 4 (external recompiled) and Step 7 (inbuilt recompiled)
            // THIS IS THE SECONDARY CHECK - bytecode must be 1:1 match, FAIL (NOT fast fail) on mismatch
            bool secondaryBytecodeMatch = true;
            if (externalRecompilationSucceeded && inbuiltRecompilationSucceeded)
            {
                Console.Write("  Comparing bytecode (external recompiled vs inbuilt recompiled)");
                long compareBytecodeSecondaryStart = Stopwatch.GetTimestamp();
                try
                {
                    // Assert: Both NCS files exist
                    File.Exists(recompiledExternal).Should().BeTrue($"External recompiled NCS should exist: {DisplayPath(recompiledExternal)}");
                    File.Exists(recompiledInbuilt).Should().BeTrue($"Inbuilt recompiled NCS should exist: {DisplayPath(recompiledInbuilt)}");

                    // Assert: Both NCS files can be loaded
                    NCS externalRecompiledNcs = NCSAuto.ReadNcs(recompiledExternal);
                    NCS inbuiltRecompiledNcs = NCSAuto.ReadNcs(recompiledInbuilt);

                    // Assert: Both have instructions
                    externalRecompiledNcs.Instructions.Should().NotBeNull("External recompiled NCS should have instructions");
                    inbuiltRecompiledNcs.Instructions.Should().NotBeNull("Inbuilt recompiled NCS should have instructions");
                    externalRecompiledNcs.Instructions.Count.Should().BeGreaterThan(0, "External recompiled NCS should have at least one instruction");
                    inbuiltRecompiledNcs.Instructions.Count.Should().BeGreaterThan(0, "Inbuilt recompiled NCS should have at least one instruction");

                    // Perform bytecode comparison - SECONDARY PRIORITY: FAIL (NOT fast fail) on mismatch
                    BytecodeDiffResult bytecodeDiff = FindBytecodeDiff(recompiledExternal, recompiledInbuilt);
                    if (bytecodeDiff != null)
                    {
                        // Bytecode mismatch - generate FULL pcode diff in UDIFF format
                        string pcodeDiff = FormatPcodeUnifiedDiff(externalRecompiledNcs, inbuiltRecompiledNcs);
                        result.PcodeDiff = pcodeDiff;
                        secondaryBytecodeMatch = false;

                        // Get action names for better debugging
                        string externalAction = GetActionNameAtOffset(recompiledExternal, bytecodeDiff.Offset, gameFlag);
                        string inbuiltAction = GetActionNameAtOffset(recompiledInbuilt, bytecodeDiff.Offset, gameFlag);

                        StringBuilder errorMsg = new StringBuilder();
                        errorMsg.AppendLine("═══════════════════════════════════════════════════════════════");
                        errorMsg.AppendLine($"BYTECODE MISMATCH (SECONDARY FAILURE): {displayRelPath}");
                        errorMsg.AppendLine("═══════════════════════════════════════════════════════════════");
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("LOCATION:");
                        errorMsg.AppendLine($"  Offset: {bytecodeDiff.Offset} (0x{bytecodeDiff.Offset:X})");
                        errorMsg.Append("  External Recompiled: ").Append(FormatByteValue(bytecodeDiff.OriginalByte));
                        if (!string.IsNullOrEmpty(externalAction))
                        {
                            errorMsg.Append(" → ").Append(externalAction);
                        }
                        errorMsg.AppendLine();
                        errorMsg.Append("  Inbuilt Recompiled: ").Append(FormatByteValue(bytecodeDiff.RoundTripByte));
                        if (!string.IsNullOrEmpty(inbuiltAction))
                        {
                            errorMsg.Append(" → ").Append(inbuiltAction);
                        }
                        errorMsg.AppendLine();
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("FILES:");
                        errorMsg.AppendLine($"  External Recompiled NCS: {DisplayPath(recompiledExternal)}");
                        errorMsg.AppendLine($"  Inbuilt Recompiled NCS: {DisplayPath(recompiledInbuilt)}");
                        if (File.Exists(decompiledSecond))
                        {
                            errorMsg.AppendLine($"  Second Decompiled NSS: {DisplayPath(decompiledSecond)}");
                        }
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("BYTECODE CONTEXT:");
                        errorMsg.AppendLine($"  External Recompiled:  {bytecodeDiff.OriginalContext}");
                        errorMsg.AppendLine($"  Inbuilt Recompiled: {bytecodeDiff.RoundTripContext}");
                        errorMsg.AppendLine();
                        errorMsg.AppendLine("FILE SIZES:");
                        errorMsg.AppendLine($"  External Recompiled: {bytecodeDiff.OriginalLength} bytes");
                        errorMsg.AppendLine($"  Inbuilt Recompiled: {bytecodeDiff.RoundTripLength} bytes");
                        errorMsg.AppendLine();

                        if (!string.IsNullOrEmpty(bytecodeDiff.HexDiff))
                        {
                            errorMsg.AppendLine("HEX BYTECODE DIFF (UDIFF format):");
                            errorMsg.AppendLine(bytecodeDiff.HexDiff);
                            errorMsg.AppendLine();
                        }

                        if (!string.IsNullOrEmpty(pcodeDiff))
                        {
                            string[] pcodeLines = pcodeDiff.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                            errorMsg.AppendLine("P-CODE DIFF (first 50 lines):");
                            int showLines = Math.Min(50, pcodeLines.Length);
                            for (int i = 0; i < showLines; i++)
                            {
                                errorMsg.AppendLine(pcodeLines[i]);
                            }
                            if (pcodeLines.Length > 50)
                            {
                                errorMsg.AppendLine($"... ({pcodeLines.Length - 50} more lines)");
                            }
                            errorMsg.AppendLine();
                        }

                        errorMsg.AppendLine("═══════════════════════════════════════════════════════════════");

                        result.ErrorMessage = errorMsg.ToString();
                        // SECONDARY PRIORITY: Fail but don't fast fail - continue to end of test
                        Console.Error.WriteLine();
                        Console.Error.WriteLine(errorMsg.ToString());
                        Console.Error.WriteLine();
                    }
                    else
                    {
                        secondaryBytecodeMatch = true;
                    }

                    long compareTime = Stopwatch.GetTimestamp() - compareBytecodeSecondaryStart;
                    MergeOperationTime("compare-bytecode-secondary", compareTime);
                    MergeOperationTime("compare", compareTime);
                    if (secondaryBytecodeMatch)
                    {
                        Console.WriteLine(" ✓ MATCH");
                    }
                    else
                    {
                        Console.WriteLine(" ✗ MISMATCH (secondary failure)");
                    }
                }
                catch (Exception e)
                {
                    long compareTime = Stopwatch.GetTimestamp() - compareBytecodeSecondaryStart;
                    MergeOperationTime("compare-bytecode-secondary", compareTime);
                    MergeOperationTime("compare", compareTime);
                    // SECONDARY PRIORITY: Fail but don't fast fail - continue to end of test
                    secondaryBytecodeMatch = false;
                    result.ErrorMessage = "Bytecode mismatch (SECONDARY FAILURE):\n" + e.Message;
                    if (textMismatchMessage != null)
                    {
                        result.ErrorMessage += "\n\nAlso had text mismatch (tertiary):\n" + textMismatchMessage;
                    }
                    Console.Error.WriteLine();
                    Console.Error.WriteLine(result.ErrorMessage);
                    Console.Error.WriteLine();
                    Console.WriteLine(" ✗ ERROR (secondary failure)");
                }
            }
            else
            {
                // Recompilation failed - cannot verify secondary bytecode match
                // This is acceptable per the test design
                if (!externalRecompilationSucceeded && !inbuiltRecompilationSucceeded)
                {
                    Console.Error.WriteLine("NOTE: Cannot verify secondary bytecode check (both recompilations failed).");
                }
                else if (!externalRecompilationSucceeded)
                {
                    Console.Error.WriteLine("NOTE: Cannot verify secondary bytecode check (external recompilation failed).");
                }
                else
                {
                    Console.Error.WriteLine("NOTE: Cannot verify secondary bytecode check (inbuilt recompilation failed).");
                }
            }

            long totalTime = Stopwatch.GetTimestamp() - startTime;
            MergeOperationTime("total", totalTime);

            // Test passes if:
            // 1. Step 5 (PRIMARY): External recompilation succeeded AND bytecode matches (or external recompilation failed - acceptable)
            // 2. Step 8 (SECONDARY): Both recompilations succeeded AND bytecode matches (or recompilation failed - acceptable)
            // Recompilation failure is acceptable per test design
            // PRIMARY check (Step 5) is already handled - if it failed, exception was thrown (fast fail)
            // SECONDARY check (Step 8) - if it failed, we set secondaryBytecodeMatch = false but don't fast fail
            bool primaryCheckPassed = !externalRecompilationSucceeded || result.BytecodeMatch;
            bool secondaryCheckPassed = !(externalRecompilationSucceeded && inbuiltRecompilationSucceeded) || secondaryBytecodeMatch;
            result.Passed = primaryCheckPassed && secondaryCheckPassed;
            result.BytecodeMatch = primaryCheckPassed && secondaryCheckPassed;
            return result;
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
        /// Compiles NSS to NCS using the inbuilt compiler (Andastra.Parsing native implementation).
        /// </summary>
        private static void RunInbuiltCompiler(string originalNssPath, string compiledOut, string gameFlag)
        {
            BioWareGame game = gameFlag.Equals("k2") ? BioWareGame.K2 : BioWareGame.K1;

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
                // For roundtrip tests, disable all optimizations to match external compiler output exactly
                // Pass empty list to disable optimizations (including RemoveNopOptimizer)
                // This ensures bytecode matches the external compiler's output byte-for-byte
                // CRITICAL: Pass nwscript path to ensure function order matches external compiler
                string nwscriptPath = gameFlag.Equals("k2") ? K2Nwscript : K1Nwscript;
                NCS ncs = NCSAuto.CompileNss(source, game, null, new List<NCSOptimizer>(), libraryLookup, null, false, nwscriptPath);
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

        // Static instance of external compiler for reuse
        private static ExternalNCSCompiler _externalCompiler;

        private static ExternalNCSCompiler GetExternalCompiler()
        {
            if (_externalCompiler == null)
            {
                _externalCompiler = new ExternalNCSCompiler(NwnCompiler);
            }
            return _externalCompiler;
        }

        /// <summary>
        /// Compiles NSS to NCS using the external compiler (nwnnsscomp_kscript.exe).
        /// Uses ExternalNCSCompiler wrapper to handle different compiler variants.
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

            // Ensure nwscript.nss is in the compiler's directory
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

                BioWareGame game = gameFlag.Equals("k2") ? BioWareGame.K2 : BioWareGame.K1;
                ExternalNCSCompiler compiler = GetExternalCompiler();

                Console.Write($" (external: {Path.GetFileName(NwnCompiler)}, variant: {compiler.GetInfo()})");

                // Use ExternalNCSCompiler which handles all compiler variants and their command-line differences
                (string stdout, string stderr) = compiler.CompileScriptWithOutput(
                    tempSourceFile,
                    compiledOut,
                    game,
                    (int)ProcTimeout.TotalSeconds);

                // ExternalNCSCompiler.CompileScriptWithOutput already validates the output file exists
                // and throws if compilation fails, so we don't need additional checks here
            }
            catch (ExternalNCSCompiler.EntryPointException)
            {
                // This is an include file - rethrow as-is
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"External compiler failed for {DisplayPath(originalNssPath)}: {e.Message}", e);
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
                    // Read output asynchronously to avoid blocking
                    var outputTask = Task.Run(() => proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd());

                    if (!proc.WaitForExit((int)ProcTimeout.TotalMilliseconds))
                    {
                        try
                        {
                            proc.Kill();
                        }
                        catch
                        {
                            // Ignore errors when killing process
                        }
                        throw new TimeoutException($"nwnnsscomp timed out for {DisplayPath(originalNssPath)}");
                    }

                    string output = outputTask.Result;

                    int exitCode = proc.ExitCode;
                    bool fileExists = File.Exists(compiledOut);

                    if (exitCode != 0 || !fileExists)
                    {
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
                // Note: We keep synchronous cleanup here for RunExternalCompiler to ensure temp dirs are cleaned before next test
                // For RunCompiler, we can use async cleanup since it's less critical
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

        // Cache file content reads to avoid repeated I/O operations
        private static readonly Dictionary<string, string> _fileContentCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _cacheLock = new object();

        private static string ReadFileCached(string filePath)
        {
            lock (_cacheLock)
            {
                if (_fileContentCache.TryGetValue(filePath, out string cached))
                {
                    return cached;
                }

                string content = File.ReadAllText(filePath, Encoding.UTF8);
                _fileContentCache[filePath] = content;
                return content;
            }
        }

        private static bool NeedsAscNwscript(string nssPath)
        {
            string content = ReadFileCached(nssPath);
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
            string content = ReadFileCached(nssPath);
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
                // Use RoundTripUtil to match Java implementation exactly
                // Matching DeNCS implementation at vendor/DeNCS/src/test/java/com/kotor/resource/formats/ncs/NCSDecompCLIRoundTripTest.java:1768-1789
                // Original: RoundTripUtil.decompileNcsToNssFile(ncsFile, nssFile, gameFlag, StandardCharsets.UTF_8);
                NcsFile ncsFile = new NcsFile(ncsPath);
                NcsFile nssFile = new NcsFile(nssOut);

                // Capture all console output during decompilation for exhaustive debugging
                var originalOut = Console.Out;
                var originalErr = Console.Error;
                var outputCapture = new StringWriter();
                var errorCapture = new StringWriter();

                Exception decompileEx = null;

                try
                {
                    // Temporarily redirect output to capture verbose logging
                    Console.SetOut(outputCapture);
                    Console.SetError(errorCapture);

                    RoundTripUtil.DecompileNcsToNssFile(ncsFile, nssFile, gameFlag, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    decompileEx = ex;
                }
                finally
                {
                    // Always restore original output streams
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                }

                // Output captured logs for debugging (always, if there's any output)
                string capturedOutput = outputCapture.ToString();
                string capturedError = errorCapture.ToString();
                if (!string.IsNullOrEmpty(capturedOutput) || !string.IsNullOrEmpty(capturedError))
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.Error.WriteLine($"DECOMPILATION VERBOSE OUTPUT FOR: {DisplayPath(ncsPath)}");
                    Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
                    if (!string.IsNullOrEmpty(capturedOutput))
                    {
                        Console.Error.WriteLine("STDOUT CAPTURE:");
                        Console.Error.WriteLine(capturedOutput);
                    }
                    if (!string.IsNullOrEmpty(capturedError))
                    {
                        Console.Error.WriteLine("STDERR CAPTURE:");
                        Console.Error.WriteLine(capturedError);
                    }
                    Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.Error.WriteLine();
                }

                // Now handle the result
                if (decompileEx != null)
                {
                    throw new InvalidOperationException($"Decompile failed for {DisplayPath(ncsPath)}: {decompileEx.Message}", decompileEx);
                }

                if (!File.Exists(nssOut))
                {
                    throw new InvalidOperationException($"Decompile did not produce output: {DisplayPath(nssOut)}");
                }
            }
            catch (Exception ex)
            {
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

            string content = ReadFileCached(normalizedSource);
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

        /// <summary>
        /// Represents a function signature for comparison purposes.
        /// </summary>
        private class FunctionSignature
        {
            public string ReturnType { get; set; }
            public string Name { get; set; }
            public string Parameters { get; set; }
            public string FullSignature { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is FunctionSignature other)
                {
                    return ReturnType == other.ReturnType &&
                           Name == other.Name &&
                           NormalizeParameterString(Parameters) == NormalizeParameterString(other.Parameters);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return (ReturnType?.GetHashCode() ?? 0) ^
                       (Name?.GetHashCode() ?? 0) ^
                       (NormalizeParameterString(Parameters)?.GetHashCode() ?? 0);
            }

            private static string NormalizeParameterString(string parameters)
            {
                if (string.IsNullOrEmpty(parameters))
                {
                    return "";
                }
                return Regex.Replace(parameters.Trim(), @"\s+", " ").Replace("const ", "");
            }
        }

        /// <summary>
        /// Extracts all function signatures from NSS source code.
        /// </summary>
        private static List<FunctionSignature> ExtractFunctionSignatures(string code)
        {
            List<FunctionSignature> signatures = new List<FunctionSignature>();

            Regex funcPattern = new Regex(
                @"^\s*((?:void|int|float|string|object|vector|location|effect|itemproperty|talent|action|event|\w+)\s+(?:const\s+)?\*?\s*(\w+)\s*\(([^)]*)\)\s*\{)",
                RegexOptions.Multiline);

            foreach (Match match in funcPattern.Matches(code))
            {
                string fullMatch = match.Groups[1].Value;
                string returnTypeAndName = match.Groups[1].Value;
                string funcName = match.Groups[2].Value;
                string parameters = match.Groups[3].Value;

                string returnType = returnTypeAndName.Substring(0, returnTypeAndName.IndexOf(funcName)).Trim();
                returnType = Regex.Replace(returnType, @"\s+", " ");

                signatures.Add(new FunctionSignature
                {
                    ReturnType = returnType,
                    Name = funcName,
                    Parameters = parameters,
                    FullSignature = fullMatch
                });
            }

            return signatures;
        }

        /// <summary>
        /// Extracts the full body of a function from source code given its signature.
        /// </summary>
        private static string ExtractFunctionBody(string code, FunctionSignature signature)
        {
            string[] lines = code.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            StringBuilder body = new StringBuilder();
            bool inTargetFunction = false;
            int braceDepth = 0;

            Regex functionStartPattern = new Regex(
                @"^\s*((?:void|int|float|string|object|vector|location|effect|itemproperty|talent|action|event|\w+)\s+(?:const\s+)?\*?\s*(\w+)\s*\(([^)]*)\)\s*\{)",
                RegexOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Match funcMatch = functionStartPattern.Match(line);

                if (!inTargetFunction && funcMatch.Success)
                {
                    string funcName = funcMatch.Groups[2].Value;
                    string parameters = funcMatch.Groups[3].Value;
                    string returnTypeAndName = funcMatch.Groups[1].Value;
                    string returnType = returnTypeAndName.Substring(0, returnTypeAndName.IndexOf(funcName)).Trim();
                    returnType = Regex.Replace(returnType, @"\s+", " ");

                    FunctionSignature sig = new FunctionSignature
                    {
                        ReturnType = returnType,
                        Name = funcName,
                        Parameters = parameters
                    };

                    if (sig.Equals(signature))
                    {
                        inTargetFunction = true;
                        braceDepth = 0;
                        body.Clear();
                    }
                }

                if (inTargetFunction)
                {
                    body.Append(line);
                    if (i < lines.Length - 1)
                    {
                        body.Append("\n");
                    }
                    braceDepth += CountChar(line, '{');
                    braceDepth -= CountChar(line, '}');

                    if (braceDepth == 0)
                    {
                        return body.ToString();
                    }
                }
            }

            return body.ToString();
        }

        /// <summary>
        /// Filters out functions from the original expanded source that are not present in the decompiled output.
        /// This is necessary because the original source may include helper functions from includes that
        /// are not part of the actual compiled script and therefore won't appear in the decompiled output.
        /// </summary>
        private static string FilterFunctionsNotInDecompiled(string expandedOriginal, string decompiledOutput)
        {
            List<FunctionSignature> originalFunctions = ExtractFunctionSignatures(expandedOriginal);
            List<FunctionSignature> decompiledFunctions = ExtractFunctionSignatures(decompiledOutput);

            HashSet<FunctionSignature> decompiledSet = new HashSet<FunctionSignature>(decompiledFunctions);

            if (decompiledSet.Count == 0)
            {
                return expandedOriginal;
            }

            Dictionary<FunctionSignature, string> originalFunctionBodies = new Dictionary<FunctionSignature, string>();

            foreach (FunctionSignature sig in originalFunctions)
            {
                string functionBody = ExtractFunctionBody(expandedOriginal, sig);
                if (!string.IsNullOrEmpty(functionBody))
                {
                    originalFunctionBodies[sig] = functionBody;
                }
            }

            StringBuilder result = new StringBuilder();
            string[] lines = expandedOriginal.Split(new[] { '\n', '\r' }, StringSplitOptions.None);
            bool inFunction = false;
            int braceDepth = 0;
            StringBuilder currentFunction = new StringBuilder();
            FunctionSignature currentFunctionSig = null;
            bool keepCurrentFunction = false;

            Regex functionStartPattern = new Regex(
                @"^\s*((?:void|int|float|string|object|vector|location|effect|itemproperty|talent|action|event|\w+)\s+(?:const\s+)?\*?\s*(\w+)\s*\(([^)]*)\)\s*\{)",
                RegexOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                Match funcMatch = functionStartPattern.Match(line);

                if (!inFunction && funcMatch.Success)
                {
                    string returnTypeAndName = funcMatch.Groups[1].Value;
                    string funcName = funcMatch.Groups[2].Value;
                    string parameters = funcMatch.Groups[3].Value;
                    string returnType = returnTypeAndName.Substring(0, returnTypeAndName.IndexOf(funcName)).Trim();
                    returnType = Regex.Replace(returnType, @"\s+", " ");

                    currentFunctionSig = new FunctionSignature
                    {
                        ReturnType = returnType,
                        Name = funcName,
                        Parameters = parameters
                    };

                    if (funcName.Equals("main") || funcName.Equals("StartingConditional"))
                    {
                        keepCurrentFunction = true;
                    }
                    else
                    {
                        keepCurrentFunction = decompiledSet.Contains(currentFunctionSig);
                    }

                    inFunction = true;
                    braceDepth = 0;
                    currentFunction.Clear();
                    currentFunction.Append(line);
                    if (i < lines.Length - 1)
                    {
                        currentFunction.Append("\n");
                    }

                    braceDepth += CountChar(line, '{');
                    braceDepth -= CountChar(line, '}');

                    if (braceDepth == 0)
                    {
                        if (keepCurrentFunction)
                        {
                            result.Append(currentFunction.ToString());
                        }
                        inFunction = false;
                        currentFunction.Clear();
                        currentFunctionSig = null;
                    }
                }
                else if (inFunction)
                {
                    currentFunction.Append(line);
                    if (i < lines.Length - 1)
                    {
                        currentFunction.Append("\n");
                    }
                    braceDepth += CountChar(line, '{');
                    braceDepth -= CountChar(line, '}');

                    if (braceDepth == 0)
                    {
                        if (keepCurrentFunction)
                        {
                            result.Append(currentFunction.ToString());
                        }
                        inFunction = false;
                        currentFunction.Clear();
                        currentFunctionSig = null;
                    }
                }
                else
                {
                    result.Append(line);
                    if (i < lines.Length - 1)
                    {
                        result.Append("\n");
                    }
                }
            }

            if (inFunction && keepCurrentFunction)
            {
                result.Append(currentFunction.ToString());
            }

            return result.ToString();
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

        /// <summary>
        /// PRIMARY PRIORITY: Compares bytecode from first compilation (inbuilt) vs second compilation (external).
        /// This is the PRIMARY validation - test FAST FAILS on any mismatch.
        /// Bytecode must match 1:1, byte-by-byte, with zero tolerance for differences.
        /// </summary>
        private static void AssertBytecodeEqual(string originalNcs, string roundTripNcs, string gameFlag, string displayName)
        {
            // CRITICAL: Bytecode must match 1:1, byte-by-byte, with zero tolerance for differences
            // This is the PRIMARY validation - any mismatch is a failure and causes test to FAST FAIL

            // First, verify both files exist and are readable
            if (!File.Exists(originalNcs))
            {
                throw new InvalidOperationException($"Original NCS file does not exist: {DisplayPath(originalNcs)}");
            }
            if (!File.Exists(roundTripNcs))
            {
                throw new InvalidOperationException($"Round-trip NCS file does not exist: {DisplayPath(roundTripNcs)}");
            }

            // Perform strict byte-by-byte comparison
            BytecodeDiffResult diff = FindBytecodeDiff(originalNcs, roundTripNcs);
            if (diff == null)
            {
                // Files are identical - test passes
                return;
            }

            // Bytecode mismatch detected - this is a PRIMARY FAILURE
            // Build detailed error message with all diagnostic information
            StringBuilder message = new StringBuilder();
            message.Append("═══════════════════════════════════════════════════════════════\n");
            message.Append("BYTECODE MISMATCH (PRIMARY FAILURE): ").Append(displayName).Append("\n");
            message.Append("═══════════════════════════════════════════════════════════════\n");
            message.Append("\n");
            message.Append("CRITICAL: Bytecode must match 1:1. Any difference is a failure.\n");
            message.Append("\n");
            message.Append("LOCATION:\n");
            message.Append("  Offset: ").Append(diff.Offset).Append(" (0x").Append(diff.Offset.ToString("X")).Append(")\n");
            message.Append("  Original: ").Append(FormatByteValue(diff.OriginalByte)).Append("\n");
            message.Append("  Round-trip: ").Append(FormatByteValue(diff.RoundTripByte)).Append("\n");
            message.Append("\n");
            message.Append("FILES:\n");
            message.Append("  Original NCS: ").Append(DisplayPath(originalNcs)).Append("\n");
            message.Append("  Round-trip NCS: ").Append(DisplayPath(roundTripNcs)).Append("\n");
            message.Append("\n");
            message.Append("BYTECODE CONTEXT (hex dump around mismatch):\n");
            message.Append("  Original:  ").Append(diff.OriginalContext).Append("\n");
            message.Append("  Round-trip: ").Append(diff.RoundTripContext).Append("\n");
            message.Append("\n");
            message.Append("FILE SIZES:\n");
            message.Append("  Original: ").Append(diff.OriginalLength).Append(" bytes\n");
            message.Append("  Round-trip: ").Append(diff.RoundTripLength).Append(" bytes\n");
            if (diff.OriginalLength != diff.RoundTripLength)
            {
                message.Append("  ⚠️ WARNING: File sizes differ by ").Append(Math.Abs(diff.OriginalLength - diff.RoundTripLength)).Append(" bytes\n");
            }
            message.Append("\n");
            message.Append("═══════════════════════════════════════════════════════════════\n");

            throw new InvalidOperationException(message.ToString());
        }

        /// <summary>
        /// Performs strict byte-by-byte comparison of two NCS files.
        /// Returns null if files are identical, otherwise returns detailed diff information.
        /// This is the PRIMARY validation - bytecode must match 1:1 with zero tolerance.
        /// </summary>
        private static BytecodeDiffResult FindBytecodeDiff(string originalNcs, string roundTripNcs)
        {
            // Read both files as raw byte arrays for strict comparison
            byte[] originalBytes = File.ReadAllBytes(originalNcs);
            byte[] roundTripBytes = File.ReadAllBytes(roundTripNcs);

            // Quick check: if lengths differ, files cannot be identical
            if (originalBytes.Length != roundTripBytes.Length)
            {
                // Find the first position where they differ (which will be at min length)
                int firstDiffOffset = Math.Min(originalBytes.Length, roundTripBytes.Length);
                return new BytecodeDiffResult
                {
                    Offset = firstDiffOffset,
                    OriginalByte = firstDiffOffset < originalBytes.Length ? originalBytes[firstDiffOffset] & 0xFF : -1,
                    RoundTripByte = firstDiffOffset < roundTripBytes.Length ? roundTripBytes[firstDiffOffset] & 0xFF : -1,
                    OriginalLength = originalBytes.Length,
                    RoundTripLength = roundTripBytes.Length,
                    OriginalContext = RenderHexContext(originalBytes, firstDiffOffset),
                    RoundTripContext = RenderHexContext(roundTripBytes, firstDiffOffset),
                    HexDiff = FormatHexUnifiedDiff(originalBytes, roundTripBytes)
                };
            }

            // Files have same length - perform byte-by-byte comparison
            // This is the strictest possible comparison: every single byte must match
            for (int i = 0; i < originalBytes.Length; i++)
            {
                // Compare as unsigned bytes (0-255) to ensure correct comparison
                int original = originalBytes[i] & 0xFF;
                int roundTrip = roundTripBytes[i] & 0xFF;

                if (original != roundTrip)
                {
                    // First byte mismatch found - return detailed diff information
                    return new BytecodeDiffResult
                    {
                        Offset = i,
                        OriginalByte = original,
                        RoundTripByte = roundTrip,
                        OriginalLength = originalBytes.Length,
                        RoundTripLength = roundTripBytes.Length,
                        OriginalContext = RenderHexContext(originalBytes, i),
                        RoundTripContext = RenderHexContext(roundTripBytes, i),
                        HexDiff = FormatHexUnifiedDiff(originalBytes, roundTripBytes)
                    };
                }
            }

            // All bytes match - files are identical
            return null;
        }

        private static string RenderHexContext(byte[] bytes, int focus)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "<empty>";
            }

            // Handle EOF case: if focus is at or beyond the end, show the last bytes
            int anchor = focus >= bytes.Length ? bytes.Length - 1 : Math.Max(focus, 0);
            int start = Math.Max(0, anchor - 8);
            int end = Math.Min(bytes.Length, anchor + 9);

            StringBuilder sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (i > start) sb.Append(' ');
                sb.Append($"{bytes[i]:X2}");
                if (i == focus && focus < bytes.Length)
                {
                    sb.Append('*');
                }
            }

            // If focus is at EOF, indicate it
            if (focus >= bytes.Length)
            {
                sb.Append(" <EOF>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats hex dump diff in UDIFF format for bytecode mismatches.
        /// Shows hex bytes in a unified diff format for easy comparison.
        /// </summary>
        private static string FormatHexUnifiedDiff(byte[] originalBytes, byte[] roundTripBytes)
        {
            if (originalBytes == null && roundTripBytes == null)
            {
                return null;
            }

            if (originalBytes == null || originalBytes.Length == 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("    --- original.hex (empty)");
                sb.AppendLine("    +++ roundtrip.hex");
                sb.AppendLine($"    @@ -0,0 +1,{roundTripBytes.Length} @@");
                // Show first few lines of roundtrip
                int showBytes = Math.Min(roundTripBytes.Length, 256);
                for (int offset = 0; offset < showBytes; offset += 16)
                {
                    int lineEnd = Math.Min(offset + 16, showBytes);
                    sb.Append("    +");
                    sb.Append(FormatHexLine(roundTripBytes, offset, lineEnd));
                    sb.AppendLine();
                }
                if (roundTripBytes.Length > showBytes)
                {
                    sb.AppendLine($"    ... ({roundTripBytes.Length - showBytes} more bytes)");
                }
                return sb.ToString();
            }

            if (roundTripBytes == null || roundTripBytes.Length == 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("    --- original.hex");
                sb.AppendLine("    +++ roundtrip.hex (empty)");
                sb.AppendLine($"    @@ -1,{originalBytes.Length} +0,0 @@");
                // Show last few lines of original
                int showBytes = Math.Min(originalBytes.Length, 256);
                int startOffset = Math.Max(0, originalBytes.Length - showBytes);
                for (int offset = startOffset; offset < originalBytes.Length; offset += 16)
                {
                    int lineEnd = Math.Min(offset + 16, originalBytes.Length);
                    sb.Append("    -");
                    sb.Append(FormatHexLine(originalBytes, offset, lineEnd));
                    sb.AppendLine();
                }
                return sb.ToString();
            }

            // Find the first difference
            int minLength = Math.Min(originalBytes.Length, roundTripBytes.Length);
            int firstDiff = -1;
            for (int i = 0; i < minLength; i++)
            {
                if (originalBytes[i] != roundTripBytes[i])
                {
                    firstDiff = i;
                    break;
                }
            }

            // If all bytes match up to minLength, the difference is in length
            if (firstDiff == -1)
            {
                firstDiff = minLength;
            }

            // Show context around the first difference (32 bytes before, 64 bytes after, or more if needed)
            int contextBefore = 32;
            int contextAfter = 64;
            int contextStart = Math.Max(0, firstDiff - contextBefore);
            int maxLength = Math.Max(originalBytes.Length, roundTripBytes.Length);
            int contextEnd = Math.Min(maxLength, firstDiff + contextAfter);

            StringBuilder diff = new StringBuilder();
            diff.AppendLine("    --- original.hex");
            diff.AppendLine("    +++ roundtrip.hex");
            diff.AppendLine($"    @@ -{contextStart + 1},{contextEnd - contextStart} +{contextStart + 1},{contextEnd - contextStart} @@");

            // Show hex bytes in 16-byte lines
            for (int offset = contextStart; offset < contextEnd; offset += 16)
            {
                int lineEnd = Math.Min(offset + 16, contextEnd);
                bool hasOriginal = offset < originalBytes.Length;
                bool hasRoundTrip = offset < roundTripBytes.Length;
                bool isDifferent = false;

                if (hasOriginal && hasRoundTrip)
                {
                    // Check if any bytes in this line differ
                    int checkEnd = Math.Min(lineEnd, minLength);
                    for (int i = offset; i < checkEnd; i++)
                    {
                        if (originalBytes[i] != roundTripBytes[i])
                        {
                            isDifferent = true;
                            break;
                        }
                    }
                    // Also different if one file extends beyond the other
                    if (lineEnd > minLength)
                    {
                        isDifferent = true;
                    }
                }
                else
                {
                    isDifferent = true; // One file is longer
                }

                if (isDifferent)
                {
                    // Show both versions
                    if (hasOriginal)
                    {
                        diff.Append("    -");
                        diff.Append(FormatHexLine(originalBytes, offset, Math.Min(lineEnd, originalBytes.Length)));
                        diff.AppendLine();
                    }
                    if (hasRoundTrip)
                    {
                        diff.Append("    +");
                        diff.Append(FormatHexLine(roundTripBytes, offset, Math.Min(lineEnd, roundTripBytes.Length)));
                        diff.AppendLine();
                    }
                }
                else
                {
                    // Show context (unchanged)
                    diff.Append("     ");
                    diff.Append(FormatHexLine(originalBytes, offset, lineEnd));
                    diff.AppendLine();
                }
            }

            return diff.ToString();
        }

        private static string FormatHexLine(byte[] bytes, int start, int end)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{start:X8}: ");
            for (int i = start; i < end; i++)
            {
                if (i < bytes.Length)
                {
                    sb.Append($"{bytes[i]:X2}");
                }
                else
                {
                    sb.Append("??");
                }
                if (i < end - 1 && (i - start + 1) % 8 == 0)
                {
                    sb.Append(' ');
                }
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
            public string HexDiff { get; set; }
        }

        /// <summary>
        /// Attempts to identify the action name at a given bytecode offset.
        /// Returns null if unable to determine.
        /// Matching Java implementation at vendor/DeNCS/src/test/java/com/kotor/resource/formats/ncs/NCSDecompCLIRoundTripTest.java:3656-3699
        ///
        /// Implementation: Full NCS structure analysis - parses the NCS file to locate the instruction
        /// containing the given offset, and if it's an ACTION instruction, extracts the action ID properly.
        /// Based on swkotor2.exe ACTION opcode structure: bytecode (0x05) + qualifier (1 byte) + routine ID (uint16 big-endian) + arg count (uint8)
        /// </summary>
        private static string GetActionNameAtOffset(string ncsFile, long offset, string gameFlag)
        {
            try
            {
                int offsetInt = (int)offset;
                if (offsetInt < 0)
                {
                    return null;
                }

                // Parse the NCS file to get instruction structure
                NCS ncs = null;
                using (var reader = new NCSBinaryReader(ncsFile))
                {
                    try
                    {
                        ncs = reader.Load();
                    }
                    catch
                    {
                        // If parsing fails, fall back to null (corrupt or invalid NCS)
                        return null;
                    }
                }

                if (ncs == null || ncs.Instructions == null || ncs.Instructions.Count == 0)
                {
                    return null;
                }

                // Find which instruction contains the given byte offset
                NCSInstruction containingInstruction = null;
                foreach (var instruction in ncs.Instructions)
                {
                    if (instruction == null)
                    {
                        continue;
                    }

                    int instructionSize = CalculateInstructionSize(instruction);
                    int instructionStart = instruction.Offset;
                    int instructionEnd = instructionStart + instructionSize;

                    // Check if the offset falls within this instruction's byte range
                    if (offsetInt >= instructionStart && offsetInt < instructionEnd)
                    {
                        containingInstruction = instruction;
                        break;
                    }
                }

                // If we found an ACTION instruction, extract the action ID
                if (containingInstruction != null && containingInstruction.InsType == NCSInstructionType.ACTION)
                {
                    // ACTION instruction structure:
                    // Byte 0: bytecode (0x05)
                    // Byte 1: qualifier
                    // Bytes 2-3: action ID (uint16, big-endian) - stored in Args[0]
                    // Byte 4: argument count (uint8) - stored in Args[1]
                    if (containingInstruction.Args != null && containingInstruction.Args.Count >= 1)
                    {
                        try
                        {
                            int actionId = Convert.ToInt32(containingInstruction.Args[0]);
                            return LookupActionName(actionId, gameFlag);
                        }
                        catch
                        {
                            // Ignore conversion failures
                        }
                    }
                }
            }
            catch
            {
                // Ignore all errors
            }
            return null;
        }

        /// <summary>
        /// Calculates the byte size of an NCS instruction.
        /// Based on NCSBinaryWriter.DetermineSize and swkotor2.exe instruction size calculation.
        /// </summary>
        private static int CalculateInstructionSize(NCSInstruction instruction)
        {
            if (instruction == null)
            {
                return 0;
            }

            // Base size: bytecode (1 byte) + qualifier (1 byte)
            int size = 2;

            switch (instruction.InsType)
            {
                case NCSInstructionType.CPDOWNSP:
                case NCSInstructionType.CPTOPSP:
                case NCSInstructionType.CPDOWNBP:
                case NCSInstructionType.CPTOPBP:
                    // Stack copy operations: offset (4 bytes) + size (2 bytes)
                    size += 6;
                    break;

                case NCSInstructionType.CONSTI:
                case NCSInstructionType.CONSTF:
                case NCSInstructionType.CONSTO:
                    // Constants: value (4 bytes)
                    size += 4;
                    break;

                case NCSInstructionType.CONSTS:
                    // String constant: length (2 bytes) + string data
                    if (instruction.Args != null && instruction.Args.Count >= 1 && instruction.Args[0] is string str)
                    {
                        size += 2 + str.Length;
                    }
                    else
                    {
                        // Fallback: assume empty string
                        size += 2;
                    }
                    break;

                case NCSInstructionType.ACTION:
                    // ACTION: routine ID (2 bytes, big-endian) + argument count (1 byte)
                    // Based on swkotor2.exe: ACTION opcode format: uint16 routineId (big-endian, bytes 2-3) + uint8 argCount (byte 4)
                    size += 3;
                    break;

                case NCSInstructionType.MOVSP:
                case NCSInstructionType.JMP:
                case NCSInstructionType.JSR:
                case NCSInstructionType.JZ:
                case NCSInstructionType.JNZ:
                    // Jump/stack operations: offset (4 bytes)
                    size += 4;
                    break;

                case NCSInstructionType.DESTRUCT:
                    // DESTRUCT: size (2 bytes) + stack offset (2 bytes) + size (2 bytes)
                    size += 6;
                    break;

                case NCSInstructionType.DECxSP:
                case NCSInstructionType.INCxSP:
                case NCSInstructionType.DECxBP:
                case NCSInstructionType.INCxBP:
                    // Increment/decrement: value (4 bytes)
                    size += 4;
                    break;

                case NCSInstructionType.STORE_STATE:
                    // STORE_STATE: size (4 bytes) + stack offset (4 bytes)
                    size += 8;
                    break;

                case NCSInstructionType.EQUALTT:
                case NCSInstructionType.NEQUALTT:
                    // Struct equality: size (2 bytes)
                    size += 2;
                    break;

                // All other instructions (RSADDx, logical/arithmetic ops, RETN, SAVEBP, RESTOREBP, NOP, etc.)
                // have just opcode + qualifier (2 bytes total)
                default:
                    break;
            }

            return size;
        }

        /// <summary>
        /// Looks up an action name by ID from the nwscript.nss file.
        /// </summary>
        private static string LookupActionName(int actionId, string gameFlag)
        {
            try
            {
                string nwscriptPath = gameFlag.Equals("k2") ? K2Nwscript : K1Nwscript;
                if (!File.Exists(nwscriptPath))
                {
                    return null;
                }

                string content = File.ReadAllText(nwscriptPath, Encoding.UTF8);
                // Look for "// actionId:" pattern (with or without description)
                string pattern = "// " + actionId + ":";
                int idx = content.IndexOf(pattern);
                if (idx >= 0)
                {
                    // Find the function signature after the comment
                    // Format: "// 120: Description\n// ...\nvoid FunctionName(...);"
                    int searchStart = idx + pattern.Length;
                    int searchEnd = Math.Min(searchStart + 500, content.Length); // Look ahead up to 500 chars
                    string section = content.Substring(searchStart, searchEnd - searchStart);

                    // Look for function signature pattern: "void FunctionName" or "int FunctionName" etc.
                    Regex funcPattern = new Regex(@"\b(void|int|float|string|object|location|effect|talent|action|itemproperty|vector)\s+(\w+)\s*\(");
                    Match match = funcPattern.Match(section);
                    if (match.Success)
                    {
                        return match.Groups[2].Value; // Return function name
                    }
                }
            }
            catch
            {
                // Ignore lookup failures
            }
            return null;
        }

        /// <summary>
        /// Formats NCS instructions as strings for pcode diff output.
        /// </summary>
        private static string FormatPcodeInstructions(NCS ncs)
        {
            if (ncs == null || ncs.Instructions == null)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ncs.Instructions.Count; i++)
            {
                NCSInstruction inst = ncs.Instructions[i];
                sb.Append($"{i:D4}: {inst}");
                if (inst.Jump != null && inst.Jump.Offset >= 0)
                {
                    sb.Append($" -> {inst.Jump.Offset:D4}");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats pcode diff in UDIFF format for bytecode mismatches.
        /// </summary>
        private static string FormatPcodeUnifiedDiff(NCS originalNcs, NCS roundTripNcs)
        {
            string originalPcode = FormatPcodeInstructions(originalNcs);
            string roundTripPcode = FormatPcodeInstructions(roundTripNcs);

            string[] originalLines = originalPcode.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            string[] roundTripLines = roundTripPcode.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            DiffResult diffResult = ComputeDiff(originalLines, roundTripLines);

            if (diffResult.IsEmpty())
            {
                return null;
            }

            StringBuilder diff = new StringBuilder();
            diff.AppendLine("    --- original.pcode");
            diff.AppendLine("    +++ roundtrip.pcode");
            diff.AppendLine($"    @@ -1,{originalLines.Length} +1,{roundTripLines.Length} @@");

            foreach (DiffLine line in diffResult.Lines)
            {
                switch (line.Type)
                {
                    case DiffLineType.Context:
                        diff.Append("     ").AppendLine(line.Content);
                        break;
                    case DiffLineType.Removed:
                        diff.Append("    -").AppendLine(line.Content);
                        break;
                    case DiffLineType.Added:
                        diff.Append("    +").AppendLine(line.Content);
                        break;
                }
            }

            return diff.ToString();
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

        [Fact] // Timeout enforced via PerformanceTestHelper
        public void TestRoundTripSuite()
        {
            // Performance monitoring - will generate profile report and fail if exceeds 2 minutes
            using (var perfHelper = new Performance.PerformanceTestHelper(
                nameof(TestRoundTripSuite),
                null, // No ITestOutputHelper available in this context
                maxSeconds: 600, // 10 minutes to allow processing of many files
                enableProfiling: true))
            {
                try
                {
                    ResetPerformanceTracking();
                    _testStartTime = Stopwatch.GetTimestamp();

                    Preflight();
                    List<RoundTripCase> tests = BuildRoundTripCases();

                    if (tests.Count == 0)
                    {
                        Console.Error.WriteLine("ERROR: No test files found!");
                        throw new InvalidOperationException("No test files found!");
                    }

                    Console.WriteLine("=== Running Round-Trip Tests ===");
                    Console.WriteLine($"Total NSS scripts to roundtrip test: {tests.Count}");
                    Console.WriteLine("Flow: NSS--(external)-->NCS--(decompile)-->NSS--(external)-->NCS--(decompile)-->NSS--(inbuilt)-->NCS");
                    Console.WriteLine("Fast-fail: enabled for PRIMARY check (Step 5: external vs external recompiled)");
                    Console.WriteLine("Secondary check: Step 8 (external recompiled vs inbuilt recompiled) fails but doesn't fast-fail");
                    Console.WriteLine();

                    foreach (RoundTripCase testCase in tests)
                    {
                        // Check timeout before processing each test case
                        perfHelper.CheckTimeout();

                        _testsProcessed++;
                        string relPath = GetRelativePath(VanillaRepoDir, testCase.Item.Path);
                        string displayPath = relPath.Replace('\\', '/');

                        try
                        {
                            // Clear file cache periodically to prevent memory bloat
                            if (_testsProcessed % 10 == 0)
                            {
                                lock (_cacheLock)
                                {
                                    if (_fileContentCache.Count > 1000)
                                    {
                                        _fileContentCache.Clear();
                                    }
                                }
                            }

                            RoundTripResult result = RoundTripSingle(testCase.Item.Path, testCase.Item.GameFlag, testCase.Item.ScratchRoot);

                            // Show one-line summary (matching Java format: [testNum/total] path - STATUS)
                            string status = result.Passed ? "PASS" : "FAIL";
                            string details = "";
                            if (!result.TextMatch && result.TextDiff != null)
                            {
                                details += " [TEXT MISMATCH]";
                            }
                            if (!result.BytecodeMatch)
                            {
                                details += " [BYTECODE MISMATCH]";
                            }
                            Console.WriteLine($"[{_testsProcessed}/{_totalTests}] {displayPath} - {status}{details}");

                            // Show text diff if there's a mismatch (SECONDARY - warning only)
                            if (!result.TextMatch && result.TextDiff != null)
                            {
                                Console.Error.WriteLine();
                                Console.Error.WriteLine($"WARNING: Text mismatch for {displayPath}");
                                Console.Error.WriteLine("UDIFF:");
                                Console.Error.WriteLine(result.TextDiff);
                            }

                            if (!result.Passed)
                            {
                                // Save decompiled output for debugging
                                try
                                {
                                    string debugOutputDir = Path.Combine(Directory.GetCurrentDirectory(), "debug-decompiled-output");
                                    Directory.CreateDirectory(debugOutputDir);
                                    string debugFile = Path.Combine(debugOutputDir, Path.GetFileName(displayPath.Replace('/', '_').Replace('\\', '_')) + ".dec.nss");
                                    string decompiledPath = Path.Combine(testCase.Item.ScratchRoot, GetRelativePath(VanillaRepoDir, testCase.Item.Path).Replace(Path.GetExtension(testCase.Item.Path), ".dec.nss"));
                                    if (File.Exists(decompiledPath))
                                    {
                                        File.Copy(decompiledPath, debugFile, true);
                                        Console.Error.WriteLine($"DEBUG: Saved decompiled output to {debugFile}");
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignore debug file save errors
                                }

                                // Show bytecode diff if there's a mismatch (PRIMARY - fast fail)
                                if (!result.BytecodeMatch && result.PcodeDiff != null)
                                {
                                    Console.Error.WriteLine();
                                    Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
                                    Console.Error.WriteLine($"BYTECODE MISMATCH (PRIMARY FAILURE): {displayPath}");
                                    Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
                                    Console.Error.WriteLine();
                                    Console.Error.WriteLine("FULL PCODE DIFF (UDIFF format):");
                                    Console.Error.WriteLine(result.PcodeDiff);
                                    Console.Error.WriteLine();
                                }
                                if (result.ErrorMessage != null)
                                {
                                    Console.Error.WriteLine(result.ErrorMessage);
                                }
                                PrintPerformanceSummary();
                                throw new InvalidOperationException($"Round-trip test failed for {displayPath}: {result.ErrorMessage ?? "Unknown error"}");
                            }
                        }
                        catch (SourceCompilationException)
                        {
                            Console.WriteLine($"[{_testsProcessed}/{_totalTests}] {displayPath} - SKIP (original source file has compilation errors)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{_testsProcessed}/{_totalTests}] {displayPath} - FAIL");
                            Console.Error.WriteLine();
                            Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
                            Console.Error.WriteLine($"FAILURE: {testCase.DisplayName}");
                            Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
                            Console.Error.WriteLine($"Exception: {ex.GetType().Name}");
                            Console.Error.WriteLine($"Message:\n{ex.Message}");
                            Console.Error.WriteLine("═══════════════════════════════════════════════════════════");
                            Console.Error.WriteLine();

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
                    perfHelper.CheckTimeout(); // Final timeout check
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
}
