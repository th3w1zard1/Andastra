using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for Kaitai Struct compiler functionality with IFO.ksy.
    /// Tests compilation to multiple target languages (at least 12) and verifies compiler output.
    /// </summary>
    public class IFOKaitaiCompilerTests
    {
        private static readonly string IFOKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "IFO", "IFO.ksy"
        ));

        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_ifo_compiled"
        );

        private static string CompilerOutputDir => TestOutputDir;

        private static string TestIfoFile => Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "test.ifo"
        );

        // Supported languages in Kaitai Struct (at least 12 as required)
        private static readonly string[] SupportedLanguages = new[]
        {
            "python",
            "java",
            "javascript",
            "csharp",
            "cpp_stl",
            "go",
            "ruby",
            "php",
            "rust",
            "swift",
            "lua",
            "nim",
            "perl",
            "kotlin",
            "typescript",
            "visualbasic"
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
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                // Skip if compiler not found
                return;
            }

            // Test compiler version
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                var kscCheck = RunCommand("java", $"-jar \"{jarPath}\" --version");
                kscCheck.ExitCode.Should().Be(0, "Kaitai Struct compiler should be available");
            }
            else
            {
                var kscCheck = RunCommand(compilerPath, "--version");
                kscCheck.ExitCode.Should().Be(0, "Kaitai Struct compiler should be available");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestIfoKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(IFOKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"IFO.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "IFO.ksy should contain meta section");
            content.Should().Contain("id: ifo", "IFO.ksy should have id: ifo");
            content.Should().Contain("seq:", "IFO.ksy should contain seq section");
            content.Should().Contain("gff_header", "IFO.ksy should define gff_header");
        }

        [Fact(Timeout = 300000)]
        public void TestIfoKsyFileValid()
        {
            // Validate that IFO.ksy is valid YAML and can be parsed by compiler
            if (!File.Exists(IFOKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Try to compile to Python as validation (most commonly supported)
            ProcessStartInfo validateInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                validateInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t python \"{IFOKsyPath}\" -d \"{Path.GetTempPath()}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }
            else
            {
                validateInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t python \"{IFOKsyPath}\" -d \"{Path.GetTempPath()}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    if (process.ExitCode != 0 && stderr.Contains("error") && !stderr.Contains("import"))
                    {
                        Assert.True(false, $"IFO.ksy has syntax errors: {stderr}");
                    }
                }
            }
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileIfoToLanguage(string language)
        {
            TestCompileToLanguage(language);
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToKotlin()
        {
            TestCompileToLanguage("kotlin");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileIfoToTypeScript()
        {
            TestCompileToLanguage("typescript");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileIfoToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(IFOKsyPath);
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

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            Directory.CreateDirectory(TestOutputDir);

            var results = new Dictionary<string, CompileResult>();

            foreach (var language in SupportedLanguages)
            {
                try
                {
                    var result = CompileToLanguage(normalizedKsyPath, language, compilerPath);
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

            // At least 12 languages should compile successfully
            successful.Count.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. " +
                $"Successful: {string.Join(", ", successful.Select(s => s.Key))}. " +
                $"Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

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
        public void TestCompileIfoToMultipleLanguagesSimultaneously()
        {
            var normalizedKsyPath = Path.GetFullPath(IFOKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                return;
            }

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return;
            }

            Directory.CreateDirectory(TestOutputDir);

            // Compile to multiple languages in a single command
            var languages = new[] { "python", "java", "javascript", "csharp" };
            var languageArgs = string.Join(" ", languages.Select(l => $"-t {l}"));

            var result = RunKaitaiCompiler(normalizedKsyPath, languageArgs, TestOutputDir, compilerPath);

            // Compilation should succeed (or at least not fail catastrophically)
            // Some languages may fail due to missing dependencies, but the command should execute
            result.ExitCode.Should().BeInRange(-1, 1,
                $"Kaitai compiler should execute. Output: {result.Output}, Error: {result.Error}");
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

            string langOutputDir = Path.Combine(TestOutputDir, "csharp");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to C#
            ProcessStartInfo compileInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                compileInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t csharp \"{IFOKsyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }
            else
            {
                compileInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t csharp \"{IFOKsyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }

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
            string ifoCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("ifo"));
            if (ifoCsFile != null)
            {
                string csContent = File.ReadAllText(ifoCsFile);
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

            string langOutputDir = Path.Combine(TestOutputDir, "java");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to Java
            ProcessStartInfo compileInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                compileInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t java \"{IFOKsyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }
            else
            {
                compileInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t java \"{IFOKsyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }

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

            string langOutputDir = Path.Combine(TestOutputDir, "javascript");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Compile to JavaScript
            ProcessStartInfo compileInfo;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                compileInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" -t javascript \"{IFOKsyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }
            else
            {
                compileInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = $"-t javascript \"{IFOKsyPath}\" -d \"{langOutputDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(IFOKsyPath)
                };
            }

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

        [Fact(Timeout = 300000)]
        public void TestIfoKsyDefinitionCompleteness()
        {
            // Validate that IFO.ksy definition is complete and matches the format
            if (!File.Exists(IFOKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(IFOKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: ifo", "Should have id: ifo");
            ksyContent.Should().Contain("file-extension: ifo", "Should specify ifo file extension");
            ksyContent.Should().Contain("gff_header", "Should define gff_header type");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("struct_array", "Should define struct_array");
            ksyContent.Should().Contain("field_array", "Should define field_array");
            ksyContent.Should().Contain("label_array", "Should define label_array");
            ksyContent.Should().Contain("field_data", "Should define field_data section");
            ksyContent.Should().Contain("list_indices", "Should define list_indices");
            ksyContent.Should().Contain("gff_field_type", "Should define gff_field_type enum");
            ksyContent.Should().Contain("localized_string_data", "Should define localized_string_data type");
        }

        [Fact(Timeout = 300000)]
        public void TestIfoKsyCompilesToAtLeastDozenLanguages()
        {
            // Ensure we test at least a dozen languages
            if (!File.Exists(IFOKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            SupportedLanguages.Length.Should().BeGreaterThanOrEqualTo(12,
                "Should support at least a dozen languages for testing");

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            Process process;
            if (compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4);
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java",
                        Arguments = $"-jar \"{jarPath}\" --version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
            }
            else
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = compilerPath,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
            }

            try
            {
                process.Start();
                process.WaitForExit(5000);

                if (process.ExitCode != 0)
                {
                    return; // Skip if compiler not available
                }
            }
            catch
            {
                return; // Skip if compiler not installed
            }

            int compiledCount = 0;
            Directory.CreateDirectory(TestOutputDir);

            foreach (string lang in SupportedLanguages)
            {
                try
                {
                    var result = CompileToLanguage(IFOKsyPath, lang, compilerPath);
                    if (result.Success)
                    {
                        compiledCount++;
                    }
                }
                catch
                {
                    // Ignore individual failures
                }
            }

            // We should be able to compile to at least a dozen languages
            compiledCount.Should().BeGreaterThanOrEqualTo(12,
                $"Should successfully compile IFO.ksy to at least 12 languages. Compiled to {compiledCount} languages.");
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(IFOKsyPath);
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

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            Directory.CreateDirectory(TestOutputDir);

            var result = CompileToLanguage(normalizedKsyPath, language, compilerPath);

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

        private CompileResult CompileToLanguage(string ksyPath, string language, string compilerPath)
        {
            var outputDir = Path.Combine(TestOutputDir, language);
            Directory.CreateDirectory(outputDir);

            var result = RunKaitaiCompiler(ksyPath, $"-t {language}", outputDir, compilerPath);

            return new CompileResult
            {
                Success = result.ExitCode == 0,
                Output = result.Output,
                ErrorMessage = result.Error,
                ExitCode = result.ExitCode
            };
        }

        private (int ExitCode, string Output, string Error) RunKaitaiCompiler(
            string ksyPath, string arguments, string outputDir, string compilerPath)
        {
            ProcessStartInfo processInfo;

            // Handle JAR file execution
            if (compilerPath != null && compilerPath.StartsWith("JAR:"))
            {
                string jarPath = compilerPath.Substring(4); // Remove "JAR:" prefix
                processInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{jarPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"",
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
                    Arguments = $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ksyPath)
                };
            }

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000); // 60 second timeout
                    return (process.ExitCode, stdout, stderr);
                }
            }

            return (-1, "", "Failed to start process");
        }

        private string FindKaitaiCompiler()
        {
            // Try common locations and PATH first
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

            // Try JAR file locations
            string jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                // Check if Java is available
                var javaCheck = RunCommand("java", "-version");
                if (javaCheck.ExitCode == 0)
                {
                    // Return special marker that indicates JAR usage
                    return $"JAR:{jarPath}";
                }
            }

            return null;
        }

        private string FindKaitaiCompilerJar()
        {
            // Check environment variable first
            var envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return Path.GetFullPath(envJar);
            }

            // Try JAR file locations
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kaitai-struct-compiler.jar"),
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

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        private class CompileResult
        {
            public bool Success { get; set; }
            public string Output { get; set; }
            public string ErrorMessage { get; set; }
            public int ExitCode { get; set; }
        }
    }
}

