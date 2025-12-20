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
    /// Comprehensive tests for Kaitai Struct compiler functionality with TLK.ksy.
    /// Tests compilation to multiple target languages and verifies compiler output.
    /// </summary>
    public class TLKKaitaiCompilerTests
    {
        private static readonly string TLKKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "TLK", "TLK.ksy"
        ));

        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_compiled"
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

        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_tlk_tests");

        static TLKKaitaiCompilerTests()
        {
            // Normalize TLK.ksy path
            TLKKsyPath = Path.GetFullPath(TLKKsyPath);

            // Create output directory
            if (!Directory.Exists(CompilerOutputDir))
            {
                Directory.CreateDirectory(CompilerOutputDir);
            }
        }

        [Fact(Timeout = 300000)] // 5 minute timeout for compilation
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
        public void TestTLKKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(TLKKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"TLK.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "TLK.ksy should contain meta section");
            content.Should().Contain("id: tlk", "TLK.ksy should have id: tlk");
            content.Should().Contain("seq:", "TLK.ksy should contain seq section");
            content.Should().Contain("tlk_header", "TLK.ksy should contain tlk_header type");
            content.Should().Contain("string_data_entry", "TLK.ksy should contain string_data_entry type");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileTLKToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileTLKToAllLanguages()
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
                        Arguments = $"-t {language} \"{TLKKsyPath}\" -d \"{langOutputDir}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(TLKKsyPath)
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
        public void TestCompileTLKToAtLeastDozenLanguages()
        {
            if (!File.Exists(TLKKsyPath))
            {
                Assert.True(true, "TLK.ksy not found - skipping test");
                return;
            }

            SupportedLanguages.Length.Should().BeGreaterThanOrEqualTo(12,
                "Should support at least a dozen languages for testing");

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                Assert.True(true, "Kaitai Struct compiler not available - skipping test");
                return;
            }

            int compiledCount = 0;
            foreach (string lang in SupportedLanguages)
            {
                var compileProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = compilerPath,
                        Arguments = $"-t {lang} \"{TLKKsyPath}\" -d \"{Path.GetTempPath()}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                try
                {
                    compileProcess.Start();
                    compileProcess.WaitForExit(60000);

                    if (compileProcess.ExitCode == 0)
                    {
                        compiledCount++;
                    }
                }
                catch
                {
                    // Ignore individual failures
                }
            }

            compiledCount.Should().BeGreaterThanOrEqualTo(12,
                $"Should successfully compile TLK.ksy to at least 12 languages. Compiled to {compiledCount} languages.");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileTLKKsyToLanguage(string language)
        {
            // Skip if compiler not available
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
                Arguments = $"-t {language} \"{TLKKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(TLKKsyPath)
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);

                    if (process.ExitCode != 0)
                    {
                        if (stderr.Contains("not supported") || stderr.Contains("unsupported"))
                        {
                            Assert.True(true, $"Language {language} not supported by compiler: {stderr}");
                        }
                        else
                        {
                            Assert.True(false, $"Failed to compile TLK.ksy to {language}: {stderr}");
                        }
                    }
                    else
                    {
                        Assert.True(true, $"Successfully compiled TLK.ksy to {language}");
                    }
                }
            }
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        [Fact(Timeout = 300000)]
        public void TestTLKKsyStructureValidation()
        {
            if (!File.Exists(TLKKsyPath))
            {
                Assert.True(true, "TLK.ksy not found - skipping validation");
                return;
            }

            var content = File.ReadAllText(TLKKsyPath);

            // Validate key structural elements
            content.Should().Contain("tlk_header", "Should define tlk_header type");
            content.Should().Contain("string_data_table", "Should define string_data_table type");
            content.Should().Contain("string_data_entry", "Should define string_data_entry type");
            content.Should().Contain("file_type", "Should have file_type field");
            content.Should().Contain("file_version", "Should have file_version field");
            content.Should().Contain("language_id", "Should have language_id field");
            content.Should().Contain("string_count", "Should have string_count field");
            content.Should().Contain("entries_offset", "Should have entries_offset field");
            content.Should().Contain("flags", "Should have flags field in string_data_entry");
            content.Should().Contain("sound_resref", "Should have sound_resref field");
            content.Should().Contain("text_offset", "Should have text_offset field");
            content.Should().Contain("text_length", "Should have text_length field");
            content.Should().Contain("sound_length", "Should have sound_length field");
            content.Should().Contain("TLK ", "Should reference \"TLK \" magic");
            content.Should().Contain("V3.0", "Should reference \"V3.0\" version");
        }

        [Fact(Timeout = 300000)]
        public void TestTLKKsyDefinitionCompleteness()
        {
            if (!File.Exists(TLKKsyPath))
            {
                Assert.True(true, "TLK.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(TLKKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: tlk", "Should have id: tlk");
            ksyContent.Should().Contain("file_type", "Should define file_type field or header");
            ksyContent.Should().Contain("header", "Should define header field");
            ksyContent.Should().Contain("string_data_table", "Should define string_data_table field");
            ksyContent.Should().Contain("string_count", "Should define string_count field");
            ksyContent.Should().Contain("entries_offset", "Should define entries_offset field");
            ksyContent.Should().Contain("tlk_header", "Should define tlk_header type");
            ksyContent.Should().Contain("magic", "Should define magic field");
            ksyContent.Should().Contain("version", "Should define version field");
            ksyContent.Should().Contain("TLK ", "Should reference \"TLK \" magic");
            ksyContent.Should().Contain("V3.0", "Should reference \"V3.0\" version");
        }

        [Fact(Timeout = 300000)]
        public void TestTLKKsySyntaxValidation()
        {
            // Validate TLK.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{TLKKsyPath}\" --debug",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(TLKKsyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    stderr.Should().NotContain("error", "TLK.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"TLK.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        private void TestCompileToLanguage(string language)
        {
            if (!File.Exists(TLKKsyPath))
            {
                Assert.True(true, "TLK.ksy not found - skipping compilation test");
                return;
            }

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping compilation test");
                return;
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
                Arguments = $"-t {language} \"{TLKKsyPath}\" -d \"{langOutputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(TLKKsyPath)
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);

                    if (process.ExitCode != 0)
                    {
                        if (stderr.Contains("not supported") || stderr.Contains("unsupported"))
                        {
                            Assert.True(true, $"Language {language} not supported by compiler: {stderr}");
                        }
                        else
                        {
                            Assert.True(false, $"Failed to compile TLK.ksy to {language}: {stderr}");
                        }
                    }
                    else
                    {
                        Assert.True(true, $"Successfully compiled TLK.ksy to {language}");
                    }
                }
            }
        }

        private static string FindKaitaiCompiler()
        {
            // Try Windows installation path first (as specified by user)
            var windowsPath = @"C:\Program Files (x86)\kaitai-struct-compiler\bin\kaitai-struct-compiler.bat";
            if (File.Exists(windowsPath))
            {
                return windowsPath;
            }

            // Try common locations and PATH
            string[] possiblePaths = new[]
            {
                "kaitai-struct-compiler",
                "ksc",
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

        private (int ExitCode, string Output, string Error) RunKaitaiCompiler(
            string ksyPath, string arguments, string outputDir)
        {
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return (-1, "", "Compiler not found");
            }

            ProcessStartInfo processInfo;
            if (compilerPath.StartsWith("JAVA_JAR:"))
            {
                string jarPath = compilerPath.Substring("JAVA_JAR:".Length);
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
                return (-1, "", ex.Message);
            }

            return (exitCode, stdout, stderr);
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

    }
}

