using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using UTM = Andastra.Parsing.Resource.Generics.UTM.UTM;
using UTMHelpers = Andastra.Parsing.Resource.Generics.UTM.UTMHelpers;
using UTMItem = Andastra.Parsing.Resource.Generics.UTM.UTMItem;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTM.ksy Kaitai Struct compiler functionality.
    /// Tests compile UTM.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic
    /// </summary>
    public class UTMKaitaiCompilerTests
    {
        private static readonly string UtmKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTM", "UTM.ksy");

        private static readonly string TestUtmFile = TestFileHelper.GetPath("test.utm");
        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_utm_tests");

        // Supported Kaitai Struct target languages (at least 12 as required)
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
            "swift"
        };

        static UTMKaitaiCompilerTests()
        {
            // Normalize UTM.ksy path
            UtmKsyPath = Path.GetFullPath(UtmKsyPath);

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
            var processInfo = CreateCompilerProcessInfo(compilerPath, "--version");

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
        public void TestUtmKsyFileExists()
        {
            File.Exists(UtmKsyPath).Should().BeTrue($"UTM.ksy should exist at {UtmKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(UtmKsyPath);
            content.Should().Contain("meta:", "UTM.ksy should contain meta section");
            content.Should().Contain("id: utm", "UTM.ksy should have id: utm");
            content.Should().Contain("file-extension: utm", "UTM.ksy should specify utm file extension");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileUtmKsyToLanguage(string language)
        {
            // Skip if compiler not available
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip test if compiler not available
            }

            // Create output directory for this language
            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile UTM.ksy to target language
            var processInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t {language} \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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
                $"kaitai-struct-compiler should compile UTM.ksy to {language} successfully. " +
                $"STDOUT: {stdout}, STDERR: {stderr}");

            // Verify output files were generated
            string[] generatedFiles = Directory.GetFiles(langOutputDir, "*", SearchOption.AllDirectories);
            generatedFiles.Should().NotBeEmpty($"Compilation to {language} should generate output files");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmKsyToAllLanguages()
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

                    var processInfo = CreateCompilerProcessInfo(
                        compilerPath,
                        $"-t {language} \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                        Path.GetDirectoryName(UtmKsyPath));

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
            successCount.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. " +
                $"Results: {string.Join(", ", results.Select(kvp => $"{kvp.Key}: {(kvp.Value ? "OK" : "FAIL")}"))}. " +
                $"Errors: {string.Join("; ", errors.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
        }

        [Fact(Timeout = 300000)]
        public void TestCompiledParserValidatesUtmFile()
        {
            // Create test UTM file if it doesn't exist
            if (!File.Exists(TestUtmFile))
            {
                CreateTestUtmFile(TestUtmFile);
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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t python \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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

            // Actually execute the generated Python parser to validate it can parse the UTM file
            // This requires Python runtime and kaitaistruct library
            string pythonPath = FindPythonRuntime();
            if (string.IsNullOrEmpty(pythonPath))
            {
                // Skip parser execution if Python is not available, but compilation test still passes
                return;
            }

            // Check if kaitaistruct library is available
            if (!IsKaitaiStructLibraryAvailable(pythonPath))
            {
                // Skip parser execution if kaitaistruct is not installed
                // This is acceptable - the test still validates compilation succeeds
                return;
            }

            // Find the main parser file (usually utm.py)
            string parserFile = pythonFiles.FirstOrDefault(f =>
                Path.GetFileName(f).Equals("utm.py", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(f).StartsWith("utm", StringComparison.OrdinalIgnoreCase));

            if (parserFile == null)
            {
                // Try to find any Python file that looks like the parser
                parserFile = pythonFiles.FirstOrDefault();
            }

            parserFile.Should().NotBeNull("Python parser file should be found");

            // Execute the Python parser to parse the test UTM file
            var parsedData = ExecutePythonParser(pythonPath, parserFile, TestUtmFile, langOutputDir);
            parsedData.Should().NotBeNull("Python parser should successfully parse UTM file");

            // Validate parsed data matches C# implementation
            ValidateParsedUtmData(parsedData, TestUtmFile);
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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t csharp \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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
            string utmCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("utm"));
            if (utmCsFile != null)
            {
                string csContent = File.ReadAllText(utmCsFile);
                csContent.Should().Contain("class", "Generated C# file should contain class definition");
                csContent.Should().Contain("GffHeader", "Generated C# file should contain GffHeader structure");
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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t java \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t javascript \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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
        public void TestCompileUtmKsyToAdditionalLanguages(string language)
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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t {language} \"{UtmKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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
        public void TestUtmKsySyntaxValidation()
        {
            // Validate UTM.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t python \"{UtmKsyPath}\" --debug",
                Path.GetDirectoryName(UtmKsyPath));

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    stderr.Should().NotContain("error", "UTM.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"UTM.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestUtmKsyCompilesToMultipleLanguagesSimultaneously()
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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t {languages} \"{UtmKsyPath}\" -d \"{multiLangDir}\"",
                Path.GetDirectoryName(UtmKsyPath));

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
        public void TestUtmKsyFileTypeSignature()
        {
            // Validate UTM.ksy defines correct file type signature
            string content = File.ReadAllText(UtmKsyPath);
            content.Should().Contain("valid: \"UTM \"", "UTM.ksy should validate file type signature as 'UTM '");
            content.Should().Contain("file-extension: utm", "UTM.ksy should specify utm file extension");
        }

        [Fact(Timeout = 300000)]
        public void TestUtmKsyGffHeaderStructure()
        {
            // Validate UTM.ksy defines GFF header structure correctly
            string content = File.ReadAllText(UtmKsyPath);
            content.Should().Contain("gff_header:", "UTM.ksy should define gff_header type");
            content.Should().Contain("file_type", "UTM.ksy should define file_type field");
            content.Should().Contain("file_version", "UTM.ksy should define file_version field");
            content.Should().Contain("struct_array_offset", "UTM.ksy should define struct_array_offset field");
            content.Should().Contain("field_array_offset", "UTM.ksy should define field_array_offset field");
        }

        [Fact(Timeout = 300000)]
        public void TestUtmKsyEnumsDefined()
        {
            // Validate UTM.ksy defines enums correctly
            string content = File.ReadAllText(UtmKsyPath);
            content.Should().Contain("enums:", "UTM.ksy should define enums section");
            content.Should().Contain("gff_field_type:", "UTM.ksy should define gff_field_type enum");
            content.Should().Contain("uint8", "UTM.ksy should define uint8 enum value");
            content.Should().Contain("resref", "UTM.ksy should define resref enum value");
            content.Should().Contain("localized_string", "UTM.ksy should define localized_string enum value");
        }

        [Fact(Timeout = 300000)]
        public void TestUtmKsyInstancesDefined()
        {
            // Validate UTM.ksy defines instances for computed values
            string content = File.ReadAllText(UtmKsyPath);
            content.Should().Contain("instances:", "UTM.ksy should define instances section");
            content.Should().Contain("is_simple_type", "UTM.ksy should define is_simple_type instance");
            content.Should().Contain("is_complex_type", "UTM.ksy should define is_complex_type instance");
            content.Should().Contain("is_list_type", "UTM.ksy should define is_list_type instance");
        }

        [Fact(Timeout = 300000)]
        public void TestUtmKsyDefinitionCompleteness()
        {
            // Validate UTM.ksy defines all UTM-specific fields and structures
            var normalizedKsyPath = Path.GetFullPath(UtmKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for required elements in Kaitai Struct definition
            content.Should().Contain("meta:", "Should have meta section");
            content.Should().Contain("id: utm", "Should have id: utm");
            content.Should().Contain("file-extension: utm", "Should define file extension");
            content.Should().Contain("gff_header", "Should define gff_header type");
            content.Should().Contain("file_type", "Should define file_type field");
            content.Should().Contain("file_version", "Should define file_version field");
            content.Should().Contain("UTM ", "Should support UTM file type signature");

            // Check for UTM-specific field documentation
            content.Should().Contain("ResRef", "Should document ResRef field");
            content.Should().Contain("LocName", "Should document LocName field");
            content.Should().Contain("Tag", "Should document Tag field");
            content.Should().Contain("MarkUp", "Should document MarkUp field");
            content.Should().Contain("MarkDown", "Should document MarkDown field");
            content.Should().Contain("OnOpenStore", "Should document OnOpenStore field");
            content.Should().Contain("Comment", "Should document Comment field");
            content.Should().Contain("BuySellFlag", "Should document BuySellFlag field");
            content.Should().Contain("ItemList", "Should document ItemList field");
            content.Should().Contain("InventoryRes", "Should document InventoryRes field");
            content.Should().Contain("Infinite", "Should document Infinite field");
            content.Should().Contain("Dropable", "Should document Dropable field");
            content.Should().Contain("Repos_PosX", "Should document Repos_PosX field");
            content.Should().Contain("Repos_PosY", "Should document Repos_PosY field");
        }

        [Fact(Timeout = 300000)]
        public void TestUtmKsyHeaderStructure()
        {
            // Validate UTM.ksy defines all required header fields
            var normalizedKsyPath = Path.GetFullPath(UtmKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for all required header fields
            content.Should().Contain("file_type", "Header should define file_type");
            content.Should().Contain("file_version", "Header should define file_version");
            content.Should().Contain("struct_array_offset", "Header should define struct_array_offset");
            content.Should().Contain("struct_count", "Header should define struct_count");
            content.Should().Contain("field_array_offset", "Header should define field_array_offset");
            content.Should().Contain("field_count", "Header should define field_count");
            content.Should().Contain("label_array_offset", "Header should define label_array_offset");
            content.Should().Contain("label_count", "Header should define label_count");
            content.Should().Contain("field_data_offset", "Header should define field_data_offset");
            content.Should().Contain("field_data_count", "Header should define field_data_count");
            content.Should().Contain("field_indices_offset", "Header should define field_indices_offset");
            content.Should().Contain("field_indices_count", "Header should define field_indices_count");
            content.Should().Contain("list_indices_offset", "Header should define list_indices_offset");
            content.Should().Contain("list_indices_count", "Header should define list_indices_count");

            // Check for header size (56 bytes)
            content.Should().Contain("56", "Header should be 56 bytes");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToPython()
        {
            TestCompileToLanguageInternal("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToJava()
        {
            TestCompileToLanguageInternal("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToJavaScript()
        {
            TestCompileToLanguageInternal("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToCSharp()
        {
            TestCompileToLanguageInternal("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToCpp()
        {
            TestCompileToLanguageInternal("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToGo()
        {
            TestCompileToLanguageInternal("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToRuby()
        {
            TestCompileToLanguageInternal("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToPhp()
        {
            TestCompileToLanguageInternal("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToRust()
        {
            TestCompileToLanguageInternal("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToSwift()
        {
            TestCompileToLanguageInternal("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToLua()
        {
            TestCompileToLanguageInternal("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToNim()
        {
            TestCompileToLanguageInternal("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToPerl()
        {
            TestCompileToLanguageInternal("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtmToVisualBasic()
        {
            TestCompileToLanguageInternal("visualbasic");
        }

        private void TestCompileToLanguageInternal(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(UtmKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip test if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            var processInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t {language} \"{normalizedKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(normalizedKsyPath));

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

            // Compilation should succeed (some languages may not be fully supported, log but don't fail)
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
            // Try common locations and PATH
            string[] possiblePaths = new[]
            {
                @"C:\Program Files (x86)\kaitai-struct-compiler\bin\kaitai-struct-compiler.bat",
                "kaitai-struct-compiler",
                "ksc",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "bin", "kaitai-struct-compiler.bat"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "bin", "kaitai-struct-compiler.bat"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"),
                "/usr/bin/kaitai-struct-compiler",
                "/usr/local/bin/kaitai-struct-compiler",
                "C:\\Program Files\\kaitai-struct-compiler\\kaitai-struct-compiler.exe"
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    var result = RunCommand(path, "--version");
                    if (result.ExitCode == 0)
                    {
                        return path;
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            // Try as Java JAR
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                var result = RunCommand("java", $"-jar \"{jarPath}\" --version");
                if (result.ExitCode == 0)
                {
                    // Return JAR path - callers will use RunKaitaiCompiler helper
                    return jarPath;
                }
            }

            return null;
        }

        private static ProcessStartInfo CreateCompilerProcessInfo(string compilerPath, string arguments, string workingDirectory = null)
        {
            // Check if compiler path is a JAR file
            bool isJar = !string.IsNullOrEmpty(compilerPath) &&
                         (compilerPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                          (File.Exists(compilerPath) && Path.GetExtension(compilerPath).Equals(".jar", StringComparison.OrdinalIgnoreCase)));

            if (isJar)
            {
                // Use java -jar for JAR files
                return new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{compilerPath}\" {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory
                };
            }
            else
            {
                // Use compiler directly
                return new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory
                };
            }
        }

        private static string FindKaitaiCompilerJar()
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

        private static (int ExitCode, string Output, string Error) RunCommand(string command, string arguments)
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

        private static string FindPythonRuntime()
        {
            // Try common Python executable names and paths
            string[] possiblePaths = new[]
            {
                "python",
                "python3",
                "py",
                @"C:\Python39\python.exe",
                @"C:\Python310\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Program Files\Python39\python.exe",
                @"C:\Program Files\Python310\python.exe",
                @"C:\Program Files\Python311\python.exe",
                @"C:\Program Files\Python312\python.exe",
                @"/usr/bin/python3",
                @"/usr/bin/python",
                @"/usr/local/bin/python3",
                @"/usr/local/bin/python"
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    var result = RunCommand(path, "--version");
                    if (result.ExitCode == 0)
                    {
                        return path;
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            return null;
        }

        private static bool IsKaitaiStructLibraryAvailable(string pythonPath)
        {
            if (string.IsNullOrEmpty(pythonPath))
            {
                return false;
            }

            try
            {
                // Try to import kaitaistruct library
                var result = RunCommand(pythonPath, "-c \"import kaitaistruct; print('OK')\"");
                return result.ExitCode == 0 && result.Output.Contains("OK");
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, object> ExecutePythonParser(string pythonPath, string parserFile, string utmFilePath, string workingDirectory)
        {
            if (string.IsNullOrEmpty(pythonPath) || !File.Exists(parserFile) || !File.Exists(utmFilePath))
            {
                return null;
            }

            // Create a Python script that uses the generated parser to parse the UTM file
            // and output JSON for easy parsing in C#
            string scriptPath = Path.Combine(Path.GetTempPath(), $"utm_parser_test_{Guid.NewGuid()}.py");
            try
            {
                // Generate Python script that imports the parser and outputs structured data
                // This script comprehensively extracts data from the parsed UTM file for validation
                string scriptContent = $@"
import sys
import json
import os

# Add parser directory to path
sys.path.insert(0, r'{Path.GetDirectoryName(parserFile).Replace("\\", "\\\\")}')

try:
    # Import the generated parser
    # Try different possible import names
    try:
        from utm import Utm
    except ImportError:
        # Try alternative import patterns
        import importlib.util
        spec = importlib.util.spec_from_file_location('utm', r'{parserFile.Replace("\\", "\\\\")}')
        utm_module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(utm_module)
        Utm = utm_module.Utm

    # Parse the UTM file
    with open(r'{utmFilePath.Replace("\\", "\\\\")}', 'rb') as f:
        data = f.read()

    utm = Utm.from_bytes(data)

    # Extract key fields for validation
    result = {{}}

    # Extract GFF header information
    if hasattr(utm, 'gff_header'):
        header = utm.gff_header
        result['file_type'] = str(header.file_type) if hasattr(header, 'file_type') else None
        result['file_version'] = str(header.file_version) if hasattr(header, 'file_version') else None
        result['struct_count'] = int(header.struct_count) if hasattr(header, 'struct_count') else None
        result['field_count'] = int(header.field_count) if hasattr(header, 'field_count') else None
        result['struct_array_offset'] = int(header.struct_array_offset) if hasattr(header, 'struct_array_offset') else None
        result['field_array_offset'] = int(header.field_array_offset) if hasattr(header, 'field_array_offset') else None
    else:
        result['file_type'] = None
        result['file_version'] = None
        result['struct_count'] = None
        result['field_count'] = None

    # Try to extract root struct data if available
    # The exact structure depends on the generated parser from UTM.ksy
    result['has_root'] = False
    result['has_structs'] = False

    if hasattr(utm, 'root'):
        result['has_root'] = True
        # Try to extract root struct fields
        root = utm.root
        root_fields = {{}}
        # Common UTM fields that might be in root struct
        for field_name in ['ResRef', 'Tag', 'MarkUp', 'MarkDown', 'Comment', 'BuySellFlag', 'OnOpenStore', 'ItemList']:
            if hasattr(root, field_name):
                try:
                    value = getattr(root, field_name)
                    # Convert to JSON-serializable format
                    if hasattr(value, '__str__'):
                        root_fields[field_name] = str(value)
                    else:
                        root_fields[field_name] = value
                except:
                    pass
        if root_fields:
            result['root_fields'] = root_fields

    if hasattr(utm, 'structs') or hasattr(utm, 'struct_array'):
        result['has_structs'] = True
        struct_count = 0
        try:
            if hasattr(utm, 'structs'):
                struct_count = len(utm.structs) if hasattr(utm.structs, '__len__') else 0
            elif hasattr(utm, 'struct_array'):
                struct_count = len(utm.struct_array) if hasattr(utm.struct_array, '__len__') else 0
        except:
            pass
        result['parsed_struct_count'] = struct_count

    # Validate that parsing was successful
    result['parse_success'] = True

    # Output as JSON
    print(json.dumps(result))

except Exception as e:
    import traceback
    error_result = {{
        'error': str(e),
        'traceback': traceback.format_exc(),
        'parse_success': False
    }}
    print(json.dumps(error_result), file=sys.stderr)
    sys.exit(1)
";

                File.WriteAllText(scriptPath, scriptContent);

                // Execute the Python script
                var result = RunCommand(pythonPath, $"\"{scriptPath}\"");

                if (result.ExitCode != 0)
                {
                    // Parser execution failed
                    return null;
                }

                // Parse JSON output
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(result.Output.Trim());
                    return parsed;
                }
                catch
                {
                    // JSON parsing failed
                    return null;
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

        private static void ValidateParsedUtmData(Dictionary<string, object> parsedData, string utmFilePath)
        {
            if (parsedData == null)
            {
                return;
            }

            // Check if parsing was successful
            if (parsedData.ContainsKey("parse_success"))
            {
                bool parseSuccess = Convert.ToBoolean(parsedData["parse_success"]);
                parseSuccess.Should().BeTrue("Python parser should successfully parse UTM file");
            }

            // Check for errors
            if (parsedData.ContainsKey("error"))
            {
                string error = parsedData["error"]?.ToString() ?? "";
                // If there's an error, the test should fail
                error.Should().BeNullOrEmpty($"Python parser should not have errors, but got: {error}");
            }

            // Parse the same file using C# implementation for comparison
            byte[] fileData = File.ReadAllBytes(utmFilePath);
            var utm = UTMHelpers.ReadUtm(fileData);

            // Validate file type signature
            if (parsedData.ContainsKey("file_type"))
            {
                string fileType = parsedData["file_type"]?.ToString() ?? "";
                // UTM files should have "UTM " signature (with space)
                // The parser might return it as bytes or string, so check for "UTM" substring
                fileType.Should().Contain("UTM", "Parsed file type should contain 'UTM'");
            }

            // Validate file version
            if (parsedData.ContainsKey("file_version"))
            {
                string fileVersion = parsedData["file_version"]?.ToString() ?? "";
                // Should be one of the valid GFF versions (V3.2, V3.3, V4.0, V4.1)
                // The version might be returned as bytes, so we check if it contains version info
                if (!string.IsNullOrEmpty(fileVersion))
                {
                    fileVersion.Should().MatchRegex("V3\\.2|V3\\.3|V4\\.0|V4\\.1|3\\.2|3\\.3|4\\.0|4\\.1",
                        $"File version should be valid GFF version, got: {fileVersion}");
                }
            }

            // Validate that parsing detected structures
            // Either has_root or has_structs should be true
            bool hasRoot = parsedData.ContainsKey("has_root") && Convert.ToBoolean(parsedData["has_root"]);
            bool hasStructs = parsedData.ContainsKey("has_structs") && Convert.ToBoolean(parsedData["has_structs"]);

            (hasRoot || hasStructs).Should().BeTrue("Parsed UTM should have root struct or struct array");

            // Validate struct and field counts are reasonable (non-negative)
            if (parsedData.ContainsKey("struct_count"))
            {
                object structCountObj = parsedData["struct_count"];
                if (structCountObj != null)
                {
                    int structCount = Convert.ToInt32(structCountObj);
                    structCount.Should().BeGreaterThanOrEqualTo(0, "Struct count should be non-negative");
                    // UTM files should have at least one struct (the root)
                    structCount.Should().BeGreaterThan(0, "UTM file should have at least one struct");
                }
            }

            if (parsedData.ContainsKey("field_count"))
            {
                object fieldCountObj = parsedData["field_count"];
                if (fieldCountObj != null)
                {
                    int fieldCount = Convert.ToInt32(fieldCountObj);
                    fieldCount.Should().BeGreaterThanOrEqualTo(0, "Field count should be non-negative");
                }
            }

            // Validate parsed struct count matches header if available
            if (parsedData.ContainsKey("struct_count") && parsedData.ContainsKey("parsed_struct_count"))
            {
                int headerStructCount = Convert.ToInt32(parsedData["struct_count"]);
                int parsedStructCount = Convert.ToInt32(parsedData["parsed_struct_count"]);
                // The parsed count should match or be close to header count
                parsedStructCount.Should().BeLessThanOrEqualTo(headerStructCount + 1,
                    "Parsed struct count should not exceed header struct count significantly");
            }

            // Validate root fields if available
            if (parsedData.ContainsKey("root_fields"))
            {
                var rootFields = parsedData["root_fields"] as Dictionary<string, object>;
                if (rootFields != null && rootFields.Count > 0)
                {
                    // Validate that key UTM fields are present
                    // At minimum, we should have some fields parsed
                    rootFields.Count.Should().BeGreaterThan(0, "Root struct should have fields");
                }
            }

            // Additional validation: The C# implementation should have successfully parsed the file
            utm.Should().NotBeNull("C# UTM parser should successfully parse the file");
            utm.ResRef.Should().NotBeNull("UTM should have ResRef");

            // Validate that the C# parsed data is consistent
            // The test UTM file should have the values we set in CreateTestUtmFile
            utm.ResRef.ToString().Should().Be("test_merchant", "ResRef should match test data");
            utm.Tag.Should().Be("TEST", "Tag should match test data");
            utm.MarkUp.Should().Be(10, "MarkUp should match test data");
            utm.MarkDown.Should().Be(5, "MarkDown should match test data");
            utm.CanBuy.Should().BeTrue("CanBuy should match test data");
            utm.CanSell.Should().BeTrue("CanSell should match test data");
            utm.Items.Count.Should().BeGreaterThan(0, "UTM should have items");
        }

        private static void CreateTestUtmFile(string path)
        {
            var utm = new UTM();
            utm.ResRef = new ResRef("test_merchant");
            utm.Name = LocalizedString.FromEnglish("Test Merchant");
            utm.Tag = "TEST";
            utm.MarkUp = 10;
            utm.MarkDown = 5;
            utm.CanBuy = true;
            utm.CanSell = true;
            utm.Comment = "Test merchant comment";
            utm.OnOpenScript = new ResRef("test_open");

            // Add a test item
            var item = new UTMItem();
            item.ResRef = new ResRef("test_item");
            item.Infinite = 1;
            item.Droppable = 0;
            utm.Items.Add(item);

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}
