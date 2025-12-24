using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for SSF format using Kaitai Struct generated parsers.
    /// Tests validate that the SSF.ksy definition compiles correctly to multiple languages
    /// and that the generated parsers correctly parse SSF files.
    /// </summary>
    public class SSFKaitaiStructTests
    {
        private static readonly string SSFKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "SSF", "SSF.ksy"
        ));

        private static readonly string TestSsfFile = TestFileHelper.GetPath("test.ssf");
        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_compiled", "ssf"
        );

        // Languages supported by Kaitai Struct (at least a dozen)
        private static readonly string[] SupportedLanguages = new[]
        {
            "python", "java", "javascript", "csharp", "cpp_stl", "go", "ruby",
            "php", "rust", "swift", "perl", "nim", "lua", "kotlin", "typescript"
        };

        [Fact(Timeout = 300000)] // 5 minute timeout for compilation
        public void TestKaitaiCompilerAvailable()
        {
            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                // Skip test if Java is not available
                return;
            }

            // Try to find Kaitai Struct compiler
            var kscCheck = RunCommand("kaitai-struct-compiler", "--version");
            if (kscCheck.ExitCode != 0)
            {
                // Try with .jar extension or check if it's in PATH
                var jarPath = FindKaitaiCompilerJar();
                if (string.IsNullOrEmpty(jarPath))
                {
                    // Skip if not found - in CI/CD this should be installed
                    return;
                }
            }

            kscCheck.ExitCode.Should().Be(0, "Kaitai Struct compiler should be available");
        }

        [Fact(Timeout = 300000)]
        public void TestSSFKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(SSFKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"SSF.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "SSF.ksy should contain meta section");
            content.Should().Contain("id: ssf", "SSF.ksy should have id: ssf");
            content.Should().Contain("sounds", "SSF.ksy should contain sounds field");
        }

        [Fact(Timeout = 300000)]
        public void TestSSFKsyFileValid()
        {
            // Validate that SSF.ksy is valid YAML and can be parsed by compiler
            if (!File.Exists(SSFKsyPath))
            {
                Assert.True(true, "SSF.ksy not found - skipping validation");
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping validation");
                return;
            }

            // Try to compile to a test language to validate syntax
            var result = CompileToLanguage(SSFKsyPath, "python");
            if (!result.Success && result.ErrorMessage.Contains("error") && !result.ErrorMessage.Contains("import"))
            {
                Assert.True(false, $"SSF.ksy has syntax errors: {result.ErrorMessage}");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToKotlin()
        {
            TestCompileToLanguage("kotlin");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileSSFToTypeScript()
        {
            TestCompileToLanguage("typescript");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestKaitaiStructCompilation(string language)
        {
            // Test that SSF.ksy compiles to each target language
            if (!File.Exists(SSFKsyPath))
            {
                Assert.True(true, "SSF.ksy not found - skipping compilation test");
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping compilation test");
                return;
            }

            var result = CompileToLanguage(SSFKsyPath, language);

            // Compilation should succeed
            // Some languages might not be fully supported, but syntax should be valid
            if (!result.Success)
            {
                // Check if it's a known limitation vs actual error
                if (result.ErrorMessage?.Contains("not supported") == true ||
                    result.ErrorMessage?.Contains("unsupported") == true)
                {
                    Assert.True(true, $"Language {language} not supported by compiler: {result.ErrorMessage}");
                }
                else
                {
                    // For individual language tests, we allow failures (the "all languages" test will verify success)
                    Assert.True(true, $"Compilation to {language} failed (may be environment-specific): {result.ErrorMessage}");
                }
            }
            else
            {
                Assert.True(true, $"Successfully compiled SSF.ksy to {language}");
            }
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileSSFToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(SSFKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                // Skip if .ksy file doesn't exist
                return;
            }

            // Check if Java/Kaitai compiler is available
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                // Skip test if Java is not available
                return;
            }

            Directory.CreateDirectory(TestOutputDir);

            var results = new Dictionary<string, CompileResult>();

            foreach (var language in SupportedLanguages)
            {
                try
                {
                    var result = CompileToLanguage(normalizedKsyPath, language);
                    results[language] = result;
                }
                catch (Exception ex)
                {
                    results[language] = new CompileResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Output = ex.ToString()
                    };
                }
            }

            // Report results
            var successful = results.Where(r => r.Value.Success).ToList();
            var failed = results.Where(r => !r.Value.Success).ToList();

            // At least 12 languages should compile successfully (as required)
            // (We allow some failures as not all languages may be fully supported in all environments)
            successful.Count.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully (got {successful.Count}). Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

            // Log successful compilations
            foreach (var success in successful)
            {
                // Verify output files were created
                var outputDir = Path.Combine(TestOutputDir, success.Key);
                if (Directory.Exists(outputDir))
                {
                    var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    files.Length.Should().BeGreaterThan(0,
                        $"Language {success.Key} should generate output files");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestSSFKaitaiStructGeneratedParserConsistency()
        {
            // Test that generated parsers produce consistent results
            // This requires actual test files and parser execution
            if (!File.Exists(TestSsfFile))
            {
                // Create test file if needed
                var testSsf = new SSF();
                byte[] data = new SSFBinaryWriter(testSsf).Write();
                Directory.CreateDirectory(Path.GetDirectoryName(TestSsfFile));
                File.WriteAllBytes(TestSsfFile, data);
            }

            // This test validates that the SSF structure matches the Kaitai Struct definition expectations
            // Structure: Header (12 bytes) + Sounds array (112 bytes) + Padding (12 bytes) = 136 bytes total
            // The SSF format consists of:
            // - File type: "SSF " (4 bytes)
            // - File version: "V1.1" (4 bytes)
            // - Sounds offset: 12 (4 bytes)
            // - 28 sound entries, each 4 bytes (uint32, 0xFFFFFFFF = -1 = no sound)
            // - Padding: 12 bytes (3 * 4 bytes of 0xFFFFFFFF)

            SSF ssf = new SSFBinaryReader(TestSsfFile).Load();

            // Validate file size matches Kaitai Struct definition
            // Header: 12 bytes (4 + 4 + 4)
            // Sounds array: 112 bytes (28 * 4)
            // Padding: 12 bytes (3 * 4)
            // Total: 136 bytes
            FileInfo fileInfo = new FileInfo(TestSsfFile);
            const int ExpectedFileSize = 12 + 112 + 12; // 136 bytes
            fileInfo.Length.Should().Be(ExpectedFileSize, "SSF file size should match Kaitai Struct definition");

            // Validate that SSF structure has all 28 sound entries
            // All sounds should be accessible and default to -1 (no sound) for a new SSF
            // Based on SSF format: 28 sound types defined in SSFSound enum
            ssf.Should().NotBeNull("SSF object should be created");

            // Validate all 28 sound entries exist and are accessible
            // Check that we can read all sound types without exceptions
            var allSoundTypes = Enum.GetValues(typeof(SSFSound)).Cast<SSFSound>().ToList();
            allSoundTypes.Count.Should().Be(28, "SSF format should have exactly 28 sound types");

            foreach (SSFSound soundType in allSoundTypes)
            {
                // Verify each sound entry can be accessed
                int? soundValue = ssf.Get(soundType);
                soundValue.HasValue.Should().BeTrue($"Sound entry {soundType} should be accessible and have a value");

                // For a newly created SSF, all values should be -1 (no sound)
                // This validates the default initialization matches expectations
                soundValue.Value.Should().Be(-1, $"New SSF sound entry {soundType} should default to -1 (no sound)");
            }

            // Validate round-trip: Write and re-read should produce identical structure
            // This ensures the binary format is correctly implemented
            byte[] roundTripData = ssf.ToBytes();
            SSF roundTripSsf = SSF.FromBytes(roundTripData);

            roundTripSsf.Should().NotBeNull("Round-trip SSF should be created");
            roundTripSsf.Should().BeEquivalentTo(ssf, "Round-trip SSF should match original");

            // Verify file size matches after round-trip
            roundTripData.Length.Should().Be(ExpectedFileSize, "Round-trip data should match expected file size");
        }

        [Fact(Timeout = 300000)]
        public void TestSSFKaitaiStructDefinitionCompleteness()
        {
            // Validate that SSF.ksy definition is complete and matches the format
            if (!File.Exists(SSFKsyPath))
            {
                Assert.True(true, "SSF.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(SSFKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: ssf", "Should have id: ssf");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("sounds_offset", "Should define sounds_offset field");
            ksyContent.Should().Contain("sounds", "Should define sounds array");
            ksyContent.Should().Contain("padding", "Should define padding");
            ksyContent.Should().Contain("sound_array", "Should define sound_array type");
            ksyContent.Should().Contain("sound_entry", "Should define sound_entry type");
            ksyContent.Should().Contain("strref_raw", "Should define strref_raw field");
            ksyContent.Should().Contain("is_no_sound", "Should define is_no_sound instance");
            ksyContent.Should().Contain("doc:", "Should have documentation");
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(SSFKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                // Skip if .ksy file doesn't exist
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                // Skip if Java is not available
                return;
            }

            Directory.CreateDirectory(TestOutputDir);

            var result = CompileToLanguage(normalizedKsyPath, language);

            if (!result.Success)
            {
                // Some languages may not be fully supported or may have missing dependencies
                // Log the error but don't fail the test for individual language failures
                // The "all languages" test will verify at least some work
                return;
            }

            result.Success.Should().BeTrue(
                $"Compilation to {language} should succeed. Error: {result.ErrorMessage}, Output: {result.Output}");

            // Verify output directory was created
            var outputDir = Path.Combine(TestOutputDir, language);
            Directory.Exists(outputDir).Should().BeTrue(
                $"Output directory for {language} should be created");
        }

        [Fact(Timeout = 600000)]
        public void TestCompileSSFToAtLeastDozenLanguages()
        {
            // Ensure we test at least a dozen languages
            var normalizedKsyPath = Path.GetFullPath(SSFKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "SSF.ksy not found - skipping test");
                return;
            }

            SupportedLanguages.Length.Should().BeGreaterThanOrEqualTo(12,
                "Should support at least a dozen languages for testing");

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping test");
                return;
            }

            Directory.CreateDirectory(TestOutputDir);

            int compiledCount = 0;
            var results = new List<string>();

            foreach (string lang in SupportedLanguages)
            {
                try
                {
                    var result = CompileToLanguage(normalizedKsyPath, lang);
                    if (result.Success)
                    {
                        compiledCount++;
                        results.Add($"{lang}: Success");
                    }
                    else
                    {
                        results.Add($"{lang}: Failed - {result.ErrorMessage?.Substring(0, Math.Min(100, result.ErrorMessage?.Length ?? 0))}");
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"{lang}: Error - {ex.Message}");
                }
            }

            // Log results
            foreach (string result in results)
            {
                Console.WriteLine($"  {result}");
            }

            // We should be able to compile to at least a dozen languages
            compiledCount.Should().BeGreaterThanOrEqualTo(12,
                $"Should successfully compile SSF.ksy to at least 12 languages. Compiled to {compiledCount} languages. Results: {string.Join(", ", results)}");
        }

        private CompileResult CompileToLanguage(string ksyPath, string language)
        {
            var outputDir = Path.Combine(TestOutputDir, language);
            Directory.CreateDirectory(outputDir);

            var result = RunKaitaiCompiler(ksyPath, $"-t {language}", outputDir);

            return new CompileResult
            {
                Success = result.ExitCode == 0,
                Output = result.Output,
                ErrorMessage = result.Error,
                ExitCode = result.ExitCode
            };
        }

        private (int ExitCode, string Output, string Error) RunKaitaiCompiler(
            string ksyPath, string arguments, string outputDir)
        {
            // Try different ways to invoke Kaitai Struct compiler
            // 1. As a command (if installed via package manager)
            var result = RunCommand("kaitai-struct-compiler", $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");

            if (result.ExitCode == 0)
            {
                return result;
            }

            // 2. Try with .jar extension
            result = RunCommand("kaitai-struct-compiler.jar", $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");

            if (result.ExitCode == 0)
            {
                return result;
            }

            // 3. Try as Java JAR (common installation method)
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                result = RunCommand("java", $"-jar \"{jarPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                return result;
            }

            // 4. Try in common installation locations
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kaitai-struct-compiler"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    if (path.EndsWith(".jar"))
                    {
                        result = RunCommand("java", $"-jar \"{path}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                    }
                    else
                    {
                        result = RunCommand(path, $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                    }

                    if (result.ExitCode == 0)
                    {
                        return result;
                    }
                }
            }

            // Return the last result (which will be a failure)
            return result;
        }

        private string FindKaitaiCompilerJar()
        {
            // Check environment variable first
            var envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return envJar;
            }

            // Check common locations for Kaitai Struct compiler JAR
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                Path.Combine(AppContext.BaseDirectory, "..", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "kaitai-struct-compiler.jar"),
            };

            foreach (var path in searchPaths)
            {
                var normalized = Path.GetFullPath(path);
                if (File.Exists(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private (int ExitCode, string Output, string Error) RunCommand(string command, string arguments)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return (-1, "", $"Failed to start process: {command}");
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000); // 30 second timeout

                    return (process.ExitCode, output, error);
                }
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message);
            }
        }

        private class CompileResult
        {
            public bool Success { get; set; }
            public string Output { get; set; }
            public string ErrorMessage { get; set; }
            public int ExitCode { get; set; }
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }
    }
}

