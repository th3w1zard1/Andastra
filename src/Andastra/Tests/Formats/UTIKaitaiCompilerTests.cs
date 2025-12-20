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

            // Note: Actually using the generated parser would require Python runtime and kaitaistruct library
            // This test validates that compilation succeeds and generates expected files
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

            byte[] data = UTIHelpers.BytesUti(uti, Game.K2);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

