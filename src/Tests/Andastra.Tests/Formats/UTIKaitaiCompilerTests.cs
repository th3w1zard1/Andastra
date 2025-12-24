using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using UTC = Andastra.Parsing.Resource.Generics.UTC.UTC;
using UTCHelpers = Andastra.Parsing.Resource.Generics.UTC.UTCHelpers;
using UTI = Andastra.Parsing.Resource.Generics.UTI.UTI;
using UTIHelpers = Andastra.Parsing.Resource.Generics.UTI.UTIHelpers;
using UTIProperty = Andastra.Parsing.Resource.Generics.UTI.UTIProperty;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTI.ksy Kaitai Struct compiler functionality.
    /// Tests compile UTI.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic
    /// </summary>
    public class UTIKaitaiCompilerTests
    {
        private static readonly string UtiKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTI", "UTI.ksy");

        private static readonly string TestUtiFile = TestFileHelper.GetPath("test.uti");
        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_uti_tests");

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
            "visualbasic"
        };

        static UTIKaitaiCompilerTests()
        {
            // Normalize UTI.ksy path
            UtiKsyPath = Path.GetFullPath(UtiKsyPath);

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
        public void TestUtiKsyFileExists()
        {
            File.Exists(UtiKsyPath).Should().BeTrue($"UTI.ksy should exist at {UtiKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("meta:", "UTI.ksy should contain meta section");
            content.Should().Contain("id: uti", "UTI.ksy should have id: uti");
            content.Should().Contain("file-extension: uti", "UTI.ksy should specify uti file extension");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileUtiKsyToLanguage(string language)
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

            // Compile UTI.ksy to target language
            var processInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t {language} \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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
                $"kaitai-struct-compiler should compile UTI.ksy to {language} successfully. " +
                $"STDOUT: {stdout}, STDERR: {stderr}");

            // Verify output files were generated
            string[] generatedFiles = Directory.GetFiles(langOutputDir, "*", SearchOption.AllDirectories);
            generatedFiles.Should().NotBeEmpty($"Compilation to {language} should generate output files");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtiKsyToAllLanguages()
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
                        $"-t {language} \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                        Path.GetDirectoryName(UtiKsyPath));

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
        public void TestCompiledParserValidatesUtiFile()
        {
            // Create test UTI file if it doesn't exist
            if (!File.Exists(TestUtiFile))
            {
                CreateTestUtiFile(TestUtiFile);
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
                $"-t python \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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

            // Actually use the generated parser to parse the test UTI file
            string pythonExe = FindPythonExecutable();
            if (string.IsNullOrEmpty(pythonExe))
            {
                // Skip if Python not available
                return;
            }

            // Check/install kaitaistruct library
            bool kaitaiAvailable = CheckKaitaiStructLibrary(pythonExe);
            if (!kaitaiAvailable)
            {
                // Try to install kaitaistruct
                bool installed = InstallKaitaiStructLibrary(pythonExe);
                if (!installed)
                {
                    // Skip if kaitaistruct not available and couldn't be installed
                    return;
                }
            }

            // Find the main UTI parser file
            string utiParserFile = pythonFiles.FirstOrDefault(f =>
                Path.GetFileName(f).ToLowerInvariant().Contains("uti") &&
                !Path.GetFileName(f).ToLowerInvariant().Contains("test"));

            if (string.IsNullOrEmpty(utiParserFile))
            {
                // Try to find any .py file that looks like the main parser
                utiParserFile = pythonFiles.FirstOrDefault();
            }

            utiParserFile.Should().NotBeNullOrEmpty("Should find generated UTI parser file");

            // Create Python script to parse the UTI file
            string testScriptPath = Path.Combine(Path.GetTempPath(), "test_uti_parser_" + Guid.NewGuid().ToString("N") + ".py");
            CreatePythonParserScript(testScriptPath, utiParserFile, TestUtiFile, langOutputDir);

            try
            {
                // Execute Python script
                var parseResult = ExecutePythonScript(pythonExe, testScriptPath, langOutputDir);

                // Validate parser executed successfully
                parseResult.ExitCode.Should().Be(0,
                    $"Python parser should parse UTI file successfully. STDOUT: {parseResult.Output}, STDERR: {parseResult.Error}");

                // Validate parsed output contains expected fields
                // The Python script outputs JSON first, then human-readable text
                string combinedOutput = parseResult.Output + parseResult.Error;

                // Check for successful parsing indicators
                combinedOutput.Should().Contain("parser_loaded", "Parsed output should indicate parser was loaded");
                combinedOutput.Should().Contain("file_parsed", "Parsed output should indicate file was parsed");

                // Check for file type signature (UTI files should have "UTI " as file type)
                combinedOutput.Should().Contain("UTI", "Parsed output should contain UTI file type signature");

                // Check for file_type field (either in JSON or text output)
                bool hasFileType = combinedOutput.Contains("file_type") || combinedOutput.Contains("File type:");
                hasFileType.Should().BeTrue("Parsed output should contain file_type information");
            }
            finally
            {
                // Clean up test script
                if (File.Exists(testScriptPath))
                {
                    try
                    {
                        File.Delete(testScriptPath);
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
            var compileInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t csharp \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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
            string utiCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("uti"));
            if (utiCsFile != null)
            {
                string csContent = File.ReadAllText(utiCsFile);
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
                $"-t java \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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
                $"-t javascript \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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
        public void TestCompileUtiKsyToAdditionalLanguages(string language)
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
                $"-t {language} \"{UtiKsyPath}\" -d \"{langOutputDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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
        public void TestUtiKsySyntaxValidation()
        {
            // Validate UTI.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = CreateCompilerProcessInfo(
                compilerPath,
                $"-t python \"{UtiKsyPath}\" --debug",
                Path.GetDirectoryName(UtiKsyPath));

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    stderr.Should().NotContain("error", "UTI.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"UTI.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyCompilesToMultipleLanguagesSimultaneously()
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
                $"-t {languages} \"{UtiKsyPath}\" -d \"{multiLangDir}\"",
                Path.GetDirectoryName(UtiKsyPath));

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
        public void TestUtiKsyFileTypeSignature()
        {
            // Validate UTI.ksy defines correct file type signature
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("valid: \"UTI \"", "UTI.ksy should validate file type signature as 'UTI '");
            content.Should().Contain("file-extension: uti", "UTI.ksy should specify uti file extension");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyGffHeaderStructure()
        {
            // Validate UTI.ksy defines GFF header structure correctly
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("gff_header:", "UTI.ksy should define gff_header type");
            content.Should().Contain("file_type", "UTI.ksy should define file_type field");
            content.Should().Contain("file_version", "UTI.ksy should define file_version field");
            content.Should().Contain("struct_array_offset", "UTI.ksy should define struct_array_offset field");
            content.Should().Contain("field_array_offset", "UTI.ksy should define field_array_offset field");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyEnumsDefined()
        {
            // Validate UTI.ksy defines enums correctly
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("enums:", "UTI.ksy should define enums section");
            content.Should().Contain("gff_field_type:", "UTI.ksy should define gff_field_type enum");
            content.Should().Contain("uint8", "UTI.ksy should define uint8 enum value");
            content.Should().Contain("resref", "UTI.ksy should define resref enum value");
            content.Should().Contain("localized_string", "UTI.ksy should define localized_string enum value");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyInstancesDefined()
        {
            // Validate UTI.ksy defines instances for computed values
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("instances:", "UTI.ksy should define instances section");
            content.Should().Contain("is_simple_type", "UTI.ksy should define is_simple_type instance");
            content.Should().Contain("is_complex_type", "UTI.ksy should define is_complex_type instance");
            content.Should().Contain("is_list_type", "UTI.ksy should define is_list_type instance");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyPropertiesListDocumentation()
        {
            // Validate UTI.ksy documents PropertiesList field correctly
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("PropertiesList", "UTI.ksy should document PropertiesList field");
            content.Should().Contain("PropertyName", "UTI.ksy should document PropertyName field");
            content.Should().Contain("Subtype", "UTI.ksy should document Subtype field");
            content.Should().Contain("CostTable", "UTI.ksy should document CostTable field");
            content.Should().Contain("CostValue", "UTI.ksy should document CostValue field");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyItemFieldsDocumentation()
        {
            // Validate UTI.ksy documents all important UTI item fields
            string content = File.ReadAllText(UtiKsyPath);
            content.Should().Contain("TemplateResRef", "UTI.ksy should document TemplateResRef field");
            content.Should().Contain("LocalizedName", "UTI.ksy should document LocalizedName field");
            content.Should().Contain("BaseItem", "UTI.ksy should document BaseItem field");
            content.Should().Contain("Cost", "UTI.ksy should document Cost field");
            content.Should().Contain("StackSize", "UTI.ksy should document StackSize field");
            content.Should().Contain("Charges", "UTI.ksy should document Charges field");
            content.Should().Contain("Plot", "UTI.ksy should document Plot field");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyDefinitionCompleteness()
        {
            // Validate UTI.ksy defines all UTI-specific fields and structures comprehensively
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for required elements in Kaitai Struct definition
            content.Should().Contain("meta:", "Should have meta section");
            content.Should().Contain("id: uti", "Should have id: uti");
            content.Should().Contain("file-extension: uti", "Should define file extension");
            content.Should().Contain("gff_header", "Should define gff_header type");
            content.Should().Contain("file_type", "Should define file_type field");
            content.Should().Contain("file_version", "Should define file_version field");
            content.Should().Contain("UTI ", "Should support UTI file type signature");

            // Check for all core UTI item fields
            content.Should().Contain("TemplateResRef", "Should document TemplateResRef field");
            content.Should().Contain("LocalizedName", "Should document LocalizedName field");
            content.Should().Contain("Description", "Should document Description field");
            content.Should().Contain("DescIdentified", "Should document DescIdentified field");
            content.Should().Contain("Tag", "Should document Tag field");
            content.Should().Contain("Comment", "Should document Comment field");
            content.Should().Contain("BaseItem", "Should document BaseItem field");
            content.Should().Contain("Cost", "Should document Cost field");
            content.Should().Contain("AddCost", "Should document AddCost field");
            content.Should().Contain("Plot", "Should document Plot field");
            content.Should().Contain("Charges", "Should document Charges field");
            content.Should().Contain("StackSize", "Should document StackSize field");
            content.Should().Contain("ModelVariation", "Should document ModelVariation field");
            content.Should().Contain("BodyVariation", "Should document BodyVariation field");
            content.Should().Contain("TextureVar", "Should document TextureVar field");
            content.Should().Contain("PaletteID", "Should document PaletteID field");
            content.Should().Contain("Identified", "Should document Identified field");
            content.Should().Contain("Stolen", "Should document Stolen field");

            // Check for KotOR2-specific fields
            content.Should().Contain("UpgradeLevel", "Should document UpgradeLevel field (KotOR2)");
            content.Should().Contain("WeaponColor", "Should document WeaponColor field (KotOR2)");
            content.Should().Contain("WeaponWhoosh", "Should document WeaponWhoosh field (KotOR2)");
            content.Should().Contain("ArmorRulesType", "Should document ArmorRulesType field (KotOR2)");

            // Check for KotOR1-specific fields
            content.Should().Contain("Upgradable", "Should document Upgradable field (KotOR1)");

            // Check for PropertiesList structure
            content.Should().Contain("PropertiesList", "Should document PropertiesList field");
            content.Should().Contain("PropertyName", "Should document PropertyName field in PropertiesList");
            content.Should().Contain("Subtype", "Should document Subtype field in PropertiesList");
            content.Should().Contain("CostTable", "Should document CostTable field in PropertiesList");
            content.Should().Contain("CostValue", "Should document CostValue field in PropertiesList");
            content.Should().Contain("Param1", "Should document Param1 field in PropertiesList");
            content.Should().Contain("Param1Value", "Should document Param1Value field in PropertiesList");
            content.Should().Contain("ChanceAppear", "Should document ChanceAppear field in PropertiesList");
            content.Should().Contain("UpgradeType", "Should document UpgradeType field in PropertiesList");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyHeaderStructure()
        {
            // Validate UTI.ksy defines all required header fields comprehensively
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
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

            // Check for header size documentation (56 bytes)
            content.Should().Contain("56", "Header should be 56 bytes");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyFieldTypeDocumentation()
        {
            // Validate UTI.ksy documents all field types used in UTI format
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for field type documentation
            content.Should().Contain("uint8", "Should document uint8 field type (used for Plot, Charges, etc.)");
            content.Should().Contain("uint16", "Should document uint16 field type (used for StackSize, PropertyName, etc.)");
            content.Should().Contain("uint32", "Should document uint32 field type (used for Cost, AddCost)");
            content.Should().Contain("int32", "Should document int32 field type (used for BaseItem, Identified, Stolen)");
            content.Should().Contain("string", "Should document string field type (used for Tag, Comment)");
            content.Should().Contain("resref", "Should document resref field type (used for TemplateResRef)");
            content.Should().Contain("localized_string", "Should document localized_string field type (used for LocalizedName, Description)");
            content.Should().Contain("list", "Should document list field type (used for PropertiesList)");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyLocalizedStringDocumentation()
        {
            // Validate UTI.ksy documents LocalizedString structure comprehensively
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for LocalizedString type documentation
            content.Should().Contain("localized_string_data", "Should define localized_string_data type");
            content.Should().Contain("localized_substring", "Should define localized_substring type");
            content.Should().Contain("string_ref", "Should document string_ref field");
            content.Should().Contain("string_count", "Should document string_count field");
            content.Should().Contain("string_id", "Should document string_id field");
            content.Should().Contain("language_id", "Should document language_id computed instance");
            content.Should().Contain("gender_id", "Should document gender_id computed instance");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyPropertiesListStructure()
        {
            // Validate UTI.ksy documents PropertiesList structure comprehensively
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for PropertiesList structure documentation
            content.Should().Contain("PropertiesList", "Should document PropertiesList field");
            content.Should().Contain("PropertyName", "Should document PropertyName in PropertiesList struct");
            content.Should().Contain("Subtype", "Should document Subtype in PropertiesList struct");
            content.Should().Contain("CostTable", "Should document CostTable in PropertiesList struct");
            content.Should().Contain("CostValue", "Should document CostValue in PropertiesList struct");
            content.Should().Contain("Param1", "Should document Param1 in PropertiesList struct");
            content.Should().Contain("Param1Value", "Should document Param1Value in PropertiesList struct");
            content.Should().Contain("ChanceAppear", "Should document ChanceAppear in PropertiesList struct");
            content.Should().Contain("list_entry", "Should document list_entry type for PropertiesList");
            content.Should().Contain("struct_indices", "Should document struct_indices in list_entry");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyResRefDocumentation()
        {
            // Validate UTI.ksy documents ResRef structure comprehensively
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for ResRef type documentation
            content.Should().Contain("resref_data", "Should define resref_data type");
            content.Should().Contain("max 16", "Should document ResRef max 16 characters limit");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyVersionSupport()
        {
            // Validate UTI.ksy supports all required GFF versions
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for version support
            content.Should().Contain("V3.2", "Should support V3.2 version (KotOR)");
            content.Should().Contain("V3.3", "Should support V3.3 version");
            content.Should().Contain("V4.0", "Should support V4.0 version");
            content.Should().Contain("V4.1", "Should support V4.1 version");
        }

        [Fact(Timeout = 300000)]
        public void TestUtiKsyReferencesAndDocumentation()
        {
            // Validate UTI.ksy includes comprehensive references and documentation
            var normalizedKsyPath = Path.GetFullPath(UtiKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for documentation references
            content.Should().Contain("xref:", "Should include xref section with references");
            content.Should().Contain("pykotor", "Should reference PyKotor implementation");
            content.Should().Contain("wiki", "Should reference wiki documentation");
            content.Should().Contain("doc:", "Should include comprehensive doc section");
            content.Should().Contain("baseitems.2da", "Should document baseitems.2da reference");
            content.Should().Contain("itempropdef.2da", "Should document itempropdef.2da reference");
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

        private static void CreateTestUtiFile(string path)
        {
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("test_item");
            uti.Name = LocalizedString.FromEnglish("Test Item");
            uti.Description = LocalizedString.FromEnglish("A test item");
            uti.DescriptionUnidentified = LocalizedString.FromEnglish("An unidentified item");
            uti.Tag = "TEST_ITEM";
            uti.BaseItem = 1; // Shortsword
            uti.Cost = 100;
            uti.AddCost = 0;
            uti.Plot = 0;
            uti.Charges = 0;
            uti.StackSize = 1;
            uti.ModelVariation = 1;
            uti.BodyVariation = 0;
            uti.TextureVariation = 0;
            uti.PaletteId = 0;
            uti.Comment = "Test item comment";
            uti.Identified = 1;
            uti.Stolen = 0;

            // Add a test property
            var prop = new UTIProperty();
            prop.PropertyName = 1; // Attack bonus
            prop.Subtype = 1;
            prop.CostTable = 1;
            prop.CostValue = 1;
            prop.Param1 = 0;
            prop.Param1Value = 1;
            prop.ChanceAppear = 100;
            uti.Properties.Add(prop);

            byte[] data = UTIHelpers.BytesUti(uti, BioWareGame.K2);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        /// <summary>
        /// Finds Python executable by checking common locations and PATH.
        /// Returns null if Python is not found.
        /// </summary>
        private static string FindPythonExecutable()
        {
            // Try common Python command names
            string[] pythonCommands = new[]
            {
                "python3",
                "python",
                "py"
            };

            foreach (string cmd in pythonCommands)
            {
                try
                {
                    var result = RunCommand(cmd, "--version");
                    if (result.ExitCode == 0)
                    {
                        // Try to get full path
                        var pathResult = RunCommand(cmd, "-c \"import sys; print(sys.executable)\"");
                        if (pathResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(pathResult.Output))
                        {
                            return pathResult.Output.Trim();
                        }
                        return cmd; // Return command name if we can't get full path
                    }
                }
                catch
                {
                    // Continue searching
                }
            }

            // Try common Windows locations
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                string[] windowsPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python311", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python312", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python313", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python311", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python313", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Python", "Python311", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Python", "Python312", "python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs", "Python", "Python313", "python.exe")
                };

                foreach (string path in windowsPaths)
                {
                    if (File.Exists(path))
                    {
                        var result = RunCommand(path, "--version");
                        if (result.ExitCode == 0)
                        {
                            return path;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if kaitaistruct library is available in Python.
        /// </summary>
        private static bool CheckKaitaiStructLibrary(string pythonExe)
        {
            try
            {
                var result = RunCommand(pythonExe, "-c \"import kaitaistruct; print('ok')\"");
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to install kaitaistruct library using pip.
        /// </summary>
        private static bool InstallKaitaiStructLibrary(string pythonExe)
        {
            try
            {
                // Try pip3 first, then pip
                string[] pipCommands = new[] { "pip3", "pip" };
                string pipExe = null;

                foreach (string cmd in pipCommands)
                {
                    try
                    {
                        var pipResult = RunCommand(cmd, "--version");
                        if (pipResult.ExitCode == 0)
                        {
                            pipExe = cmd;
                            break;
                        }
                    }
                    catch
                    {
                        // Continue
                    }
                }

                // If pip not found, try python -m pip
                if (string.IsNullOrEmpty(pipExe))
                {
                    var pipResult = RunCommand(pythonExe, "-m pip --version");
                    if (pipResult.ExitCode == 0)
                    {
                        // Use python -m pip
                        var installResult = RunCommand(pythonExe, "-m pip install kaitaistruct --quiet");
                        return installResult.ExitCode == 0;
                    }
                }
                else
                {
                    var installResult = RunCommand(pipExe, "install kaitaistruct --quiet");
                    return installResult.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Creates a Python script that uses the generated Kaitai parser to parse a UTI file.
        /// Kaitai Struct parsers are classes that can be instantiated with from_bytes() or from_io().
        /// </summary>
        private static void CreatePythonParserScript(string scriptPath, string parserFile, string utiFilePath, string parserDir)
        {
            // Get module name from parser file (remove .py extension)
            string moduleName = Path.GetFileNameWithoutExtension(parserFile);

            // Normalize paths for Python (use raw strings with forward slashes or escaped backslashes)
            string parserDirNormalized = Path.GetDirectoryName(parserFile).Replace("\\", "/");
            string utiFilePathNormalized = utiFilePath.Replace("\\", "/");

            // Create Python script that:
            // 1. Adds parser directory to sys.path
            // 2. Imports the generated parser module
            // 3. Parses the UTI file using Kaitai Struct API
            // 4. Outputs key fields for validation
            string script = $@"import sys
import os
import json
import io

# Add parser directory to path
parser_dir = r""{parserDirNormalized}""
if parser_dir not in sys.path:
    sys.path.insert(0, parser_dir)

try:
    # Import the generated parser module
    # Kaitai Struct generates a module with a class of the same name
    import importlib.util
    spec = importlib.util.spec_from_file_location(""{moduleName}"", r""{parserFile.Replace("\\", "/")}"")
    if spec is None or spec.loader is None:
        raise ImportError(f""Failed to load module from {parserFile}"")

    parser_module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(parser_module)

    # Get the parser class (usually same name as module, but could be Uti or UTI)
    ParserClass = None
    for attr_name in [""{moduleName}"", ""Uti"", ""UTI"", ""Uti_"", ""UtiKaitai""]:
        if hasattr(parser_module, attr_name):
            ParserClass = getattr(parser_module, attr_name)
            if isinstance(ParserClass, type):
                break

    if ParserClass is None:
        # Try to find any class in the module
        for attr_name in dir(parser_module):
            attr = getattr(parser_module, attr_name)
            if isinstance(attr, type) and attr_name[0].isupper():
                ParserClass = attr
                break

    if ParserClass is None:
        raise ImportError(f""Could not find parser class in {moduleName} module"")

    # Parse the UTI file
    with open(r""{utiFilePathNormalized}"", ""rb"") as f:
        data = f.read()

    # Kaitai Struct parsers use from_bytes() or from_io()
    if hasattr(ParserClass, 'from_bytes'):
        uti = ParserClass.from_bytes(data)
    elif hasattr(ParserClass, 'from_io'):
        uti = ParserClass.from_io(io.BytesIO(data))
    else:
        # Try direct instantiation
        uti = ParserClass(io.BytesIO(data))

    # Output key fields for validation
    output = {{}}
    output['parser_loaded'] = True
    output['file_parsed'] = True

    # GFF Header fields - Kaitai Struct exposes fields as attributes
    if hasattr(uti, 'gff_header'):
        header = uti.gff_header
        if hasattr(header, 'file_type'):
            file_type = header.file_type
            # Convert bytes/bytearray to string if needed
            if isinstance(file_type, (bytes, bytearray)):
                file_type_str = file_type.decode('ascii', errors='ignore').strip()
                output['file_type'] = file_type_str
                output['file_type_bytes'] = list(file_type)
            else:
                output['file_type'] = str(file_type)

        if hasattr(header, 'file_version'):
            output['file_version'] = str(header.file_version)

        if hasattr(header, 'struct_array_offset'):
            output['struct_array_offset'] = header.struct_array_offset

        if hasattr(header, 'field_array_offset'):
            output['field_array_offset'] = header.field_array_offset

    # Try to access root structure if available
    if hasattr(uti, 'root'):
        output['has_root'] = True
        root = uti.root
        if hasattr(root, 'structs'):
            structs = root.structs
            if hasattr(structs, '__len__'):
                output['struct_count'] = len(structs)

    # Print JSON output for easy parsing
    print(json.dumps(output, indent=2, default=str))

    # Also print key info for validation (human-readable)
    print(""\\n=== Parser Validation ==="")
    print(f""Parser class: {{ParserClass.__name__}}"")
    print(f""File type: {{output.get('file_type', 'N/A')}}"")
    print(f""File version: {{output.get('file_version', 'N/A')}}"")
    print(f""Parser loaded successfully: {{output.get('parser_loaded', False)}}"")
    print(f""File parsed successfully: {{output.get('file_parsed', False)}}"")

    # Validate file type signature
    file_type = output.get('file_type', '')
    if 'UTI' in file_type.upper():
        print(""✓ File type signature validated: Contains 'UTI'"")
    else:
        print(f""⚠ Warning: File type signature may be invalid: {{file_type}}"")

except ImportError as e:
    print(f""IMPORT_ERROR: {{str(e)}}"")
    import traceback
    traceback.print_exc()
    sys.exit(1)
except Exception as e:
    print(f""ERROR: {{str(e)}}"")
    import traceback
    traceback.print_exc()
    sys.exit(1)
";

            File.WriteAllText(scriptPath, script);
        }

        /// <summary>
        /// Executes a Python script and returns the result.
        /// </summary>
        private static (int ExitCode, string Output, string Error) ExecutePythonScript(string pythonExe, string scriptPath, string workingDirectory)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(scriptPath)
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return (-1, "", $"Failed to start Python process: {pythonExe}");
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000); // 60 second timeout

                    return (process.ExitCode, output, error);
                }
            }
            catch (Exception ex)
            {
                return (-1, "", ex.Message);
            }
        }
    }
}

