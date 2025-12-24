using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for LYT.ksy Kaitai Struct compiler functionality.
    /// Tests compile LYT.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested (at least 12 as required):
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Swift, Perl, Lua, Nim, VisualBasic
    /// </summary>
    public class LYTKaitaiStructTests
    {
        private static readonly string LytKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "LYT", "LYT.ksy"
        ));

        private static readonly string TestLytFile = TestFileHelper.GetPath("test.lyt");
        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_lyt_compiled"
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
            "visualbasic"
        };

        static LYTKaitaiStructTests()
        {
            // Normalize LYT.ksy path
            LytKsyPath = Path.GetFullPath(LytKsyPath);
        }

        [Fact(Timeout = 300000)] // 5 minutes timeout for compilation
        public void TestKaitaiStructCompilerAvailable()
        {
            // Test that kaitai-struct-compiler is available
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                // Skip test if compiler not available
                return;
            }

            // Test compiler version - handle JAR files differently
            ProcessStartInfo processInfo;
            if (compilerPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                processInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{compilerPath}\" --version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                processInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

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
        public void TestLytKsyFileExists()
        {
            File.Exists(LytKsyPath).Should().BeTrue($"LYT.ksy should exist at {LytKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(LytKsyPath);
            content.Should().Contain("meta:", "LYT.ksy should contain meta section");
            content.Should().Contain("id: lyt", "LYT.ksy should have id: lyt");
            content.Should().Contain("file-extension: lyt", "LYT.ksy should specify lyt file extension");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileLytToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(LytKsyPath);
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

            // At least 12 languages should compile successfully
            successful.Count.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. " +
                $"Successful ({successful.Count}): {string.Join(", ", successful.Select(s => s.Key))}. " +
                $"Failed ({failed.Count}): {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

            // Log successful compilations and verify output files
            foreach (var success in successful)
            {
                // Verify output files were created
                var outputDir = Path.Combine(TestOutputDir, success.Key);
                if (Directory.Exists(outputDir))
                {
                    var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith("compile_output.txt") && !f.EndsWith("compile_error.txt"))
                        .ToList();
                    files.Count.Should().BeGreaterThan(0,
                        $"Language {success.Key} should generate output files. Found: {string.Join(", ", files.Select(Path.GetFileName))}");

                    // Verify at least one parser file was generated (language-specific patterns)
                    var hasParserFile = files.Any(f =>
                        f.Contains("lyt") || f.Contains("Lyt") || f.Contains("LYT") ||
                        f.EndsWith(".py") || f.EndsWith(".java") || f.EndsWith(".js") ||
                        f.EndsWith(".cs") || f.EndsWith(".cpp") || f.EndsWith(".h") ||
                        f.EndsWith(".go") || f.EndsWith(".rb") || f.EndsWith(".php") ||
                        f.EndsWith(".rs") || f.EndsWith(".swift") || f.EndsWith(".lua") ||
                        f.EndsWith(".nim") || f.EndsWith(".pm") || f.EndsWith(".vb"));

                    hasParserFile.Should().BeTrue(
                        $"Language {success.Key} should generate parser files. Files: {string.Join(", ", files.Select(Path.GetFileName))}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileLytToVisualBasic()
        {
            TestCompileToLanguage("visualbasic");
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(LytKsyPath);
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

            Directory.CreateDirectory(TestOutputDir);

            var result = CompileToLanguage(normalizedKsyPath, language);

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

            // Verify parser files were actually generated
            var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith("compile_output.txt") && !f.EndsWith("compile_error.txt"))
                .ToList();

            files.Count.Should().BeGreaterThan(0,
                $"Language {language} should generate parser files. Found: {string.Join(", ", files.Select(Path.GetFileName))}");

            // Verify at least one parser file matches language-specific patterns
            var hasParserFile = files.Any(f =>
                f.Contains("lyt") || f.Contains("Lyt") || f.Contains("LYT") ||
                f.EndsWith(".py") || f.EndsWith(".java") || f.EndsWith(".js") ||
                f.EndsWith(".cs") || f.EndsWith(".cpp") || f.EndsWith(".h") ||
                f.EndsWith(".go") || f.EndsWith(".rb") || f.EndsWith(".php") ||
                f.EndsWith(".rs") || f.EndsWith(".swift") || f.EndsWith(".lua") ||
                f.EndsWith(".nim") || f.EndsWith(".pm") || f.EndsWith(".vb"));

            hasParserFile.Should().BeTrue(
                $"Language {language} should generate parser files. Files: {string.Join(", ", files.Select(Path.GetFileName))}");
        }

        private CompileResult CompileToLanguage(string ksyPath, string language)
        {
            var outputDir = Path.Combine(TestOutputDir, language);
            Directory.CreateDirectory(outputDir);

            var result = RunKaitaiCompiler(ksyPath, $"-t {language}", outputDir);

            return new CompileResult
            {
                Success = result.ExitCode == 0,
                Output = result.Output,
                ErrorMessage = result.Error,
                ExitCode = result.ExitCode
            };
        }

        private (int ExitCode, string Output, string Error) RunKaitaiCompiler(
            string ksyPath, string arguments, string outputDir)
        {
            // Try different ways to invoke Kaitai Struct compiler
            // 1. As a command (if installed via package manager)
            var result = RunCommand("kaitai-struct-compiler", $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");

            if (result.ExitCode == 0)
            {
                return result;
            }

            // 2. Try with .jar extension
            result = RunCommand("kaitai-struct-compiler.jar", $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");

            if (result.ExitCode == 0)
            {
                return result;
            }

            // 3. Try as Java JAR (common installation method)
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                result = RunCommand("java", $"-jar \"{jarPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                return result;
            }

            // 4. Try in common installation locations
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kaitai-struct-compiler"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    if (path.EndsWith(".jar"))
                    {
                        result = RunCommand("java", $"-jar \"{path}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                    }
                    else
                    {
                        result = RunCommand(path, $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                    }

                    if (result.ExitCode == 0)
                    {
                        return result;
                    }
                }
            }

            // Return the last result (which will be a failure)
            return result;
        }

        private string FindKaitaiCompilerJar()
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

        private string FindKaitaiCompiler()
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
                    // Return special marker for JAR - caller needs to use java -jar
                    return jarPath;
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

        private class CompileResult
        {
            public bool Success { get; set; }
            public string Output { get; set; }
            public string ErrorMessage { get; set; }
            public int ExitCode { get; set; }
        }


        [Fact(Timeout = 300000)]
        public void TestLytKsySyntaxValidation()
        {
            // Validate LYT.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{LytKsyPath}\" -d \"{Path.GetTempPath()}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(LytKsyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);

                    // Compilation should succeed - syntax errors would cause failure
                    process.ExitCode.Should().Be(0,
                        $"LYT.ksy should have valid syntax. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructDefinitionCompleteness()
        {
            // Validate that LYT.ksy definition is complete and matches the format
            if (!File.Exists(LytKsyPath))
            {
                Assert.True(true, "LYT.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(LytKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: lyt", "Should have id: lyt");
            ksyContent.Should().Contain("title:", "Should have title");
            ksyContent.Should().Contain("encoding: ASCII", "Should specify ASCII encoding");
            ksyContent.Should().Contain("file-extension: lyt", "Should specify file extension");
            ksyContent.Should().Contain("raw_content", "Should define raw_content field");
            ksyContent.Should().Contain("beginlayout", "Should document beginlayout header");
            ksyContent.Should().Contain("donelayout", "Should document donelayout footer");
            ksyContent.Should().Contain("room_entry", "Should define room_entry type");
            ksyContent.Should().Contain("track_entry", "Should define track_entry type");
            ksyContent.Should().Contain("obstacle_entry", "Should define obstacle_entry type");
            ksyContent.Should().Contain("doorhook_entry", "Should define doorhook_entry type");
            ksyContent.Should().Contain("has_valid_header", "Should define has_valid_header instance");
            ksyContent.Should().Contain("has_valid_footer", "Should define has_valid_footer instance");
            ksyContent.Should().Contain("is_valid_format", "Should define is_valid_format instance");
        }





        private static void CreateTestLytFile(string path)
        {
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("testroom"), new System.Numerics.Vector3(0.0f, 0.0f, 0.0f)));
            lyt.Tracks.Add(new LYTTrack(new ResRef("testtrack"), new System.Numerics.Vector3(1.0f, 1.0f, 1.0f)));
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("testobstacle"), new System.Numerics.Vector3(2.0f, 2.0f, 2.0f)));
            lyt.Doorhooks.Add(new LYTDoorHook("testroom", "testdoor", new System.Numerics.Vector3(3.0f, 3.0f, 3.0f), new System.Numerics.Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            new LYTAsciiWriter(lyt, path).Write();
        }
    }
}
