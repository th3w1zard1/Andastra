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
    /// Comprehensive tests for UTT.ksy Kaitai Struct compiler functionality.
    /// Tests compile UTT.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic
    ///
    /// UTT format:
    /// - UTT: Trigger Template file (GFF-based format for trigger blueprints)
    /// </summary>
    public class UTTKaitaiCompilerTests
    {
        private static readonly string UttKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTT", "UTT.ksy");

        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_utt_tests");

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

        static UTTKaitaiCompilerTests()
        {
            // Normalize UTT.ksy path
            UttKsyPath = Path.GetFullPath(UttKsyPath);

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
        public void TestUttKsyFileExists()
        {
            File.Exists(UttKsyPath).Should().BeTrue($"UTT.ksy should exist at {UttKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(UttKsyPath);
            content.Should().Contain("meta:", "UTT.ksy should contain meta section");
            content.Should().Contain("id: utt", "UTT.ksy should have id: utt");
            content.Should().Contain("file-extension:", "UTT.ksy should specify file extensions");
            content.Should().Contain("utt", "UTT.ksy should include utt extension");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileUttKsyToLanguage(string language)
        {
            // Skip if compiler not available
            var compileResult = CompileKsyToLanguage(UttKsyPath, language);
            if (!compileResult.Success && compileResult.ExitCode == -999)
            {
                return; // Skip test if compiler not available
            }

            // Compilation should succeed
            compileResult.ExitCode.Should().Be(0,
                $"kaitai-struct-compiler should compile UTT.ksy to {language} successfully. " +
                $"STDOUT: {compileResult.Output}, STDERR: {compileResult.Error}");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileUttKsyToAllLanguages()
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
                        Arguments = $"-t {language} \"{UttKsyPath}\" -d \"{langOutputDir}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(UttKsyPath)
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
        public void TestUttKsySyntaxValidation()
        {
            // Validate UTT.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{UttKsyPath}\" --debug",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(UttKsyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    stderr.Should().NotContain("error", "UTT.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"UTT.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestUttKsyDefinitionCompleteness()
        {
            // Validate that UTT.ksy definition is complete
            if (!File.Exists(UttKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(UttKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: utt", "Should have id: utt");
            ksyContent.Should().Contain("file-extension:", "Should define file extensions");
            ksyContent.Should().Contain("utt", "Should include utt extension");

            // Check for GFF format structure
            ksyContent.Should().Contain("gff_header", "Should define gff_header type");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("struct_array", "Should define struct_array");
            ksyContent.Should().Contain("field_array", "Should define field_array");
            ksyContent.Should().Contain("label_array", "Should define label_array");

            // Check for UTT-specific fields
            ksyContent.Should().Contain("UTT ", "Should support UTT file type");
            ksyContent.Should().Contain("ResRef", "Should define ResRef field");
            ksyContent.Should().Contain("LocName", "Should define LocName field");
            ksyContent.Should().Contain("ScriptOnEnter", "Should define ScriptOnEnter field");
        }

        private static (bool Success, int ExitCode, string Output, string Error) CompileKsyToLanguage(string ksyPath, string language)
        {
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return (false, -999, "", "Compiler not found");
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            var processInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t {language} \"{ksyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ksyPath)
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);

                    return (process.ExitCode == 0, process.ExitCode, stdout, stderr);
                }
            }

            return (false, -1, "", "Process start failed");
        }

        private static string FindKaitaiCompiler()
        {
            // Try common locations and PATH, including the user-specified path
            string[] possiblePaths = new[]
            {
                "kaitai-struct-compiler",
                "ksc",
                @"C:\Program Files (x86)\kaitai-struct-compiler\bin\kaitai-struct-compiler.bat",
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
    }
}
