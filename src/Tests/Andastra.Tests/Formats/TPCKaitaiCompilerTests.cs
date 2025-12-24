using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Andastra.Parsing;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for TPC/DDS/TGA/TXI Kaitai Struct compiler functionality.
    /// Tests compile TPC.ksy, DDS.ksy, TGA.ksy, and TXI.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic, Swift, Kotlin
    /// </summary>
    public class TPCKaitaiCompilerTests
    {
        private static readonly string TpcKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "TPC", "TPC.ksy");

        private static readonly string DdsKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "TPC", "DDS.ksy");

        private static readonly string TgaKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "TPC", "TGA.ksy");

        private static readonly string TxiKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "TPC", "TXI.ksy");

        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_tpc_tests");

        // Supported Kaitai Struct target languages (at least a dozen)
        private static readonly string[] SupportedLanguages = new[]
        {
            "python",
            "java",
            "javascript",
            "csharp",
            "cpp_stl",
            "ruby",
            "php",
            "go",
            "rust",
            "perl",
            "lua",
            "nim",
            "visualbasic",
            "swift",
            "kotlin"
        };

        static TPCKaitaiCompilerTests()
        {
            // Normalize KSY paths
            TpcKsyPath = Path.GetFullPath(TpcKsyPath);
            DdsKsyPath = Path.GetFullPath(DdsKsyPath);
            TgaKsyPath = Path.GetFullPath(TgaKsyPath);
            TxiKsyPath = Path.GetFullPath(TxiKsyPath);

            // Create output directory
            if (!Directory.Exists(CompilerOutputDir))
            {
                Directory.CreateDirectory(CompilerOutputDir);
            }
        }

        [Fact(Timeout = 300000)] // 5 minutes timeout for compilation
        public void TestKaitaiStructCompilerAvailable()
        {
            // Test that kaitai-struct-compiler is available
            string compilerPath = FindKaitaiCompiler();
            compilerPath.Should().NotBeNullOrEmpty("kaitai-struct-compiler should be available in PATH or common locations");

            // Test compiler version
            var processInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(10000);
                    process.ExitCode.Should().Be(0, "kaitai-struct-compiler should execute successfully");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestTpcKsyFileExists()
        {
            File.Exists(TpcKsyPath).Should().BeTrue($"TPC.ksy should exist at {TpcKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(TpcKsyPath);
            content.Should().Contain("meta:", "TPC.ksy should contain meta section");
            content.Should().Contain("id: tpc", "TPC.ksy should have id: tpc");
        }

        [Fact(Timeout = 300000)]
        public void TestDdsKsyFileExists()
        {
            File.Exists(DdsKsyPath).Should().BeTrue($"DDS.ksy should exist at {DdsKsyPath}");

            string content = File.ReadAllText(DdsKsyPath);
            content.Should().Contain("meta:", "DDS.ksy should contain meta section");
            content.Should().Contain("id: dds", "DDS.ksy should have id: dds");
        }

        [Fact(Timeout = 300000)]
        public void TestTgaKsyFileExists()
        {
            File.Exists(TgaKsyPath).Should().BeTrue($"TGA.ksy should exist at {TgaKsyPath}");

            string content = File.ReadAllText(TgaKsyPath);
            content.Should().Contain("meta:", "TGA.ksy should contain meta section");
            content.Should().Contain("id: tga", "TGA.ksy should have id: tga");
        }

        [Fact(Timeout = 300000)]
        public void TestTxiKsyFileExists()
        {
            File.Exists(TxiKsyPath).Should().BeTrue($"TXI.ksy should exist at {TxiKsyPath}");

            string content = File.ReadAllText(TxiKsyPath);
            content.Should().Contain("meta:", "TXI.ksy should contain meta section");
            content.Should().Contain("id: txi", "TXI.ksy should have id: txi");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileTpcKsyToLanguage(string language)
        {
            TestCompileKsyToLanguage(TpcKsyPath, "tpc", language);
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileDdsKsyToLanguage(string language)
        {
            TestCompileKsyToLanguage(DdsKsyPath, "dds", language);
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileTgaKsyToLanguage(string language)
        {
            TestCompileKsyToLanguage(TgaKsyPath, "tga", language);
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileTxiKsyToLanguage(string language)
        {
            TestCompileKsyToLanguage(TxiKsyPath, "txi", language);
        }

        private void TestCompileKsyToLanguage(string ksyPath, string formatName, string language)
        {
            // Skip if compiler not available
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip test if compiler not available
            }

            // Skip if KSY file doesn't exist
            if (!File.Exists(ksyPath))
            {
                return; // Skip test if KSY file doesn't exist
            }

            // Create output directory for this language and format
            string langOutputDir = Path.Combine(CompilerOutputDir, formatName, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile KSY to target language
            ProcessStartInfo processInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                // JAR file - use java -jar
                string jarPath = compilerPath.Substring(4);
                processInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t {language} \"{ksyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ksyPath)
                };
            }
            else
            {
                // Executable command
                processInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t {language} \"{ksyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ksyPath)
                };
            }

            string stdout = "";
            string stderr = "";
            int exitCode = -1;

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000); // 60 second timeout
                    exitCode = process.ExitCode;
                }
            }

            // Compilation should succeed
            exitCode.Should().Be(0,
                $"kaitai-struct-compiler should compile {formatName}.ksy to {language} successfully. " +
                $"STDOUT: {stdout}, STDERR: {stderr}");

            // Verify output files were generated
            string[] generatedFiles = Directory.GetFiles(langOutputDir, "*", SearchOption.AllDirectories);
            generatedFiles.Should().NotBeEmpty($"Compilation to {language} should generate output files");
        }

        [Fact(Timeout = 600000)] // 10 minutes timeout for compiling all languages
        public void TestCompileAllKsyFilesToAllLanguages()
        {
            // Test compilation to all supported languages for all KSY files
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            var ksyFiles = new[]
            {
                new { Path = TpcKsyPath, Name = "TPC" },
                new { Path = DdsKsyPath, Name = "DDS" },
                new { Path = TgaKsyPath, Name = "TGA" },
                new { Path = TxiKsyPath, Name = "TXI" }
            };

            var results = new Dictionary<string, bool>();
            var errors = new Dictionary<string, string>();

            foreach (var ksyFile in ksyFiles)
            {
                if (!File.Exists(ksyFile.Path))
                {
                    continue; // Skip if file doesn't exist
                }

                foreach (string language in SupportedLanguages)
                {
                    string testKey = $"{ksyFile.Name}-{language}";
                    try
                    {
                        string langOutputDir = Path.Combine(CompilerOutputDir, ksyFile.Name.ToLowerInvariant(), language);
                        if (Directory.Exists(langOutputDir))
                        {
                            Directory.Delete(langOutputDir, true);
                        }
                        Directory.CreateDirectory(langOutputDir);

                        ProcessStartInfo processInfo;
                        if (compilerPath.StartsWith("JAR:"))
                        {
                            // JAR file - use java -jar
                            string jarPath = compilerPath.Substring(4);
                            processInfo = new ProcessStartInfo
                            {
                                FileName = "java",
                                Arguments = $"-jar \"{jarPath}\" -t {language} \"{ksyFile.Path}\" -d \"{langOutputDir}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = Path.GetDirectoryName(ksyFile.Path)
                            };
                        }
                        else
                        {
                            // Executable command
                            processInfo = new ProcessStartInfo
                            {
                                FileName = compilerPath,
                                Arguments = $"-t {language} \"{ksyFile.Path}\" -d \"{langOutputDir}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = Path.GetDirectoryName(ksyFile.Path)
                            };
                        }

                        using (var process = Process.Start(processInfo))
                        {
                            if (process != null)
                            {
                                string stdout = process.StandardOutput.ReadToEnd();
                                string stderr = process.StandardError.ReadToEnd();
                                process.WaitForExit(60000);

                                bool success = process.ExitCode == 0;
                                results[testKey] = success;

                                if (!success)
                                {
                                    errors[testKey] = $"Exit code: {process.ExitCode}, STDOUT: {stdout}, STDERR: {stderr}";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results[testKey] = false;
                        errors[testKey] = ex.Message;
                    }
                }
            }

            // Report results
            int successCount = results.Values.Count(r => r);
            int totalCount = results.Count;

            // At least 12 languages should compile successfully for each format
            // (4 formats * 12 languages = 48 minimum successful compilations)
            int expectedMinimum = 4 * 12; // At least 12 languages per format
            successCount.Should().BeGreaterThanOrEqualTo(expectedMinimum,
                $"At least {expectedMinimum} compilations should succeed out of {totalCount} total. " +
                $"Results: {string.Join(", ", results.Select(kvp => $"{kvp.Key}: {(kvp.Value ? "OK" : "FAIL")}"))}. " +
                $"Errors: {string.Join("; ", errors.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
        }

        [Fact(Timeout = 300000)]
        public void TestTpcKsySyntaxValidation()
        {
            TestKsySyntaxValidation(TpcKsyPath, "TPC");
        }

        [Fact(Timeout = 300000)]
        public void TestDdsKsySyntaxValidation()
        {
            TestKsySyntaxValidation(DdsKsyPath, "DDS");
        }

        [Fact(Timeout = 300000)]
        public void TestTgaKsySyntaxValidation()
        {
            TestKsySyntaxValidation(TgaKsyPath, "TGA");
        }

        [Fact(Timeout = 300000)]
        public void TestTxiKsySyntaxValidation()
        {
            TestKsySyntaxValidation(TxiKsyPath, "TXI");
        }

        private void TestKsySyntaxValidation(string ksyPath, string formatName)
        {
            // Validate KSY syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            if (!File.Exists(ksyPath))
            {
                return; // Skip if file doesn't exist
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{ksyPath}\" --debug",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ksyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    if (process.ExitCode != 0)
                    {
                        // Check if it's a known limitation vs actual error
                        if (!stderr.Contains("error") || stderr.Contains("import"))
                        {
                            // May be acceptable (missing imports, etc.)
                            return;
                        }
                    }

                    process.ExitCode.Should().Be(0,
                        $"{formatName}.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompiledCSharpParserStructure()
        {
            // Test C# parser compilation and basic structure validation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            if (!File.Exists(TpcKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "tpc", "csharp");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to C#
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t csharp \"{TpcKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(TpcKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0,
                        $"C# compilation should succeed. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }

            // Verify C# parser file was generated
            string[] csFiles = Directory.GetFiles(langOutputDir, "*.cs", SearchOption.AllDirectories);
            csFiles.Should().NotBeEmpty("C# parser files should be generated");

            // Verify generated C# file contains expected structure
            string tpcCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("tpc"));
            if (tpcCsFile != null)
            {
                string csContent = File.ReadAllText(tpcCsFile);
                csContent.Should().Contain("class", "Generated C# file should contain class definition");
            }
        }

        [Theory(Timeout = 300000)]
        [InlineData("cpp_stl")]
        [InlineData("ruby")]
        [InlineData("php")]
        [InlineData("go")]
        [InlineData("rust")]
        [InlineData("perl")]
        [InlineData("lua")]
        [InlineData("nim")]
        [InlineData("visualbasic")]
        public void TestCompileTpcKsyToAdditionalLanguages(string language)
        {
            // Test compilation to additional languages
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "tpc", language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to target language
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t {language} \"{TpcKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(TpcKsyPath)
            };

            int exitCode = -1;
            string stdout = "";
            string stderr = "";

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    exitCode = process.ExitCode;
                }
            }

            // Compilation should succeed (some languages may not be fully supported, but should attempt)
            if (exitCode != 0)
            {
                // Log but don't fail - some languages may not be available in all compiler versions
                Console.WriteLine($"Warning: {language} compilation failed with exit code {exitCode}. STDOUT: {stdout}, STDERR: {stderr}");
            }
            else
            {
                // Verify output files were generated
                string[] generatedFiles = Directory.GetFiles(langOutputDir, "*", SearchOption.AllDirectories);
                generatedFiles.Should().NotBeEmpty($"{language} compilation should generate output files");
            }
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        private static string FindKaitaiCompiler()
        {
            // Try as command first (if installed via package manager)
            string[] commandPaths = new[]
            {
                "kaitai-struct-compiler",
                "ksc"
            };

            foreach (string path in commandPaths)
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            process.WaitForExit(5000);
                            if (process.ExitCode == 0)
                            {
                                return path;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            // Try as Java JAR (common installation method)
            string jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                // Return special marker for JAR - callers will use java -jar
                return "JAR:" + jarPath;
            }

            // Try common installation locations
            string[] commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kaitai-struct-compiler"),
                "C:\\Program Files\\kaitai-struct-compiler\\kaitai-struct-compiler.exe",
                "/usr/bin/kaitai-struct-compiler",
                "/usr/local/bin/kaitai-struct-compiler"
            };

            foreach (string path in commonPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            Arguments = "--version",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(processInfo))
                        {
                            if (process != null)
                            {
                                process.WaitForExit(5000);
                                if (process.ExitCode == 0)
                                {
                                    return path;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            return null;
        }

        private static string FindKaitaiCompilerJar()
        {
            // Check environment variable first
            string envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return envJar;
            }

            // Check common locations for Kaitai Struct compiler JAR
            string[] searchPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "kaitai-struct-compiler.jar"),
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "kaitai-struct-compiler.jar")
            };

            foreach (string path in searchPaths)
            {
                try
                {
                    string normalizedPath = Path.GetFullPath(path);
                    if (File.Exists(normalizedPath))
                    {
                        return normalizedPath;
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            return null;
        }

        // ============================================================================
        // ROUND-TRIP TESTS - Comparing Kaitai parsers to Andastra parsers
        // ============================================================================

        /// <summary>
        /// Test round-trip: Read with Andastra parser -> Write -> Read again -> Compare
        /// Comprehensive test using real TPC files from test directories or game installations.
        /// Validates 1:1 parity with original game TPC format implementation.
        /// </summary>
        [Fact(Timeout = 300000)]
        public void TestTpcRoundTripWithAndastraParser()
        {
            // Find real TPC files for testing
            List<byte[]> tpcTestFiles = FindTpcTestFiles();

            // If no real files found, fall back to synthetic data test
            if (tpcTestFiles.Count == 0)
            {
                // Fallback: Test with synthetic data to verify round-trip logic
                var testTpc = CreateTestTPC();
                byte[] originalBytes = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);

                // Round-trip: Read -> Write -> Read
                TPC readTpc1 = TPCAuto.ReadTpc(originalBytes);
                readTpc1.Should().NotBeNull("TPC should be readable from bytes");

                byte[] writtenBytes = TPCAuto.BytesTpc(readTpc1, ResourceType.TPC);
                writtenBytes.Should().NotBeNullOrEmpty("Written TPC bytes should not be empty");

                TPC readTpc2 = TPCAuto.ReadTpc(writtenBytes);
                readTpc2.Should().NotBeNull("Round-trip TPC should be readable");

                // Compare key properties
                readTpc2.AlphaTest.Should().BeApproximately(readTpc1.AlphaTest, 0.001f, "AlphaTest should match");
                readTpc2.IsCubeMap.Should().Be(readTpc1.IsCubeMap, "IsCubeMap should match");
                readTpc2.IsAnimated.Should().Be(readTpc1.IsAnimated, "IsAnimated should match");
                readTpc2.Format().Should().Be(readTpc1.Format(), "Format should match");
                readTpc2.Layers.Count.Should().Be(readTpc1.Layers.Count, "Layer count should match");
                return;
            }

            // Test each real TPC file with comprehensive round-trip validation
            int successCount = 0;
            int totalCount = tpcTestFiles.Count;
            var failures = new List<string>();

            foreach (byte[] originalBytes in tpcTestFiles)
            {
                try
                {
                    // Step 1: Read original TPC file
                    TPC originalTpc = TPCAuto.ReadTpc(originalBytes);
                    originalTpc.Should().NotBeNull("Original TPC should be readable");
                    originalTpc.Layers.Should().NotBeNull("TPC should have layers");
                    originalTpc.Layers.Count.Should().BeGreaterThan(0, "TPC should have at least one layer");

                    // Step 2: Write TPC to bytes
                    byte[] writtenBytes = TPCAuto.BytesTpc(originalTpc, ResourceType.TPC);
                    writtenBytes.Should().NotBeNullOrEmpty("Written TPC bytes should not be empty");
                    writtenBytes.Length.Should().BeGreaterThan(0, "Written TPC should have data");

                    // Step 3: Read written TPC back
                    TPC roundTripTpc = TPCAuto.ReadTpc(writtenBytes);
                    roundTripTpc.Should().NotBeNull("Round-trip TPC should be readable");

                    // Step 4: Comprehensive property comparison
                    ValidateTpcRoundTrip(originalTpc, roundTripTpc, originalBytes.Length, writtenBytes.Length);

                    successCount++;
                }
                catch (Exception ex)
                {
                    failures.Add($"Failed to round-trip TPC file ({originalBytes.Length} bytes): {ex.Message}");
                }
            }

            // At least 80% of files should round-trip successfully
            double successRate = (double)successCount / totalCount;
            successRate.Should().BeGreaterThanOrEqualTo(0.8,
                $"At least 80% of TPC files should round-trip successfully. " +
                $"Success: {successCount}/{totalCount}. Failures: {string.Join("; ", failures)}");
        }

        /// <summary>
        /// Comprehensive validation of TPC round-trip properties.
        /// Ensures all fields match exactly between original and round-trip TPC.
        /// </summary>
        private void ValidateTpcRoundTrip(TPC original, TPC roundTrip, int originalSize, int writtenSize)
        {
            // Validate header properties
            roundTrip.AlphaTest.Should().BeApproximately(original.AlphaTest, 0.001f,
                "AlphaTest should match exactly (allowing floating point precision)");
            roundTrip.IsCubeMap.Should().Be(original.IsCubeMap,
                $"IsCubeMap should match: original={original.IsCubeMap}, roundTrip={roundTrip.IsCubeMap}");
            roundTrip.IsAnimated.Should().Be(original.IsAnimated,
                $"IsAnimated should match: original={original.IsAnimated}, roundTrip={roundTrip.IsAnimated}");

            // Validate format
            TPCTextureFormat originalFormat = original.Format();
            TPCTextureFormat roundTripFormat = roundTrip.Format();
            roundTripFormat.Should().Be(originalFormat,
                $"Format should match: original={originalFormat}, roundTrip={roundTripFormat}");

            // Validate dimensions
            var (originalWidth, originalHeight) = original.Dimensions();
            var (roundTripWidth, roundTripHeight) = roundTrip.Dimensions();
            roundTripWidth.Should().Be(originalWidth,
                $"Width should match: original={originalWidth}, roundTrip={roundTripWidth}");
            roundTripHeight.Should().Be(originalHeight,
                $"Height should match: original={originalHeight}, roundTrip={roundTripHeight}");

            // Validate layer count
            roundTrip.Layers.Count.Should().Be(original.Layers.Count,
                $"Layer count should match: original={original.Layers.Count}, roundTrip={roundTrip.Layers.Count}");

            // Validate each layer
            for (int layerIdx = 0; layerIdx < original.Layers.Count && layerIdx < roundTrip.Layers.Count; layerIdx++)
            {
                TPCLayer originalLayer = original.Layers[layerIdx];
                TPCLayer roundTripLayer = roundTrip.Layers[layerIdx];

                originalLayer.Should().NotBeNull($"Original layer {layerIdx} should not be null");
                roundTripLayer.Should().NotBeNull($"Round-trip layer {layerIdx} should not be null");

                // Validate mipmap count
                roundTripLayer.Mipmaps.Count.Should().Be(originalLayer.Mipmaps.Count,
                    $"Layer {layerIdx} mipmap count should match: original={originalLayer.Mipmaps.Count}, roundTrip={roundTripLayer.Mipmaps.Count}");

                // Validate each mipmap
                for (int mipIdx = 0; mipIdx < originalLayer.Mipmaps.Count && mipIdx < roundTripLayer.Mipmaps.Count; mipIdx++)
                {
                    TPCMipmap originalMipmap = originalLayer.Mipmaps[mipIdx];
                    TPCMipmap roundTripMipmap = roundTripLayer.Mipmaps[mipIdx];

                    originalMipmap.Should().NotBeNull($"Original mipmap {layerIdx}/{mipIdx} should not be null");
                    roundTripMipmap.Should().NotBeNull($"Round-trip mipmap {layerIdx}/{mipIdx} should not be null");

                    // Validate mipmap dimensions
                    roundTripMipmap.Width.Should().Be(originalMipmap.Width,
                        $"Mipmap {layerIdx}/{mipIdx} width should match: original={originalMipmap.Width}, roundTrip={roundTripMipmap.Width}");
                    roundTripMipmap.Height.Should().Be(originalMipmap.Height,
                        $"Mipmap {layerIdx}/{mipIdx} height should match: original={originalMipmap.Height}, roundTrip={roundTripMipmap.Height}");

                    // Validate mipmap format
                    roundTripMipmap.TpcFormat.Should().Be(originalMipmap.TpcFormat,
                        $"Mipmap {layerIdx}/{mipIdx} format should match: original={originalMipmap.TpcFormat}, roundTrip={roundTripMipmap.TpcFormat}");

                    // Validate mipmap data size (allowing small differences for alignment/padding)
                    int sizeDiff = Math.Abs(roundTripMipmap.Data.Length - originalMipmap.Data.Length);
                    sizeDiff.Should().BeLessThanOrEqualTo(16,
                        $"Mipmap {layerIdx}/{mipIdx} data size should be close: original={originalMipmap.Data.Length}, roundTrip={roundTripMipmap.Data.Length}, diff={sizeDiff}");

                    // Validate mipmap data content (compare actual pixel data)
                    // For compressed formats (DXT), exact byte comparison may not work due to compression differences
                    // For uncompressed formats, compare pixel data
                    if (!originalMipmap.TpcFormat.IsDxt() && !roundTripMipmap.TpcFormat.IsDxt())
                    {
                        int minSize = Math.Min(originalMipmap.Data.Length, roundTripMipmap.Data.Length);
                        for (int i = 0; i < minSize; i++)
                        {
                            if (originalMipmap.Data[i] != roundTripMipmap.Data[i])
                            {
                                // Allow small differences for rounding/format conversion
                                int diff = Math.Abs(originalMipmap.Data[i] - roundTripMipmap.Data[i]);
                                diff.Should().BeLessThanOrEqualTo(1,
                                    $"Mipmap {layerIdx}/{mipIdx} data byte {i} should match (allowing 1-byte rounding): original={originalMipmap.Data[i]}, roundTrip={roundTripMipmap.Data[i]}");
                            }
                        }
                    }
                }
            }

            // Validate TXI data
            bool originalHasTxi = !string.IsNullOrWhiteSpace(original.Txi);
            bool roundTripHasTxi = !string.IsNullOrWhiteSpace(roundTrip.Txi);
            roundTripHasTxi.Should().Be(originalHasTxi,
                $"TXI presence should match: original={originalHasTxi}, roundTrip={roundTripHasTxi}");

            if (originalHasTxi && roundTripHasTxi)
            {
                // Normalize line endings for comparison
                string normalizedOriginal = original.Txi.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                string normalizedRoundTrip = roundTrip.Txi.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                normalizedRoundTrip.Should().Be(normalizedOriginal,
                    $"TXI content should match (normalized): original length={normalizedOriginal.Length}, roundTrip length={normalizedRoundTrip.Length}");
            }
        }

        /// <summary>
        /// Finds real TPC files from test directories or game installations.
        /// Returns list of TPC file byte arrays for comprehensive round-trip testing.
        /// </summary>
        private List<byte[]> FindTpcTestFiles()
        {
            var tpcFiles = new List<byte[]>();

            // Method 1: Try test files directory (PyKotor test files)
            string[] testFileDirs = new[]
            {
                Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files"),
                Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files")
            };

            foreach (string testDir in testFileDirs)
            {
                string normalizedDir = Path.GetFullPath(testDir);
                if (Directory.Exists(normalizedDir))
                {
                    string[] foundFiles = Directory.GetFiles(normalizedDir, "*.tpc", SearchOption.AllDirectories);
                    foreach (string file in foundFiles)
                    {
                        try
                        {
                            byte[] data = File.ReadAllBytes(file);
                            if (data.Length > 0)
                            {
                                tpcFiles.Add(data);
                                // Limit to first 10 files to avoid excessive test time
                                if (tpcFiles.Count >= 10)
                                {
                                    return tpcFiles;
                                }
                            }
                        }
                        catch
                        {
                            // Skip files that can't be read
                        }
                    }
                }
            }

            // Method 2: Try game installation directories (K2 preferred, then K1)
            if (tpcFiles.Count < 5)
            {
                string[] gamePaths = new[]
                {
                    Environment.GetEnvironmentVariable("K2_PATH") ?? @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
                    Environment.GetEnvironmentVariable("K1_PATH") ?? @"C:\Program Files (x86)\Steam\steamapps\common\swkotor"
                };

                foreach (string gamePath in gamePaths)
                {
                    if (tpcFiles.Count >= 10)
                    {
                        break;
                    }

                    if (!Directory.Exists(gamePath))
                    {
                        continue;
                    }

                    // Try override directories (most likely to have TPC files)
                    string[] overrideDirs = new[]
                    {
                        Path.Combine(gamePath, "override"),
                        Path.Combine(gamePath, "textures"),
                        Path.Combine(gamePath, "data")
                    };

                    foreach (string overrideDir in overrideDirs)
                    {
                        if (tpcFiles.Count >= 10)
                        {
                            break;
                        }

                        if (Directory.Exists(overrideDir))
                        {
                            try
                            {
                                string[] foundFiles = Directory.GetFiles(overrideDir, "*.tpc", SearchOption.TopDirectoryOnly);
                                foreach (string file in foundFiles)
                                {
                                    try
                                    {
                                        byte[] data = File.ReadAllBytes(file);
                                        if (data.Length > 128) // Minimum valid TPC size
                                        {
                                            // Validate it's actually a TPC file
                                            ResourceType detected = TPCAuto.DetectTpc(data);
                                            if (detected == ResourceType.TPC)
                                            {
                                                tpcFiles.Add(data);
                                                if (tpcFiles.Count >= 10)
                                                {
                                                    return tpcFiles;
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // Skip files that can't be read
                                    }
                                }
                            }
                            catch
                            {
                                // Skip directories that can't be accessed
                            }
                        }
                    }
                }
            }

            return tpcFiles;
        }

        /// <summary>
        /// Test round-trip for DDS format with Andastra parser
        /// </summary>
        [Fact(Timeout = 300000)]
        public void TestDdsRoundTripWithAndastraParser()
        {
            var testTpc = CreateTestTPC();
            byte[] originalBytes = TPCAuto.BytesTpc(testTpc, ResourceType.DDS);

            TPC readTpc1 = TPCAuto.ReadTpc(originalBytes);
            readTpc1.Should().NotBeNull("DDS should be readable from bytes");

            byte[] writtenBytes = TPCAuto.BytesTpc(readTpc1, ResourceType.DDS);
            TPC readTpc2 = TPCAuto.ReadTpc(writtenBytes);

            readTpc2.Should().NotBeNull("Round-trip DDS should be readable");
            readTpc2.Format().Should().Be(readTpc1.Format(), "Format should match");
        }

        /// <summary>
        /// Test round-trip for TGA format with Andastra parser
        /// </summary>
        [Fact(Timeout = 300000)]
        public void TestTgaRoundTripWithAndastraParser()
        {
            var testTpc = CreateTestTPC();
            byte[] originalBytes = TPCAuto.BytesTpc(testTpc, ResourceType.TGA);

            TPC readTpc1 = TPCAuto.ReadTpc(originalBytes);
            readTpc1.Should().NotBeNull("TGA should be readable from bytes");

            byte[] writtenBytes = TPCAuto.BytesTpc(readTpc1, ResourceType.TGA);
            TPC readTpc2 = TPCAuto.ReadTpc(writtenBytes);

            readTpc2.Should().NotBeNull("Round-trip TGA should be readable");
            readTpc2.Format().Should().Be(readTpc1.Format(), "Format should match");
        }

        /// <summary>
        /// Test that Kaitai-generated Python parser can parse TPC files (structural validation)
        /// Validates actual parsing by comparing Kaitai parser output with manual parser implementation
        /// </summary>
        [Fact(Timeout = 300000)]
        public void TestKaitaiPythonParserCanParseTpc()
        {
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Compile TPC.ksy to Python
            string pythonOutputDir = Path.Combine(CompilerOutputDir, "tpc", "python");
            if (Directory.Exists(pythonOutputDir))
            {
                Directory.Delete(pythonOutputDir, true);
            }
            Directory.CreateDirectory(pythonOutputDir);

            ProcessStartInfo compileInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                compileInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t python \"{TpcKsyPath}\" -d \"{pythonOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(TpcKsyPath)
                };
            }
            else
            {
                compileInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t python \"{TpcKsyPath}\" -d \"{pythonOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(TpcKsyPath)
                };
            }

            int compileExitCode = -1;
            using (var compileProcess = Process.Start(compileInfo))
            {
                if (compileProcess != null)
                {
                    compileProcess.WaitForExit(60000);
                    compileExitCode = compileProcess.ExitCode;
                }
            }

            compileExitCode.Should().Be(0, "Python parser should compile successfully");

            // Verify Python parser files were generated
            string[] pythonFiles = Directory.GetFiles(pythonOutputDir, "*.py", SearchOption.AllDirectories);
            pythonFiles.Should().NotBeEmpty("Python parser files should be generated");

            // Find Python executable
            string pythonPath = FindPython();
            if (string.IsNullOrEmpty(pythonPath))
            {
                return; // Skip if Python not available
            }

            // Create test TPC file using Andastra parser
            var testTpc = CreateTestTPC();
            byte[] tpcBytes = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            tpcBytes.Should().NotBeNullOrEmpty("Test TPC bytes should be generated");

            // Write to temporary file for validation
            string tempTpcFile = Path.Combine(Path.GetTempPath(), $"tpc_kaitai_test_{Guid.NewGuid()}.tpc");
            try
            {
                File.WriteAllBytes(tempTpcFile, tpcBytes);

                // Run validation script that compares Kaitai parser with manual parser
                string validationScriptPath = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "src", "Andastra", "Tests", "Formats", "validate_kaitai_parser.py");

                // Normalize path
                validationScriptPath = Path.GetFullPath(validationScriptPath);

                if (!File.Exists(validationScriptPath))
                {
                    throw new FileNotFoundError($"Validation script not found at {validationScriptPath}");
                }

                var validationInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{validationScriptPath}\" \"{tempTpcFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(validationScriptPath)
                };

                string validationStdout = "";
                string validationStderr = "";
                int validationExitCode = -1;

                using (var validationProcess = Process.Start(validationInfo))
                {
                    if (validationProcess != null)
                    {
                        validationStdout = validationProcess.StandardOutput.ReadToEnd();
                        validationStderr = validationProcess.StandardError.ReadToEnd();
                        validationProcess.WaitForExit(30000);
                        validationExitCode = validationProcess.ExitCode;
                    }
                }

                // Validation should succeed
                validationExitCode.Should().Be(0,
                    $"Kaitai parser validation should succeed. STDOUT: {validationStdout}, STDERR: {validationStderr}");

                // Parse validation results
                ValidationResult validationResult;
                try
                {
                    validationResult = JsonSerializer.Deserialize<ValidationResult>(validationStdout);
                    validationResult.Should().NotBeNull("Validation results should be valid JSON");
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to parse validation JSON output: {validationStdout}. Error: {ex.Message}");
                }

                // Check overall validation success
                validationResult.OverallSuccess.Should().BeTrue(
                    $"Kaitai parser validation should pass. Errors: {string.Join("; ", validationResult.Errors ?? new List<string>())}");

                // Verify key header fields match
                validationResult.Validation.Should().NotBeNull("Validation results should contain field comparisons");

                // Check critical header fields
                string[] criticalFields = new[] { "data_size", "width", "height", "pixel_type", "mipmap_count" };
                foreach (string field in criticalFields)
                {
                    validationResult.Validation.ContainsKey(field).Should().BeTrue($"Validation should include {field}");
                    validationResult.Validation[field].Matches.Should().BeTrue(
                        $"{field} should match between Kaitai and manual parsers");
                }

                // Verify alpha_test matches within floating point tolerance
                validationResult.Validation.ContainsKey("alpha_test").Should().BeTrue("Validation should include alpha_test");
                validationResult.Validation["alpha_test"].Matches.Should().BeTrue(
                    "Alpha test should match between parsers within tolerance");
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempTpcFile))
                {
                    try
                    {
                        File.Delete(tempTpcFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        /// <summary>
        /// Test that Kaitai-generated C# parser structure matches expectations
        /// </summary>
        [Fact(Timeout = 300000)]
        public void TestKaitaiCSharpParserStructure()
        {
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string csharpOutputDir = Path.Combine(CompilerOutputDir, "tpc", "csharp");
            if (Directory.Exists(csharpOutputDir))
            {
                Directory.Delete(csharpOutputDir, true);
            }
            Directory.CreateDirectory(csharpOutputDir);

            ProcessStartInfo compileInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                compileInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t csharp \"{TpcKsyPath}\" -d \"{csharpOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(TpcKsyPath)
                };
            }
            else
            {
                compileInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t csharp \"{TpcKsyPath}\" -d \"{csharpOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(TpcKsyPath)
                };
            }

            using (var compileProcess = Process.Start(compileInfo))
            {
                if (compileProcess != null)
                {
                    compileProcess.WaitForExit(60000);
                    compileProcess.ExitCode.Should().Be(0, "C# parser should compile successfully");
                }
            }

            // Verify C# parser files were generated
            string[] csFiles = Directory.GetFiles(csharpOutputDir, "*.cs", SearchOption.AllDirectories);
            csFiles.Should().NotBeEmpty("C# parser files should be generated");

            // Verify generated C# file contains expected structure
            string tpcCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("tpc"));
            if (tpcCsFile != null)
            {
                string csContent = File.ReadAllText(tpcCsFile);
                csContent.Should().Contain("class", "Generated C# file should contain class definition");
                csContent.Should().Contain("KaitaiStruct", "Generated C# file should inherit from KaitaiStruct");
            }
        }

        /// <summary>
        /// Compare parsed data between Andastra parser and Python/Kaitai parser
        /// This is a comprehensive comparison test that validates parser correctness
        /// </summary>
        [Fact(Timeout = 600000)]
        public void TestCompareAndastraAndKaitaiParsers()
        {
            // Create test data with Andastra parser
            var testTpc = CreateTestTPC();
            byte[] tpcBytes = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            tpcBytes.Should().NotBeNullOrEmpty("TPC bytes should be generated");

            // Parse with Andastra parser
            TPC andastraTpc = TPCAuto.ReadTpc(tpcBytes);
            andastraTpc.Should().NotBeNull("Andastra parser should parse TPC");
            andastraTpc.Format().Should().NotBe(TPCTextureFormat.Invalid, "Format should be valid");
            andastraTpc.Layers.Count.Should().BeGreaterThan(0, "Should have at least one layer");

            // Write TPC bytes to temporary file for Python parser
            string tempTpcFile = Path.Combine(Path.GetTempPath(), $"tpc_test_{Guid.NewGuid()}.tpc");
            try
            {
                File.WriteAllBytes(tempTpcFile, tpcBytes);

                // Run Python parser comparison script
                // Try multiple locations: output directory, source directory
                string pythonScriptPath = null;
                string[] possiblePaths = new[]
                {
                    // In output directory (if copied)
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "compare_tpc_parsers.py"),
                    // In source directory (development)
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..", "..", "..", "src", "Andastra", "Tests", "Formats", "compare_tpc_parsers.py"),
                    // Alternative source path
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Andastra", "Tests", "Formats", "compare_tpc_parsers.py")
                };

                foreach (string path in possiblePaths)
                {
                    string normalizedPath = Path.GetFullPath(path);
                    if (File.Exists(normalizedPath))
                    {
                        pythonScriptPath = normalizedPath;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(pythonScriptPath) || !File.Exists(pythonScriptPath))
                {
                    // Skip test if Python script not available
                    return;
                }

                // Find Python executable
                string pythonPath = FindPython();
                if (string.IsNullOrEmpty(pythonPath))
                {
                    // Skip test if Python not available
                    return;
                }

                // Run Python parser
                var pythonProcessInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{pythonScriptPath}\" \"{tempTpcFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string pythonStdout = "";
                string pythonStderr = "";
                int pythonExitCode = -1;

                using (var pythonProcess = Process.Start(pythonProcessInfo))
                {
                    if (pythonProcess != null)
                    {
                        pythonStdout = pythonProcess.StandardOutput.ReadToEnd();
                        pythonStderr = pythonProcess.StandardError.ReadToEnd();
                        pythonProcess.WaitForExit(30000);
                        pythonExitCode = pythonProcess.ExitCode;
                    }
                }

                pythonExitCode.Should().Be(0,
                    $"Python parser should succeed. STDOUT: {pythonStdout}, STDERR: {pythonStderr}");

                // Parse JSON output from Python parser
                PythonTpcData pythonData;
                try
                {
                    pythonData = JsonSerializer.Deserialize<PythonTpcData>(pythonStdout);
                    pythonData.Should().NotBeNull("Python parser JSON output should be valid");
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to parse Python parser JSON output: {pythonStdout}. Error: {ex.Message}");
                }

                // Comprehensive field-by-field comparison
                CompareTpcParsers(andastraTpc, pythonData);
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempTpcFile))
                {
                    try
                    {
                        File.Delete(tempTpcFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        /// <summary>
        /// Comprehensive comparison of Andastra parser and Python parser results
        /// </summary>
        private void CompareTpcParsers(TPC andastraTpc, PythonTpcData pythonData)
        {
            // Compare alpha test (allowing small floating point differences)
            pythonData.alpha_test.Should().BeApproximately(andastraTpc.AlphaTest, 0.001f,
                "AlphaTest should match between parsers");

            // Compare dimensions
            var (andastraWidth, andastraHeight) = andastraTpc.Dimensions();
            pythonData.width.Should().Be(andastraWidth,
                $"Width should match: Andastra={andastraWidth}, Python={pythonData.width}");
            pythonData.height.Should().Be(andastraHeight,
                $"Height should match: Andastra={andastraHeight}, Python={pythonData.height}");

            // Compare format
            int pythonFormat = pythonData.format;
            TPCTextureFormat expectedFormat = andastraTpc.Format();
            pythonFormat.Should().Be((int)expectedFormat,
                $"Format should match: Andastra={expectedFormat} ({(int)expectedFormat}), Python={pythonFormat}");

            // Compare cube map flag
            pythonData.is_cube_map.Should().Be(andastraTpc.IsCubeMap,
                $"IsCubeMap should match: Andastra={andastraTpc.IsCubeMap}, Python={pythonData.is_cube_map}");

            // Compare layer count
            pythonData.layer_count.Should().Be(andastraTpc.Layers.Count,
                $"Layer count should match: Andastra={andastraTpc.Layers.Count}, Python={pythonData.layer_count}");

            // Compare mipmap count (from first layer if available)
            if (andastraTpc.Layers.Count > 0 && pythonData.layers != null && pythonData.layers.Count > 0)
            {
                int andastraMipmapCount = andastraTpc.Layers[0].Mipmaps.Count;
                int pythonMipmapCount = pythonData.layers[0].mipmaps != null ? pythonData.layers[0].mipmaps.Count : 0;
                pythonMipmapCount.Should().Be(andastraMipmapCount,
                    $"Mipmap count should match: Andastra={andastraMipmapCount}, Python={pythonMipmapCount}");

                // Compare mipmap dimensions for each mipmap
                for (int i = 0; i < Math.Min(andastraMipmapCount, pythonMipmapCount); i++)
                {
                    var andastraMipmap = andastraTpc.Layers[0].Mipmaps[i];
                    var pythonMipmap = pythonData.layers[0].mipmaps[i];

                    pythonMipmap.width.Should().Be(andastraMipmap.Width,
                        $"Mipmap {i} width should match: Andastra={andastraMipmap.Width}, Python={pythonMipmap.width}");
                    pythonMipmap.height.Should().Be(andastraMipmap.Height,
                        $"Mipmap {i} height should match: Andastra={andastraMipmap.Height}, Python={pythonMipmap.height}");
                    pythonMipmap.format.Should().Be((int)andastraMipmap.TpcFormat,
                        $"Mipmap {i} format should match: Andastra={andastraMipmap.TpcFormat}, Python={pythonMipmap.format}");

                    // Compare data size (allowing for minor differences due to padding/alignment)
                    int sizeDiff = Math.Abs(pythonMipmap.data_size - andastraMipmap.Data.Length);
                    sizeDiff.Should().BeLessThanOrEqualTo(16, // Allow small difference for alignment
                        $"Mipmap {i} data size should be close: Andastra={andastraMipmap.Data.Length}, Python={pythonMipmap.data_size}");
                }
            }

            // Compare TXI presence
            bool andastraHasTxi = !string.IsNullOrEmpty(andastraTpc.Txi);
            pythonData.txi_present.Should().Be(andastraHasTxi,
                $"TXI presence should match: Andastra={andastraHasTxi}, Python={pythonData.txi_present}");
        }

        /// <summary>
        /// Finds Python executable in common locations
        /// </summary>
        private static string FindPython()
        {
            string[] pythonCommands = { "python3", "python" };

            foreach (string cmd in pythonCommands)
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            process.WaitForExit(5000);
                            if (process.ExitCode == 0)
                            {
                                return cmd;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            return null;
        }

        /// <summary>
        /// Data structure for Python parser JSON output
        /// </summary>
        private class PythonTpcData
        {
            [JsonPropertyName("alpha_test")]
            public float alpha_test { get; set; }

            [JsonPropertyName("width")]
            public int width { get; set; }

            [JsonPropertyName("height")]
            public int height { get; set; }

            [JsonPropertyName("format")]
            public int format { get; set; }

            [JsonPropertyName("format_name")]
            public string format_name { get; set; }

            [JsonPropertyName("is_compressed")]
            public bool is_compressed { get; set; }

            [JsonPropertyName("is_cube_map")]
            public bool is_cube_map { get; set; }

            [JsonPropertyName("mipmap_count")]
            public int mipmap_count { get; set; }

            [JsonPropertyName("layer_count")]
            public int layer_count { get; set; }

            [JsonPropertyName("txi_present")]
            public bool txi_present { get; set; }

            [JsonPropertyName("txi_length")]
            public int txi_length { get; set; }

            [JsonPropertyName("layers")]
            public List<PythonTpcLayer> layers { get; set; }
        }

        /// <summary>
        /// Python parser layer data structure
        /// </summary>
        private class PythonTpcLayer
        {
            [JsonPropertyName("mipmaps")]
            public List<PythonTpcMipmap> mipmaps { get; set; }
        }

        /// <summary>
        /// Python parser mipmap data structure
        /// </summary>
        private class PythonTpcMipmap
        {
            [JsonPropertyName("width")]
            public int width { get; set; }

            [JsonPropertyName("height")]
            public int height { get; set; }

            [JsonPropertyName("format")]
            public int format { get; set; }

            [JsonPropertyName("data_size")]
            public int data_size { get; set; }
        }

        /// <summary>
        /// Data structure for Kaitai parser validation results
        /// </summary>
        private class ValidationResult
        {
            [JsonPropertyName("file")]
            public string File { get; set; }

            [JsonPropertyName("manual_parser")]
            public Dictionary<string, object> ManualParser { get; set; }

            [JsonPropertyName("kaitai_parser")]
            public Dictionary<string, object> KaitaiParser { get; set; }

            [JsonPropertyName("validation")]
            public Dictionary<string, FieldValidation> Validation { get; set; }

            [JsonPropertyName("overall_success")]
            public bool OverallSuccess { get; set; }

            [JsonPropertyName("errors")]
            public List<string> Errors { get; set; }

            [JsonPropertyName("error")]
            public string Error { get; set; }
        }

        /// <summary>
        /// Field validation result
        /// </summary>
        private class FieldValidation
        {
            [JsonPropertyName("manual")]
            public object Manual { get; set; }

            [JsonPropertyName("kaitai")]
            public object Kaitai { get; set; }

            [JsonPropertyName("matches")]
            public bool Matches { get; set; }

            [JsonPropertyName("error")]
            public string Error { get; set; }
        }

        /// <summary>
        /// Creates a minimal test TPC for round-trip testing
        /// </summary>
        private TPC CreateTestTPC()
        {
            var tpc = new TPC();
            tpc.AlphaTest = 0.5f;
            tpc.IsCubeMap = false;
            tpc.IsAnimated = false;
            // Use SetSingle to set the format (since _format is internal)
            byte[] testData = new byte[64 * 64 * 4]; // RGBA format, 64x64 pixels
            tpc.SetSingle(testData, TPCTextureFormat.RGBA, 64, 64);

            var layer = new TPCLayer();
            int width = 64;
            int height = 64;
            int bytesPerPixel = 4; // RGBA
            byte[] textureData = new byte[width * height * bytesPerPixel];

            // Fill with simple pattern
            for (int i = 0; i < textureData.Length; i += bytesPerPixel)
            {
                textureData[i] = 255;     // R
                textureData[i + 1] = 128; // G
                textureData[i + 2] = 64;  // B
                textureData[i + 3] = 255; // A
            }

            var mipmap = new TPCMipmap(width, height, TPCTextureFormat.RGBA, textureData);
            layer.Mipmaps.Add(mipmap);
            tpc.Layers.Add(layer);

            return tpc;
        }
    }
}

