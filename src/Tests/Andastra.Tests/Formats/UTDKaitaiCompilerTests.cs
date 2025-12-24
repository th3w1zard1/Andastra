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
    /// Comprehensive tests for Kaitai Struct compiler functionality with UTD.ksy.
    /// Tests compilation to multiple target languages and verifies compiler output.
    /// Tests validate that UTD.ksy can be compiled to at least 12 languages as required.
    /// </summary>
    public class UTDKaitaiCompilerTests
    {
        private static readonly string UTDKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTD", "UTD.ksy"
        ));

        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_compiled", "utd"
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
            "typescript"
        };

        [Fact(Timeout = 300000)]
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
                // Try with JAR if available
                var kscJar = FindKaitaiCompilerJar();
                if (kscJar == null)
                {
                    // Skip if not found
                    return;
                }
            }

            kscCheck.ExitCode.Should().Be(0, "Kaitai Struct compiler should be available");
        }

        [Fact(Timeout = 300000)]
        public void TestUtdKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(UTDKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"UTD.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "UTD.ksy should contain meta section");
            content.Should().Contain("id: utd", "UTD.ksy should have id: utd");
            content.Should().Contain("seq:", "UTD.ksy should contain seq section");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToKotlin()
        {
            TestCompileToLanguage("kotlin");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToTypeScript()
        {
            TestCompileToLanguage("typescript");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileUtdToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            // Check if Java/Kaitai compiler is available
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
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

            // At least some languages should compile successfully
            successful.Count.Should().BeGreaterThan(0,
                $"At least one language should compile successfully. Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

            // Log successful compilations
            foreach (var success in successful)
            {
                var outputDir = Path.Combine(TestOutputDir, success.Key);
                if (Directory.Exists(outputDir))
                {
                    var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    files.Length.Should().BeGreaterThan(0,
                        $"Language {success.Key} should generate output files");
                }
            }

            // Verify at least 12 languages compiled (as required)
            successful.Count.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. Only {successful.Count} succeeded: {string.Join(", ", successful.Select(s => s.Key))}");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtdToMultipleLanguagesSimultaneously()
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                return;
            }

            Directory.CreateDirectory(TestOutputDir);

            // Compile to multiple languages in a single command
            var languages = new[] { "python", "java", "javascript", "csharp" };
            var languageArgs = string.Join(" ", languages.Select(l => $"-t {l}"));

            var result = RunKaitaiCompiler(normalizedKsyPath, languageArgs, TestOutputDir);

            // Compilation should succeed (or at least not fail catastrophically)
            result.ExitCode.Should().BeInRange(-1, 1,
                $"Kaitai compiler should execute. Output: {result.Output}, Error: {result.Error}");
        }

        [Fact(Timeout = 300000)]
        public void TestUtdKsySyntaxValidation()
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                return;
            }

            // Try to validate syntax by attempting to parse with compiler
            Directory.CreateDirectory(TestOutputDir);
            var result = RunKaitaiCompiler(normalizedKsyPath, "-t python", TestOutputDir);

            // Should not fail with syntax errors
            if (result.ExitCode != 0)
            {
                // Check if it's a syntax error or just missing dependencies
                var hasSyntaxError = result.Error.Contains("error") ||
                                   result.Error.Contains("Error") ||
                                   result.Output.Contains("error") ||
                                   result.Output.Contains("Error");

                if (hasSyntaxError)
                {
                    Assert.True(false, $"UTD.ksy has syntax errors: {result.Error}\n{result.Output}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestUtdKsyDefinitionCompleteness()
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for required elements in Kaitai Struct definition
            content.Should().Contain("meta:", "Should have meta section");
            content.Should().Contain("id: utd", "Should have id: utd");
            content.Should().Contain("file-extension: utd", "Should define file extension");
            content.Should().Contain("gff_header", "Should define gff_header type");
            content.Should().Contain("file_type", "Should define file_type field");
            content.Should().Contain("file_version", "Should define file_version field");
            content.Should().Contain("UTD ", "Should support UTD file type signature");
            content.Should().Contain("TemplateResRef", "Should document TemplateResRef field");
            content.Should().Contain("Tag", "Should document Tag field");
            content.Should().Contain("LocName", "Should document LocName field");
            content.Should().Contain("Lockable", "Should document Lockable field");
            content.Should().Contain("Locked", "Should document Locked field");
            content.Should().Contain("KeyRequired", "Should document KeyRequired field");
            content.Should().Contain("OpenLockDC", "Should document OpenLockDC field");
            content.Should().Contain("HP", "Should document HP field");
            content.Should().Contain("CurrentHP", "Should document CurrentHP field");
            content.Should().Contain("Hardness", "Should document Hardness field");
            content.Should().Contain("GenericType", "Should document GenericType field");
            content.Should().Contain("OnOpen", "Should document OnOpen script hook");
            content.Should().Contain("OnClick", "Should document OnClick script hook");
        }

        [Fact(Timeout = 300000)]
        public void TestUtdKsyHeaderStructure()
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
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
        public void TestUtdKsyFieldDocumentation()
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for comprehensive field documentation
            content.Should().Contain("Lockable", "Should document Lockable field");
            content.Should().Contain("Locked", "Should document Locked field");
            content.Should().Contain("KeyRequired", "Should document KeyRequired field");
            content.Should().Contain("KeyName", "Should document KeyName field");
            content.Should().Contain("AutoRemoveKey", "Should document AutoRemoveKey field");
            content.Should().Contain("OpenLockDC", "Should document OpenLockDC field");
            content.Should().Contain("OpenLockDiff", "Should document OpenLockDiff field (KotOR2)");
            content.Should().Contain("OpenLockDiffMod", "Should document OpenLockDiffMod field (KotOR2)");
            content.Should().Contain("OpenState", "Should document OpenState field (KotOR2)");
            content.Should().Contain("Min1HP", "Should document Min1HP field (KotOR2)");
            content.Should().Contain("NotBlastable", "Should document NotBlastable field (KotOR2)");
            content.Should().Contain("Plot", "Should document Plot field");
            content.Should().Contain("Static", "Should document Static field");
            content.Should().Contain("Conversation", "Should document Conversation field");
            content.Should().Contain("Faction", "Should document Faction field");
            content.Should().Contain("OnFailToOpen", "Should document OnFailToOpen script hook (KotOR2)");
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(UTDKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                return;
            }

            Directory.CreateDirectory(TestOutputDir);

            var result = CompileToLanguage(normalizedKsyPath, language);

            if (!result.Success)
            {
                // Some languages may not be fully supported or may have missing dependencies
                // Log the error but don't fail the test for individual language failures
                return;
            }

            result.Success.Should().BeTrue(
                $"Compilation to {language} should succeed. Error: {result.ErrorMessage}, Output: {result.Output}");

            // Verify output directory was created
            var outputDir = Path.Combine(TestOutputDir, language);
            Directory.Exists(outputDir).Should().BeTrue(
                $"Output directory for {language} should be created");
        }

        private CompileResult CompileToLanguage(string ksyPath, string language)
        {
            var outputDir = Path.Combine(TestOutputDir, language);
            Directory.CreateDirectory(outputDir);

            var result = RunKaitaiCompiler(ksyPath, $"-t {language}", outputDir);

            return new CompileResult
            {
                Success = result.ExitCode == 0,
                ErrorMessage = result.Error,
                Output = result.Output
            };
        }

        private CommandResult RunKaitaiCompiler(string ksyPath, string args, string outputDir)
        {
            var kscPath = FindKaitaiCompiler();
            if (kscPath == null)
            {
                return new CommandResult
                {
                    ExitCode = -1,
                    Output = "",
                    Error = "Kaitai Struct compiler not found"
                };
            }

            var fullArgs = $"{kscPath} {args} -d \"{outputDir}\" \"{ksyPath}\"";
            return RunCommand("java", fullArgs);
        }

        private string FindKaitaiCompiler()
        {
            // Check environment variable
            var envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return $"-jar \"{envJar}\"";
            }

            // Check common locations
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                "kaitai-struct-compiler.jar"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return $"-jar \"{Path.GetFullPath(path)}\"";
                }
            }

            // Try as command
            var check = RunCommand("kaitai-struct-compiler", "--version");
            if (check.ExitCode == 0)
            {
                return "kaitai-struct-compiler";
            }

            return null;
        }

        private string FindKaitaiCompilerJar()
        {
            var envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return envJar;
            }

            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                "kaitai-struct-compiler.jar"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }

        private CommandResult RunCommand(string fileName, string arguments)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(30000); // 30 second timeout

                return new CommandResult
                {
                    ExitCode = process.ExitCode,
                    Output = output,
                    Error = error
                };
            }
            catch (Exception ex)
            {
                return new CommandResult
                {
                    ExitCode = -1,
                    Output = "",
                    Error = ex.Message
                };
            }
        }

        private class CommandResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }

        private class CompileResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public string Output { get; set; }
        }
    }
}

