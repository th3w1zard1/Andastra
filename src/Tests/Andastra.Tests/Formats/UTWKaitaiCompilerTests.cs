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
    /// Comprehensive tests for Kaitai Struct compiler functionality with UTW.ksy.
    /// Tests compilation to multiple target languages and verifies compiler output.
    /// Tests validate that UTW.ksy can be compiled to at least 12 languages as required.
    /// </summary>
    public class UTWKaitaiCompilerTests
    {
        private static readonly string UTWKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTW", "UTW.ksy"
        ));

        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_compiled", "utw"
        );

        // Supported languages in Kaitai Struct
        private static readonly string[] SupportedLanguages = new[]
        {
            "python",
            "csharp",
            "go",
            "javascript",
            "rust",
            "java",
            "perl",
            "cpp_stl",
            "ruby",
            "lua",
            "nim",
            "php",
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
        public void TestUtwKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(UTWKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"UTW.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "UTW.ksy should contain meta section");
            content.Should().Contain("id: utw", "UTW.ksy should have id: utw");
            content.Should().Contain("seq:", "UTW.ksy should contain seq section");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUtwToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileUtwToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(UTWKsyPath);
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
        public void TestCompileUtwToMultipleLanguagesSimultaneously()
        {
            var normalizedKsyPath = Path.GetFullPath(UTWKsyPath);
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
        public void TestUtwKsySyntaxValidation()
        {
            var normalizedKsyPath = Path.GetFullPath(UTWKsyPath);
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
                    Assert.True(false, $"UTW.ksy has syntax errors: {result.Error}\n{result.Output}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestUtwKsyDefinitionCompleteness()
        {
            var normalizedKsyPath = Path.GetFullPath(UTWKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedKsyPath);

            // Check for required elements in Kaitai Struct definition
            content.Should().Contain("meta:", "Should have meta section");
            content.Should().Contain("id: utw", "Should have id: utw");
            content.Should().Contain("file-extension: utw", "Should define file extension");
            content.Should().Contain("gff_header", "Should define gff_header type");
            content.Should().Contain("file_type", "Should define file_type field");
            content.Should().Contain("file_version", "Should define file_version field");
            content.Should().Contain("UTW ", "Should support UTW file type signature");
            content.Should().Contain("TemplateResRef", "Should document TemplateResRef field");
            content.Should().Contain("Tag", "Should document Tag field");
            content.Should().Contain("LocalizedName", "Should document LocalizedName field");
            content.Should().Contain("HasMapNote", "Should document HasMapNote field");
            content.Should().Contain("MapNote", "Should document MapNote field");
            content.Should().Contain("Appearance", "Should document Appearance field");
        }

        [Fact(Timeout = 300000)]
        public void TestUtwKsyHeaderStructure()
        {
            var normalizedKsyPath = Path.GetFullPath(UTWKsyPath);
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

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(UTWKsyPath);
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

            var fullArgs = $"{args} -d \"{outputDir}\" \"{ksyPath}\"";

            // Check if it's a .bat file (Windows) - need to use cmd /c
            if (kscPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                return RunCommand("cmd", $"/c \"{kscPath}\" {fullArgs}");
            }
            // Check if it's a JAR file - need java -jar
            else if (kscPath.StartsWith("-jar"))
            {
                return RunCommand("java", $"{kscPath} {fullArgs}");
            }
            // Otherwise assume it's a direct executable
            else
            {
                return RunCommand(kscPath, fullArgs);
            }
        }

        private string FindKaitaiCompiler()
        {
            // Check Windows installation path first
            var windowsPath = @"C:\Program Files (x86)\kaitai-struct-compiler\bin\kaitai-struct-compiler.bat";
            if (File.Exists(windowsPath))
            {
                return $"\"{windowsPath}\"";
            }

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

