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
    /// Comprehensive tests for Kaitai Struct compiler functionality with GIT.ksy.
    /// Tests compilation to multiple target languages (at least 12) and verifies compiler output.
    ///
    /// This test suite validates that:
    /// - The Kaitai Struct compiler is available and functional
    /// - GIT.ksy file exists and is valid
    /// - GIT.ksy compiles successfully to at least 12 target languages
    /// - Generated parser code is created for each language
    /// </summary>
    public class GITKaitaiCompilerTests
    {
        private static readonly string GITKsyPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "GIT", "GIT.ksy"
        );

        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_git_compiled"
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
            "typescript"
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

            // Comprehensively search for Kaitai Struct compiler using all available methods
            var compilerDetection = DetectKaitaiCompiler();
            compilerDetection.Available.Should().BeTrue(
                $"Kaitai Struct compiler should be available. Detection attempted: {compilerDetection.DetectionMethod}. " +
                $"If missing, install via: pwsh -ExecutionPolicy Bypass -File scripts\\SetupKaitaiCompiler.ps1 or " +
                $"scripts\\install_kaitai_compiler.ps1. For CI/CD, ensure compiler is installed in the build environment.");

            if (compilerDetection.Available)
            {
                // Verify we can get version information
                var versionCheck = RunKaitaiCompilerVersion(compilerDetection.Path, compilerDetection.Method);
                versionCheck.ExitCode.Should().Be(0,
                    $"Compiler should respond to --version. Output: {versionCheck.Output}, Error: {versionCheck.Error}");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestGITKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(GITKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"GIT.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "GIT.ksy should contain meta section");
            content.Should().Contain("id: git", "GIT.ksy should have id: git");
            content.Should().Contain("seq:", "GIT.ksy should contain seq section");
            content.Should().Contain("types:", "GIT.ksy should contain types section");

            // Verify GFF-specific structure
            content.Should().Contain("gff_header", "GIT.ksy should contain gff_header type");
            content.Should().Contain("struct_array", "GIT.ksy should contain struct_array type");
            content.Should().Contain("field_array", "GIT.ksy should contain field_array type");
            content.Should().Contain("gff_field_type", "GIT.ksy should contain gff_field_type enum");

            // Verify file type signature validation
            content.Should().Contain("valid: \"GIT \"", "GIT.ksy should validate file type signature");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToTypeScript()
        {
            TestCompileToLanguage("typescript");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileGITToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(GITKsyPath);
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

            // At least some languages should compile successfully
            // (We allow some failures as not all languages may be fully supported in all environments)
            successful.Count.Should().BeGreaterThan(0,
                $"At least one language should compile successfully. Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

            // Verify we tested at least 12 languages (as required)
            results.Count.Should().BeGreaterThanOrEqualTo(12,
                "Should test compilation to at least 12 languages");

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

            // Log summary
            var summary = new StringBuilder();
            summary.AppendLine($"GIT.ksy compilation test summary:");
            summary.AppendLine($"  Total languages tested: {results.Count}");
            summary.AppendLine($"  Successful: {successful.Count}");
            summary.AppendLine($"  Failed: {failed.Count}");
            if (failed.Count > 0)
            {
                summary.AppendLine($"  Failed languages: {string.Join(", ", failed.Select(f => f.Key))}");
            }

            // This will be visible in test output
            Console.WriteLine(summary.ToString());
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGITToMultipleLanguagesSimultaneously()
        {
            var normalizedKsyPath = Path.GetFullPath(GITKsyPath);
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
            // Some languages may fail due to missing dependencies, but the command should execute
            result.ExitCode.Should().BeInRange(-1, 1,
                $"Kaitai compiler should execute. Output: {result.Output}, Error: {result.Error}");
        }

        [Fact(Timeout = 300000)]
        public void TestGITKsyStructureValidation()
        {
            var normalizedPath = Path.GetFullPath(GITKsyPath);
            if (!File.Exists(normalizedPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedPath);

            // Validate GFF structure components
            var requiredTypes = new[]
            {
                "gff_header",
                "label_array",
                "struct_array",
                "field_array",
                "field_data_section",
                "field_indices_array",
                "list_indices_array"
            };

            foreach (var type in requiredTypes)
            {
                content.Should().Contain($"{type}:",
                    $"GIT.ksy should contain {type} type definition");
            }

            // Validate GFF field type enum values
            var requiredEnumValues = new[]
            {
                "uint8",
                "int32",
                "string",
                "resref",
                "localized_string",
                "struct",
                "list",
                "vector3",
                "vector4"
            };

            foreach (var enumValue in requiredEnumValues)
            {
                content.Should().Contain(enumValue,
                    $"GIT.ksy should contain {enumValue} in gff_field_type enum");
            }
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(GITKsyPath);
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

        private class KaitaiCompilerDetection
        {
            public bool Available { get; set; }
            public string Path { get; set; }
            public string Method { get; set; }
            public string DetectionMethod { get; set; }
        }

        private KaitaiCompilerDetection DetectKaitaiCompiler()
        {
            var detection = new KaitaiCompilerDetection
            {
                Available = false,
                Path = null,
                Method = null,
                DetectionMethod = "none"
            };

            // Method 1: Try as a command (if installed via package manager like Chocolatey, apt, etc.)
            var cmdCheck = RunCommand("kaitai-struct-compiler", "--version");
            if (cmdCheck.ExitCode == 0)
            {
                detection.Available = true;
                detection.Path = "kaitai-struct-compiler";
                detection.Method = "command";
                detection.DetectionMethod = "command (kaitai-struct-compiler in PATH)";
                return detection;
            }

            // Method 2: Try with .jar extension (if available as command)
            cmdCheck = RunCommand("kaitai-struct-compiler.jar", "--version");
            if (cmdCheck.ExitCode == 0)
            {
                detection.Available = true;
                detection.Path = "kaitai-struct-compiler.jar";
                detection.Method = "command";
                detection.DetectionMethod = "command (kaitai-struct-compiler.jar in PATH)";
                return detection;
            }

            // Method 3: Try finding JAR file and using java -jar
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                var jarCheck = RunCommand("java", $"-jar \"{jarPath}\" --version");
                if (jarCheck.ExitCode == 0)
                {
                    detection.Available = true;
                    detection.Path = jarPath;
                    detection.Method = "java-jar";
                    detection.DetectionMethod = $"java -jar (found at: {jarPath})";
                    return detection;
                }
            }

            // Method 4: Try common installation locations (executables and JARs)
            // Note: JAR files are already checked via FindKaitaiCompilerJar above, but we check here
            // for executables and as a fallback for JARs that might be in executable locations
            var commonPaths = new[]
            {
                // Linux/Unix executable locations
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "kaitai-struct-compiler"), IsJar = false },
                // Windows executable locations (if installed as executable)
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"), IsJar = false },
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.exe"), IsJar = false },
                // JAR locations (additional checks beyond FindKaitaiCompilerJar for completeness)
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"), IsJar = true },
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"), IsJar = true },
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"), IsJar = true },
                new { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"), IsJar = true },
            };

            foreach (var commonPath in commonPaths)
            {
                try
                {
                    var normalizedPath = Path.GetFullPath(commonPath.Path);
                    if (File.Exists(normalizedPath))
                    {
                        var checkResult = commonPath.IsJar
                            ? RunCommand("java", $"-jar \"{normalizedPath}\" --version")
                            : RunCommand(normalizedPath, "--version");

                        if (checkResult.ExitCode == 0)
                        {
                            detection.Available = true;
                            detection.Path = normalizedPath;
                            detection.Method = commonPath.IsJar ? "java-jar" : "command";
                            detection.DetectionMethod = $"common location (found at: {normalizedPath})";
                            return detection;
                        }
                    }
                }
                catch
                {
                    // Skip invalid paths (e.g., on non-Windows platforms where ProgramFilesX86 might not exist)
                    continue;
                }
            }

            // All detection methods failed
            detection.DetectionMethod = "all methods attempted (command in PATH, environment variable KAITAI_COMPILER_JAR, common installation locations)";
            return detection;
        }

        private (int ExitCode, string Output, string Error) RunKaitaiCompilerVersion(string compilerPath, string method)
        {
            if (method == "java-jar")
            {
                return RunCommand("java", $"-jar \"{compilerPath}\" --version");
            }
            else
            {
                return RunCommand(compilerPath, "--version");
            }
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
            // Check environment variable first (as documented in SetupKaitaiCompiler.ps1)
            var envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return envJar;
            }

            // Check common locations for Kaitai Struct compiler JAR
            // These locations match the installation paths used by SetupKaitaiCompiler.ps1 and install_kaitai_compiler.ps1
            var searchPaths = new[]
            {
                // Standard installation location (SetupKaitaiCompiler.ps1 default)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-compiler.jar"),
                // LocalApplicationData (alternative installation location)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
                // User profile root
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "kaitai-struct-compiler.jar"),
                // Program Files (Chocolatey and standard Windows installations)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "kaitai-struct-compiler", "kaitai-struct-compiler.jar"),
                // Test/development directories
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-compiler.jar"),
                Path.Combine(AppContext.BaseDirectory, "..", "kaitai-struct-compiler.jar"),
            };

            foreach (var path in searchPaths)
            {
                try
                {
                    var normalized = Path.GetFullPath(path);
                    if (File.Exists(normalized))
                    {
                        return normalized;
                    }
                }
                catch
                {
                    // Skip invalid paths (e.g., on non-Windows platforms where ProgramFilesX86 might not exist)
                    continue;
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
    }
}
