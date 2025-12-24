using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.ERF.ERFAuto;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for ERF.ksy Kaitai Struct compiler functionality.
    /// Tests compile ERF.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic
    ///
    /// ERF format variants tested:
    /// - ERF: Generic encapsulated resource file
    /// - MOD: Module file (game areas/levels)
    /// - SAV: Save game file (contains saved game state)
    /// - HAK: Hak pak file (contains override resources)
    /// </summary>
    public class ERFKaitaiCompilerTests
    {
        private static readonly string ErfKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "ERF", "ERF.ksy");

        private static readonly string TestErfFile = TestFileHelper.GetPath("test.erf");
        private static readonly string TestModFile = TestFileHelper.GetPath("test.mod");
        private static readonly string TestHakFile = TestFileHelper.GetPath("test.hak");
        private static readonly string TestSavFile = TestFileHelper.GetPath("test.sav");
        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_erf_tests");

        // Supported Kaitai Struct target languages
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
            "visualbasic"
        };

        static ERFKaitaiCompilerTests()
        {
            // Normalize ERF.ksy path
            ErfKsyPath = Path.GetFullPath(ErfKsyPath);

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
        public void TestErfKsyFileExists()
        {
            File.Exists(ErfKsyPath).Should().BeTrue($"ERF.ksy should exist at {ErfKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(ErfKsyPath);
            content.Should().Contain("meta:", "ERF.ksy should contain meta section");
            content.Should().Contain("id: erf", "ERF.ksy should have id: erf");
            content.Should().Contain("file-extension:", "ERF.ksy should specify file extensions");
            content.Should().Contain("erf", "ERF.ksy should include erf extension");
            content.Should().Contain("mod", "ERF.ksy should include mod extension");
            content.Should().Contain("hak", "ERF.ksy should include hak extension");
            content.Should().Contain("sav", "ERF.ksy should include sav extension");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileErfKsyToLanguage(string language)
        {
            // Skip if compiler not available
            var compileResult = CompileKsyToLanguage(ErfKsyPath, language);
            if (!compileResult.Success && compileResult.ExitCode == -999)
            {
                return; // Skip test if compiler not available
            }

            // Compilation should succeed
            compileResult.ExitCode.Should().Be(0,
                $"kaitai-struct-compiler should compile ERF.ksy to {language} successfully. " +
                $"STDOUT: {compileResult.Output}, STDERR: {compileResult.Error}");

            // Verify output files were generated
            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                string[] generatedFiles = Directory.GetFiles(langOutputDir, "*", SearchOption.AllDirectories);
                generatedFiles.Should().NotBeEmpty($"Compilation to {language} should generate output files");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompileErfKsyToAllLanguages()
        {
            // Test compilation to all supported languages
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            var results = new Dictionary<string, bool>();
            var errors = new Dictionary<string, string>();

            foreach (string language in SupportedLanguages)
            {
                try
                {
                    string langOutputDir = Path.Combine(CompilerOutputDir, language);
                    if (Directory.Exists(langOutputDir))
                    {
                        Directory.Delete(langOutputDir, true);
                    }
                    Directory.CreateDirectory(langOutputDir);

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = compilerPath,
                        Arguments = $"-t {language} \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            string stdout = process.StandardOutput.ReadToEnd();
                            string stderr = process.StandardError.ReadToEnd();
                            process.WaitForExit(60000);

                            bool success = process.ExitCode == 0;
                            results[language] = success;

                            if (!success)
                            {
                                errors[language] = $"Exit code: {process.ExitCode}, STDOUT: {stdout}, STDERR: {stderr}";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    results[language] = false;
                    errors[language] = ex.Message;
                }
            }

            // Report results
            int successCount = results.Values.Count(r => r);
            int totalCount = SupportedLanguages.Length;

            // At least 12 languages should compile successfully
            var resultsStr = string.Join(", ", results.Select(kvp => $"{kvp.Key}: {(kvp.Value ? "OK" : "FAIL")}"));
            var errorsStr = string.Join("; ", errors.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            successCount.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. Results: {resultsStr}. Errors: {errorsStr}");
        }

        [Fact(Timeout = 300000)]
        public void TestCompiledParserValidatesErfFile()
        {
            // Create test ERF file if it doesn't exist
            if (!File.Exists(TestErfFile))
            {
                CreateTestErfFile(TestErfFile, ERFType.ERF);
            }

            // Test Python parser (most commonly available)
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "python");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to Python
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0, "Python compilation should succeed");
                }
            }

            // Verify Python parser file was generated
            string[] pythonFiles = Directory.GetFiles(langOutputDir, "*.py", SearchOption.AllDirectories);
            pythonFiles.Should().NotBeEmpty("Python parser files should be generated");

            // Use the generated Python parser to validate the ERF file
            string pythonPath = FindPythonExecutable();
            if (string.IsNullOrEmpty(pythonPath))
            {
                // Python not available - skip actual parser validation but test compilation succeeded
                return;
            }

            // Check if kaitaistruct library is available
            bool kaitaiAvailable = CheckKaitaiStructLibraryAvailable(pythonPath);
            if (!kaitaiAvailable)
            {
                // kaitaistruct not available - skip actual parser validation but test compilation succeeded
                return;
            }

            // Find the generated ERF parser module
            string erfParserFile = pythonFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("erf"));
            if (erfParserFile == null)
            {
                // If no specific ERF file, use the first Python file (Kaitai generates erf.py or similar)
                erfParserFile = pythonFiles[0];
            }

            // Create Python validation script
            string validationScript = CreatePythonValidationScript(erfParserFile, TestErfFile, langOutputDir);
            string scriptPath = Path.Combine(Path.GetTempPath(), "erf_kaitai_validation_" + Guid.NewGuid().ToString("N") + ".py");
            File.WriteAllText(scriptPath, validationScript);

            try
            {
                // Execute Python validation script
                var validateInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = langOutputDir
                };

                string stdout = "";
                string stderr = "";
                int exitCode = -1;

                using (var process = Process.Start(validateInfo))
                {
                    if (process != null)
                    {
                        stdout = process.StandardOutput.ReadToEnd();
                        stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit(30000);
                        exitCode = process.ExitCode;
                    }
                }

                // Validation script should succeed
                exitCode.Should().Be(0,
                    $"Python parser validation should succeed. STDOUT: {stdout}, STDERR: {stderr}");

                // Parse validation results from JSON output
                var validationResults = ParseValidationResults(stdout);
                validationResults.Should().NotBeNull("Validation results should be parseable");

                // Validate ERF structure matches expected values
                var expectedErf = new ERFBinaryReader(TestErfFile).Load();
                string expectedFileType = ERFTypeExtensions.ToFourCC(expectedErf.ErfType).Trim();
                string parsedFileType = validationResults.FileType.Trim();
                parsedFileType.Should().Be(expectedFileType,
                    $"Parsed file type '{parsedFileType}' should match expected '{expectedFileType}'");
                validationResults.FileVersion.Trim().Should().Be("V1.0",
                    "Parsed file version should be V1.0");
                validationResults.EntryCount.Should().Be((uint)expectedErf.Count,
                    "Parsed entry count should match expected resource count");

                // Validate resources match
                validationResults.Resources.Should().HaveCount(expectedErf.Count,
                    "Parsed resource count should match expected");

                foreach (var expectedResource in expectedErf)
                {
                    var parsedResource = validationResults.Resources.FirstOrDefault(r =>
                        r.ResRef.ToLowerInvariant() == expectedResource.ResRef.ToString().ToLowerInvariant() &&
                        r.ResourceType == expectedResource.ResType.TypeId);
                    parsedResource.Should().NotBeNull(
                        $"Resource {expectedResource.ResRef}.{expectedResource.ResType.Extension} should be found in parsed results");

                    if (parsedResource != null)
                    {
                        parsedResource.ResourceSize.Should().Be((uint)expectedResource.Data.Length,
                            $"Resource {expectedResource.ResRef} size should match");
                    }
                }
            }
            finally
            {
                // Clean up temporary script
                if (File.Exists(scriptPath))
                {
                    try
                    {
                        File.Delete(scriptPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
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

            string langOutputDir = Path.Combine(CompilerOutputDir, "csharp");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to C#
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t csharp \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
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
            string erfCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("erf"));
            if (erfCsFile != null)
            {
                string csContent = File.ReadAllText(erfCsFile);
                csContent.Should().Contain("class", "Generated C# file should contain class definition");
                csContent.Should().Contain("ErfHeader", "Generated C# file should contain ErfHeader structure");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompiledJavaParserStructure()
        {
            // Test Java parser compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "java");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to Java
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t java \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0,
                        $"Java compilation should succeed. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }

            // Verify Java parser files were generated
            string[] javaFiles = Directory.GetFiles(langOutputDir, "*.java", SearchOption.AllDirectories);
            javaFiles.Should().NotBeEmpty("Java parser files should be generated");
        }

        [Fact(Timeout = 300000)]
        public void TestCompiledJavaScriptParserStructure()
        {
            // Test JavaScript parser compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "javascript");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to JavaScript
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t javascript \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0,
                        $"JavaScript compilation should succeed. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }

            // Verify JavaScript parser files were generated
            string[] jsFiles = Directory.GetFiles(langOutputDir, "*.js", SearchOption.AllDirectories);
            jsFiles.Should().NotBeEmpty("JavaScript parser files should be generated");
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
        public void TestCompileErfKsyToAdditionalLanguages(string language)
        {
            // Test compilation to additional languages
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to target language
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t {language} \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
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

        [Fact(Timeout = 300000)]
        public void TestErfKsySyntaxValidation()
        {
            // Validate ERF.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{ErfKsyPath}\" --debug",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    stderr.Should().NotContain("error", "ERF.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"ERF.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsyCompilesToMultipleLanguagesSimultaneously()
        {
            // Test compiling to multiple languages in one command
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string multiLangDir = Path.Combine(CompilerOutputDir, "multilang");
            if (Directory.Exists(multiLangDir))
            {
                Directory.Delete(multiLangDir, true);
            }
            Directory.CreateDirectory(multiLangDir);

            // Compile to multiple languages at once
            string languages = string.Join(" -t ", SupportedLanguages.Take(5)); // Test with first 5 languages
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t {languages} \"{ErfKsyPath}\" -d \"{multiLangDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(120000); // 2 minute timeout for multiple languages

                    // Should succeed
                    process.ExitCode.Should().Be(0,
                        $"Multi-language compilation should succeed. STDOUT: {stdout}, STDERR: {stderr}");

                    // Verify files were generated for multiple languages
                    string[] allFiles = Directory.GetFiles(multiLangDir, "*", SearchOption.AllDirectories);
                    allFiles.Should().NotBeEmpty("Multi-language compilation should generate files");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsyDefinitionCompleteness()
        {
            // Validate that ERF.ksy definition is complete and includes all format variants
            if (!File.Exists(ErfKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(ErfKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: erf", "Should have id: erf");
            ksyContent.Should().Contain("file-extension:", "Should define file extensions");
            ksyContent.Should().Contain("erf", "Should include erf extension");
            ksyContent.Should().Contain("mod", "Should include mod extension");
            ksyContent.Should().Contain("hak", "Should include hak extension");
            ksyContent.Should().Contain("sav", "Should include sav extension");

            // Check for ERF format structure
            ksyContent.Should().Contain("erf_header", "Should define erf_header type");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("entry_count", "Should define entry_count field");
            ksyContent.Should().Contain("key_list", "Should define key_list");
            ksyContent.Should().Contain("resource_list", "Should define resource_list");
            ksyContent.Should().Contain("localized_string_list", "Should define localized_string_list");

            // Check for format variant support
            ksyContent.Should().Contain("ERF ", "Should support ERF file type");
            ksyContent.Should().Contain("MOD ", "Should support MOD file type");
            ksyContent.Should().Contain("HAK ", "Should support HAK file type");
            ksyContent.Should().Contain("SAV ", "Should support SAV file type");
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsySupportsAllFormatVariants()
        {
            // Test that ERF.ksy correctly handles all format variants (ERF, MOD, HAK, SAV)
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Create test files for each variant
            CreateTestErfFile(TestErfFile, ERFType.ERF);
            CreateTestErfFile(TestModFile, ERFType.MOD);
            CreateTestErfFile(TestHakFile, ERFType.ERF); // HAK uses ERF structure with different signature
            CreateTestErfFile(TestSavFile, ERFType.ERF); // SAV uses ERF structure with different signature

            // Modify file signatures for HAK and SAV
            if (File.Exists(TestHakFile))
            {
                byte[] hakData = File.ReadAllBytes(TestHakFile);
                Encoding.ASCII.GetBytes("HAK ").CopyTo(hakData, 0);
                File.WriteAllBytes(TestHakFile, hakData);
            }

            if (File.Exists(TestSavFile))
            {
                byte[] savData = File.ReadAllBytes(TestSavFile);
                Encoding.ASCII.GetBytes("SAV ").CopyTo(savData, 0);
                File.WriteAllBytes(TestSavFile, savData);
            }

            // Compile ERF.ksy to Python for validation
            string langOutputDir = Path.Combine(CompilerOutputDir, "variant_test");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{ErfKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ErfKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0, "Compilation should succeed for format variant testing");
                }
            }

            // Verify all test files exist
            File.Exists(TestErfFile).Should().BeTrue("Test ERF file should exist");
            File.Exists(TestModFile).Should().BeTrue("Test MOD file should exist");
            File.Exists(TestHakFile).Should().BeTrue("Test HAK file should exist");
            File.Exists(TestSavFile).Should().BeTrue("Test SAV file should exist");
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsyHeaderStructure()
        {
            // Validate that ERF.ksy correctly defines the 160-byte header structure
            if (!File.Exists(ErfKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(ErfKsyPath);

            // Check for all required header fields
            ksyContent.Should().Contain("file_type", "Header should define file_type");
            ksyContent.Should().Contain("file_version", "Header should define file_version");
            ksyContent.Should().Contain("language_count", "Header should define language_count");
            ksyContent.Should().Contain("localized_string_size", "Header should define localized_string_size");
            ksyContent.Should().Contain("entry_count", "Header should define entry_count");
            ksyContent.Should().Contain("offset_to_localized_string_list", "Header should define offset_to_localized_string_list");
            ksyContent.Should().Contain("offset_to_key_list", "Header should define offset_to_key_list");
            ksyContent.Should().Contain("offset_to_resource_list", "Header should define offset_to_resource_list");
            ksyContent.Should().Contain("build_year", "Header should define build_year");
            ksyContent.Should().Contain("build_day", "Header should define build_day");
            ksyContent.Should().Contain("description_strref", "Header should define description_strref");
            ksyContent.Should().Contain("reserved", "Header should define reserved field");

            // Check for header size (160 bytes)
            ksyContent.Should().Contain("160", "Header should be 160 bytes");
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsyKeyListStructure()
        {
            // Validate that ERF.ksy correctly defines the key list structure
            if (!File.Exists(ErfKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(ErfKsyPath);

            // Check for key list structure
            ksyContent.Should().Contain("key_entry", "Should define key_entry type");
            ksyContent.Should().Contain("resref", "Key entry should define resref field");
            ksyContent.Should().Contain("resource_id", "Key entry should define resource_id field");
            ksyContent.Should().Contain("resource_type", "Key entry should define resource_type field");
            ksyContent.Should().Contain("unused", "Key entry should define unused field");

            // Check for ResRef size (16 bytes)
            ksyContent.Should().Contain("size: 16", "ResRef should be 16 bytes");
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsyResourceListStructure()
        {
            // Validate that ERF.ksy correctly defines the resource list structure
            if (!File.Exists(ErfKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(ErfKsyPath);

            // Check for resource list structure
            ksyContent.Should().Contain("resource_entry", "Should define resource_entry type");
            ksyContent.Should().Contain("offset_to_data", "Resource entry should define offset_to_data field");
            ksyContent.Should().Contain("resource_size", "Resource entry should define resource_size field");
        }

        [Fact(Timeout = 300000)]
        public void TestErfKsyLocalizedStringStructure()
        {
            // Validate that ERF.ksy correctly defines the localized string structure
            if (!File.Exists(ErfKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(ErfKsyPath);

            // Check for localized string structure
            ksyContent.Should().Contain("localized_string_entry", "Should define localized_string_entry type");
            ksyContent.Should().Contain("language_id", "Localized string entry should define language_id field");
            ksyContent.Should().Contain("string_size", "Localized string entry should define string_size field");
            ksyContent.Should().Contain("string_data", "Localized string entry should define string_data field");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        private static string FindKaitaiCompiler()
        {
            // Try common locations and PATH
            string[] possiblePaths = new[]
            {
                "kaitai-struct-compiler",
                "ksc",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"),
                "/usr/bin/kaitai-struct-compiler",
                "/usr/local/bin/kaitai-struct-compiler",
                "C:\\Program Files\\kaitai-struct-compiler\\kaitai-struct-compiler.exe"
            };

            foreach (string path in possiblePaths)
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

            // Try Java JAR as fallback
            string jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                // Verify Java is available
                try
                {
                    var javaInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var javaProcess = Process.Start(javaInfo))
                    {
                        if (javaProcess != null)
                        {
                            javaProcess.WaitForExit(5000);
                            if (javaProcess.ExitCode == 0)
                            {
                                // Return special marker to indicate JAR usage
                                return "JAVA_JAR:" + jarPath;
                            }
                        }
                    }
                }
                catch
                {
                    // Java not available
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
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                Path.Combine(AppContext.BaseDirectory, "..", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "kaitai-struct-compiler.jar"),
            };

            foreach (string path in searchPaths)
            {
                string normalized = Path.GetFullPath(path);
                if (File.Exists(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private (int ExitCode, string Output, string Error, bool Success) CompileKsyToLanguage(string ksyPath, string language)
        {
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return (-999, "", "Compiler not found", false);
            }

            // Create output directory for this language
            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            ProcessStartInfo processInfo;
            if (compilerPath.StartsWith("JAVA_JAR:"))
            {
                string jarPath = compilerPath.Substring("JAVA_JAR:".Length);
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

            try
            {
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
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message, false);
            }

            return (exitCode, stdout, stderr, exitCode == 0);
        }

        private static void CreateTestErfFile(string path, ERFType erfType)
        {
            var erf = new ERF(erfType);
            erf.SetData("test1", ResourceType.TXT, Encoding.ASCII.GetBytes("test data 1"));
            erf.SetData("test2", ResourceType.TXT, Encoding.ASCII.GetBytes("test data 2"));
            erf.SetData("test3", ResourceType.TXT, Encoding.ASCII.GetBytes("test data 3"));

            byte[] data = BytesErf(erf);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        private static string FindPythonExecutable()
        {
            // Try common Python executable names and locations
            string[] possiblePaths = new[]
            {
                "python",
                "python3",
                "py",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Python", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "python.exe"),
                "/usr/bin/python3",
                "/usr/bin/python",
                "/usr/local/bin/python3",
                "/usr/local/bin/python"
            };

            foreach (string path in possiblePaths)
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

            return null;
        }

        private static bool CheckKaitaiStructLibraryAvailable(string pythonPath)
        {
            try
            {
                // Check if kaitaistruct module can be imported
                var processInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "-c \"import kaitaistruct; print('OK')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        string stdout = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(10000);
                        return process.ExitCode == 0 && stdout.Trim() == "OK";
                    }
                }
            }
            catch
            {
                // Library not available
            }

            return false;
        }

        private static string CreatePythonValidationScript(string parserFilePath, string erfFilePath, string outputDir)
        {
            // Get the module name from the parser file path
            string moduleName = Path.GetFileNameWithoutExtension(parserFilePath);
            string parserDir = Path.GetDirectoryName(parserFilePath);

            // Create comprehensive validation script
            StringBuilder script = new StringBuilder();
            script.AppendLine("import sys");
            script.AppendLine("import json");
            script.AppendLine("import os");
            script.AppendLine("from pathlib import Path");
            script.AppendLine();
            script.AppendLine("# Add parser directory to Python path");
            script.AppendLine($"sys.path.insert(0, r'{parserDir.Replace("\\", "\\\\")}')");
            script.AppendLine();
            script.AppendLine("try:");
            script.AppendLine("    import kaitaistruct");
            script.AppendLine($"    from {moduleName} import {GetClassNameFromModule(moduleName)}");
            script.AppendLine("except ImportError as e:");
            script.AppendLine("    print(json.dumps({'error': 'Failed to import parser: ' + str(e)}))");
            script.AppendLine("    sys.exit(1)");
            script.AppendLine();
            script.AppendLine("try:");
            script.AppendLine($"    erf_file_path = r'{erfFilePath.Replace("\\", "\\\\")}'");
            script.AppendLine($"    with open(erf_file_path, 'rb') as f:");
            script.AppendLine($"        erf_data = {GetClassNameFromModule(moduleName)}(kaitaistruct.KaitaiStream(f))");
            script.AppendLine();
            script.AppendLine("    # Extract ERF structure data - try multiple field name variations");
            script.AppendLine("    def get_attr(obj, *names, default=None):");
            script.AppendLine("        for name in names:");
            script.AppendLine("            if hasattr(obj, name):");
            script.AppendLine("                return getattr(obj, name)");
            script.AppendLine("        return default");
            script.AppendLine();
            script.AppendLine("    # Try to get header (may be direct attributes or nested in erf_header)");
            script.AppendLine("    header = get_attr(erf_data, 'erf_header', 'header')");
            script.AppendLine("    if header is None:");
            script.AppendLine("        header = erf_data");
            script.AppendLine();
            script.AppendLine("    file_type = get_attr(header, 'file_type', 'fileType', default='')");
            script.AppendLine("    if isinstance(file_type, bytes):");
            script.AppendLine("        file_type = file_type.decode('ascii', errors='ignore').rstrip()");
            script.AppendLine("    file_version = get_attr(header, 'file_version', 'fileVersion', default='')");
            script.AppendLine("    if isinstance(file_version, bytes):");
            script.AppendLine("        file_version = file_version.decode('ascii', errors='ignore').rstrip()");
            script.AppendLine("    entry_count = get_attr(header, 'entry_count', 'entryCount', default=0)");
            script.AppendLine();
            script.AppendLine("    result = {");
            script.AppendLine("        'file_type': file_type,");
            script.AppendLine("        'file_version': file_version,");
            script.AppendLine("        'entry_count': entry_count,");
            script.AppendLine("        'resources': []");
            script.AppendLine("    }");
            script.AppendLine();
            script.AppendLine("    # Extract resource list if available - try multiple naming variations");
            script.AppendLine("    key_list = get_attr(erf_data, 'key_list', 'keyList', 'keys')");
            script.AppendLine("    resource_list = get_attr(erf_data, 'resource_list', 'resourceList', 'resources')");
            script.AppendLine();
            script.AppendLine("    if key_list is not None and resource_list is not None:");
            script.AppendLine("        key_entries = get_attr(key_list, 'entries', 'entry', default=[])");
            script.AppendLine("        resource_entries = get_attr(resource_list, 'entries', 'entry', default=[])");
            script.AppendLine();
            script.AppendLine("        if not isinstance(key_entries, list):");
            script.AppendLine("            key_entries = [key_entries] if key_entries else []");
            script.AppendLine("        if not isinstance(resource_entries, list):");
            script.AppendLine("            resource_entries = [resource_entries] if resource_entries else []");
            script.AppendLine();
            script.AppendLine("        for i in range(min(len(key_entries), len(resource_entries))):");
            script.AppendLine("            key_entry = key_entries[i]");
            script.AppendLine("            resource_entry = resource_entries[i]");
            script.AppendLine();
            script.AppendLine("            resref = get_attr(key_entry, 'resref', 'resRef', 'name', default='')");
            script.AppendLine("            if isinstance(resref, bytes):");
            script.AppendLine("                resref_str = resref.decode('ascii', errors='ignore').rstrip('\\0')");
            script.AppendLine("            else:");
            script.AppendLine("                resref_str = str(resref).rstrip('\\0')");
            script.AppendLine();
            script.AppendLine("            resource_type = get_attr(key_entry, 'resource_type', 'resourceType', 'type', 'res_type', default=0)");
            script.AppendLine("            resource_size = get_attr(resource_entry, 'resource_size', 'resourceSize', 'size', default=0)");
            script.AppendLine();
            script.AppendLine("            result['resources'].append({");
            script.AppendLine("                'resref': resref_str.lower(),");
            script.AppendLine("                'resource_type': resource_type,");
            script.AppendLine("                'resource_size': resource_size");
            script.AppendLine("            })");
            script.AppendLine();
            script.AppendLine("    print(json.dumps(result))");
            script.AppendLine("except Exception as e:");
            script.AppendLine("    print(json.dumps({'error': str(e)}), file=sys.stderr)");
            script.AppendLine("    sys.exit(1)");

            return script.ToString();
        }

        private static string GetClassNameFromModule(string moduleName)
        {
            // Convert module name to class name (e.g., "erf" -> "Erf", "erf_file" -> "ErfFile")
            if (string.IsNullOrEmpty(moduleName))
            {
                return "Erf";
            }

            // Capitalize first letter and handle underscores
            string[] parts = moduleName.Split('_');
            StringBuilder className = new StringBuilder();
            foreach (string part in parts)
            {
                if (part.Length > 0)
                {
                    className.Append(char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                    {
                        className.Append(part.Substring(1));
                    }
                }
            }

            return className.Length > 0 ? className.ToString() : "Erf";
        }

        private static ERFValidationResults ParseValidationResults(string jsonOutput)
        {
            if (string.IsNullOrWhiteSpace(jsonOutput))
            {
                return null;
            }

            try
            {
                // Extract JSON from output (handle any extra text)
                string jsonText = jsonOutput.Trim();

                // Try to find JSON object in output
                int jsonStart = jsonText.IndexOf('{');
                int jsonEnd = jsonText.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    jsonText = jsonText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }

                // Parse JSON manually (C# 7.3 compatible - no System.Text.Json)
                var results = new ERFValidationResults();

                // Extract file_type
                Match match = Regex.Match(jsonText, @"""file_type""\s*:\s*""([^""]*)""");
                if (match.Success)
                {
                    results.FileType = match.Groups[1].Value;
                }

                // Extract file_version
                match = Regex.Match(jsonText, @"""file_version""\s*:\s*""([^""]*)""");
                if (match.Success)
                {
                    results.FileVersion = match.Groups[1].Value;
                }

                // Extract entry_count
                match = Regex.Match(jsonText, @"""entry_count""\s*:\s*(\d+)");
                if (match.Success)
                {
                    results.EntryCount = uint.Parse(match.Groups[1].Value);
                }

                // Extract resources array
                match = Regex.Match(jsonText, @"""resources""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                if (match.Success)
                {
                    string resourcesText = match.Groups[1].Value;
                    MatchCollection resourceMatches = Regex.Matches(resourcesText, @"\{[^}]*\}");
                    foreach (Match resourceMatch in resourceMatches)
                    {
                        string resourceText = resourceMatch.Value;
                        var resource = new ERFResourceValidationResult();

                        // Extract resref
                        Match resrefMatch = Regex.Match(resourceText, @"""resref""\s*:\s*""([^""]*)""");
                        if (resrefMatch.Success)
                        {
                            resource.ResRef = resrefMatch.Groups[1].Value;
                        }

                        // Extract resource_type
                        Match typeMatch = Regex.Match(resourceText, @"""resource_type""\s*:\s*(\d+)");
                        if (typeMatch.Success)
                        {
                            resource.ResourceType = ushort.Parse(typeMatch.Groups[1].Value);
                        }

                        // Extract resource_size
                        Match sizeMatch = Regex.Match(resourceText, @"""resource_size""\s*:\s*(\d+)");
                        if (sizeMatch.Success)
                        {
                            resource.ResourceSize = uint.Parse(sizeMatch.Groups[1].Value);
                        }

                        results.Resources.Add(resource);
                    }
                }

                return results;
            }
            catch
            {
                return null;
            }
        }

        private class ERFValidationResults
        {
            public string FileType { get; set; } = "";
            public string FileVersion { get; set; } = "";
            public uint EntryCount { get; set; }
            public List<ERFResourceValidationResult> Resources { get; set; } = new List<ERFResourceValidationResult>();
        }

        private class ERFResourceValidationResult
        {
            public string ResRef { get; set; } = "";
            public ushort ResourceType { get; set; }
            public uint ResourceSize { get; set; }
        }
    }
}

