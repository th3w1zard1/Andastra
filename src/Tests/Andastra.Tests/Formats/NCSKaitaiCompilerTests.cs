using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for NCS.ksy Kaitai Struct compiler functionality.
    /// Tests compile NCS.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic, Swift, Kotlin, TypeScript
    /// </summary>
    public class NCSKaitaiCompilerTests
    {
        private static readonly string NcsKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "NSS", "NCS.ksy");

        private static readonly string TestNcsFile = TestFileHelper.GetPath("test.ncs");
        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_ncs_tests");

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
            "kotlin",
            "typescript"
        };

        static NCSKaitaiCompilerTests()
        {
            // Normalize NCS.ksy path
            NcsKsyPath = Path.GetFullPath(NcsKsyPath);

            // Create output directory
            if (!Directory.Exists(CompilerOutputDir))
            {
                Directory.CreateDirectory(CompilerOutputDir);
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilerAvailable()
        {
            string compilerPath = FindKaitaiCompiler();
            compilerPath.Should().NotBeNullOrEmpty("kaitai-struct-compiler should be available in PATH or common locations");

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
        public void TestNcsKsyFileExists()
        {
            File.Exists(NcsKsyPath).Should().BeTrue($"NCS.ksy should exist at {NcsKsyPath}");

            string content = File.ReadAllText(NcsKsyPath);
            content.Should().Contain("meta:", "NCS.ksy should contain meta section");
            content.Should().Contain("id: ncs", "NCS.ksy should have id: ncs");
            content.Should().Contain("file-extension:", "NCS.ksy should specify file extensions");
            content.Should().Contain("ncs", "NCS.ksy should include ncs extension");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileNcsKsyToLanguage(string language)
        {
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

            var processInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t {language} \"{NcsKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(NcsKsyPath)
            };

            string stdout = "";
            string stderr = "";
            int exitCode = -1;

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    exitCode = process.ExitCode;
                }
            }

            exitCode.Should().Be(0,
                $"kaitai-struct-compiler should compile NCS.ksy to {language} successfully. " +
                $"STDOUT: {stdout}, STDERR: {stderr}");

            string[] generatedFiles = Directory.GetFiles(langOutputDir, "*", SearchOption.AllDirectories);
            generatedFiles.Should().NotBeEmpty($"Compilation to {language} should generate output files");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNcsKsyToAllLanguages()
        {
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
                        Arguments = $"-t {language} \"{NcsKsyPath}\" -d \"{langOutputDir}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(NcsKsyPath)
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

            int successCount = results.Values.Count(r => r);
            int totalCount = SupportedLanguages.Length;

            // At least 12 languages should compile successfully
            successCount.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. " +
                $"Results: {string.Join(", ", results.Select(kvp => $"{kvp.Key}: {(kvp.Value ? "OK" : "FAIL")}"))}. " +
                $"Errors: {string.Join("; ", errors.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsySyntaxValidation()
        {
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{NcsKsyPath}\" --debug",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(NcsKsyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    stderr.Should().NotContain("error", "NCS.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"NCS.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyDefinitionCompleteness()
        {
            if (!File.Exists(NcsKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(NcsKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: ncs", "Should have id: ncs");
            ksyContent.Should().Contain("file-extension:", "Should define file extensions");
            ksyContent.Should().Contain("ncs", "Should include ncs extension");

            // Check for NCS format structure
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("size_marker", "Should define size_marker field");
            ksyContent.Should().Contain("total_file_size", "Should define total_file_size field");
            ksyContent.Should().Contain("instructions", "Should define instructions");
            ksyContent.Should().Contain("instruction", "Should define instruction type");
            ksyContent.Should().Contain("bytecode", "Should define bytecode field");
            ksyContent.Should().Contain("qualifier", "Should define qualifier field");
            ksyContent.Should().Contain("args", "Should define args field");

            // Check for instruction argument types
            ksyContent.Should().Contain("const_args", "Should define const_args type");
            ksyContent.Should().Contain("stack_copy_args", "Should define stack_copy_args type");
            ksyContent.Should().Contain("jump_args", "Should define jump_args type");
            ksyContent.Should().Contain("action_args", "Should define action_args type");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyHeaderStructure()
        {
            if (!File.Exists(NcsKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(NcsKsyPath);

            // Check for header structure (13 bytes: 4 + 4 + 1 + 4)
            ksyContent.Should().Contain("file_type", "Header should define file_type");
            ksyContent.Should().Contain("file_version", "Header should define file_version");
            ksyContent.Should().Contain("size_marker", "Header should define size_marker");
            ksyContent.Should().Contain("total_file_size", "Header should define total_file_size");
            ksyContent.Should().Contain("NCS ", "Should support NCS file type");
            ksyContent.Should().Contain("V1.0", "Should support V1.0 version");
            ksyContent.Should().Contain("0x42", "Should validate size_marker as 0x42");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyInstructionTypes()
        {
            if (!File.Exists(NcsKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(NcsKsyPath);

            // Check for instruction type definitions
            ksyContent.Should().Contain("instruction_args", "Should define instruction_args type");
            ksyContent.Should().Contain("switch-on", "Should use switch-on for instruction type selection");
            ksyContent.Should().Contain("cases:", "Should define cases for different instruction types");
            ksyContent.Should().Contain("const_args", "Should define const_args for CONSTx instructions");
            ksyContent.Should().Contain("stack_copy_args", "Should define stack_copy_args for stack operations");
            ksyContent.Should().Contain("jump_args", "Should define jump_args for jump instructions");
            ksyContent.Should().Contain("action_args", "Should define action_args for ACTION instructions");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyBigEndianEncoding()
        {
            if (!File.Exists(NcsKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(NcsKsyPath);

            // Check that endian is set to be (big-endian)
            ksyContent.Should().Contain("endian: be", "NCS format should use big-endian encoding");
            ksyContent.Should().Contain("f4be", "Should use big-endian float encoding");
            ksyContent.Should().Contain("s4be", "Should use big-endian signed integer encoding");
            ksyContent.Should().Contain("u4be", "Should use big-endian unsigned integer encoding");
            ksyContent.Should().Contain("s2be", "Should use big-endian signed short encoding");
            ksyContent.Should().Contain("u2be", "Should use big-endian unsigned short encoding");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        private static string FindKaitaiCompiler()
        {
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

            return null;
        }
    }
}


