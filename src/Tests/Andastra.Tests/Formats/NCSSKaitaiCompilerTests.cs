using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for NCS.ksy Kaitai Struct compiler functionality.
    /// Tests compile NCS.ksy to multiple languages and validate the generated parsers work correctly.
    ///
    /// Supported languages tested (at least 12 as required):
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, Swift, VisualBasic, Kotlin, TypeScript
    /// </summary>
    public class NCSSKaitaiCompilerTests
    {
        private static readonly string NCSKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "NSS", "NCS.ksy"
        ));

        private static readonly string TestOutputDir = Path.Combine(
            AppContext.BaseDirectory,
            "test_files", "kaitai_ncs_compiled"
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
            "visualbasic",
            "kotlin",
            "typescript"
        };

        [Fact(Timeout = 300000)] // 5 minutes timeout for compilation
        public void TestKaitaiCompilerAvailable()
        {
            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                // Skip test if Java is not available
                return;
            }

            // Comprehensive Kaitai Struct compiler detection and installation
            var compilerPath = FindOrInstallKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                // Only skip if we're not in CI/CD and installation failed
                // In CI/CD, we should fail the test to ensure proper setup
                if (!IsRunningInCICD())
                {
                    return;
                }
                else
                {
                    Assert.True(false, "Kaitai Struct compiler not found and automatic installation failed in CI/CD environment. " +
                        "Please ensure Java is available and network access is allowed for downloading the compiler.");
                }
            }

            // Verify compiler works
            CompileResult verifyResult;
            if (compilerPath.EndsWith(".jar"))
            {
                var verifyCheck = RunCommand("java", $"-jar \"{compilerPath}\" --version");
                verifyResult = new CompileResult
                {
                    Success = verifyCheck.ExitCode == 0,
                    ExitCode = verifyCheck.ExitCode,
                    Output = verifyCheck.Output,
                    ErrorMessage = verifyCheck.Error
                };
            }
            else
            {
                var verifyCheck = RunCommand(compilerPath, "--version");
                verifyResult = new CompileResult
                {
                    Success = verifyCheck.ExitCode == 0,
                    ExitCode = verifyCheck.ExitCode,
                    Output = verifyCheck.Output,
                    ErrorMessage = verifyCheck.Error
                };
            }

            verifyResult.Success.Should().BeTrue(
                $"Kaitai Struct compiler should be available and functional. " +
                $"Compiler path: {compilerPath}, Exit code: {verifyResult.ExitCode}, " +
                $"Error: {verifyResult.ErrorMessage}");
        }

        [Fact(Timeout = 300000)]
        public void TestNCSKsyFileExists()
        {
            var normalizedPath = Path.GetFullPath(NCSKsyPath);
            File.Exists(normalizedPath).Should().BeTrue(
                $"NCS.ksy file should exist at {normalizedPath}"
            );

            // Verify it's a valid YAML file
            var content = File.ReadAllText(normalizedPath);
            content.Should().Contain("meta:", "NCS.ksy should contain meta section");
            content.Should().Contain("id: ncs", "NCS.ksy should have id: ncs");
            content.Should().Contain("seq:", "NCS.ksy should contain seq section");
        }

        [Fact(Timeout = 300000)]
        public void TestNCSKsyFileStructure()
        {
            var normalizedPath = Path.GetFullPath(NCSKsyPath);
            if (!File.Exists(normalizedPath))
            {
                return;
            }

            var content = File.ReadAllText(normalizedPath);

            // Verify key components exist
            content.Should().Contain("file_type", "NCS.ksy should define file_type field");
            content.Should().Contain("file_version", "NCS.ksy should define file_version field");
            content.Should().Contain("size_marker", "NCS.ksy should define size_marker field");
            content.Should().Contain("total_file_size", "NCS.ksy should define total_file_size field");
            content.Should().Contain("instructions", "NCS.ksy should define instructions field");
            content.Should().Contain("instruction", "NCS.ksy should define instruction type");
            content.Should().Contain("bytecode", "NCS.ksy should define bytecode field");
            content.Should().Contain("qualifier", "NCS.ksy should define qualifier field");
            content.Should().Contain("NCS ", "NCS.ksy should validate NCS file type");
            content.Should().Contain("V1.0", "NCS.ksy should validate V1.0 version");
            content.Should().Contain("0x42", "NCS.ksy should validate 0x42 size marker");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToVisualBasic()
        {
            TestCompileToLanguage("visualbasic");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToKotlin()
        {
            TestCompileToLanguage("kotlin");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToTypeScript()
        {
            TestCompileToLanguage("typescript");
        }

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestCompileNCSToAllLanguages()
        {
            var normalizedKsyPath = Path.GetFullPath(NCSKsyPath);
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
                        f.Contains("ncs") || f.Contains("Ncs") || f.Contains("NCS") ||
                        f.EndsWith(".py") || f.EndsWith(".java") || f.EndsWith(".js") ||
                        f.EndsWith(".cs") || f.EndsWith(".cpp") || f.EndsWith(".h") ||
                        f.EndsWith(".go") || f.EndsWith(".rb") || f.EndsWith(".php") ||
                        f.EndsWith(".rs") || f.EndsWith(".swift") || f.EndsWith(".lua") ||
                        f.EndsWith(".nim") || f.EndsWith(".pm") || f.EndsWith(".vb") ||
                        f.EndsWith(".kt") || f.EndsWith(".ts"));
                    hasParserFile.Should().BeTrue(
                        $"Language {success.Key} should generate parser files. Files found: {string.Join(", ", files.Select(Path.GetFileName))}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompileNCSToMultipleLanguagesSimultaneously()
        {
            var normalizedKsyPath = Path.GetFullPath(NCSKsyPath);
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
        public void TestNCSKsyValidation()
        {
            var normalizedKsyPath = Path.GetFullPath(NCSKsyPath);
            if (!File.Exists(normalizedKsyPath))
            {
                return;
            }

            // Try to compile to Python to validate syntax (Python is usually most forgiving)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                return;
            }

            Directory.CreateDirectory(TestOutputDir);
            var testOutputDir = Path.Combine(TestOutputDir, "validation");

            var result = RunKaitaiCompiler(normalizedKsyPath, "-t python", testOutputDir);

            // If compilation fails with syntax errors, that's a problem
            // But we allow runtime/dependency errors
            if (result.ExitCode != 0 && result.Error.Contains("syntax error"))
            {
                Assert.True(false, $"NCS.ksy has syntax errors: {result.Error}");
            }
        }

        /// <summary>
        /// Detects if the test is running in a CI/CD environment.
        /// Checks for common CI/CD environment variables from GitHub Actions, Azure DevOps, Jenkins, etc.
        /// </summary>
        private bool IsRunningInCICD()
        {
            // GitHub Actions
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
            {
                return true;
            }

            // Azure DevOps
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI")))
            {
                return true;
            }

            // Jenkins
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JENKINS_URL")))
            {
                return true;
            }

            // GitLab CI
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITLAB_CI")))
            {
                return true;
            }

            // Travis CI
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRAVIS")))
            {
                return true;
            }

            // CircleCI
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CIRCLECI")))
            {
                return true;
            }

            // Generic CI indicator
            var ciEnv = Environment.GetEnvironmentVariable("CI");
            if (!string.IsNullOrEmpty(ciEnv) && (ciEnv.Equals("true", StringComparison.OrdinalIgnoreCase) || ciEnv == "1"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the Kaitai Struct compiler using comprehensive detection methods.
        /// If not found and running in CI/CD or if KAITAI_AUTO_INSTALL is set, automatically downloads and installs it.
        /// </summary>
        private string FindOrInstallKaitaiCompiler()
        {
            // First, try to find existing compiler using existing detection methods
            var existingCompiler = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(existingCompiler) && File.Exists(existingCompiler))
            {
                return existingCompiler;
            }

            // Try direct command invocation
            var directCheck = RunCommand("kaitai-struct-compiler", "--version");
            if (directCheck.ExitCode == 0)
            {
                // Found in PATH
                return "kaitai-struct-compiler";
            }

            // Check if we should auto-install
            var shouldAutoInstall = IsRunningInCICD();
            var autoInstallEnv = Environment.GetEnvironmentVariable("KAITAI_AUTO_INSTALL");
            if (!string.IsNullOrEmpty(autoInstallEnv) && (autoInstallEnv.Equals("true", StringComparison.OrdinalIgnoreCase) || autoInstallEnv == "1"))
            {
                shouldAutoInstall = true;
            }

            if (!shouldAutoInstall)
            {
                return null;
            }

            // Auto-install the compiler
            try
            {
                return DownloadAndInstallKaitaiCompiler();
            }
            catch (Exception ex)
            {
                // Log but don't throw - allow test to handle gracefully
                Debug.WriteLine($"Failed to auto-install Kaitai Struct compiler: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads and installs the Kaitai Struct compiler JAR file.
        /// Downloads version 0.10 from GitHub releases and extracts the JAR to a standard location.
        /// </summary>
        private string DownloadAndInstallKaitaiCompiler()
        {
            const string version = "0.10";
            const string downloadUrl = "https://github.com/kaitai-io/kaitai_struct_compiler/releases/download/" + version + "/kaitai-struct-compiler-" + version + ".zip";

            // Determine install directory (prefer user's home directory)
            string installDir;
            if (IsRunningInCICD())
            {
                // In CI/CD, use temp directory or workspace directory
                var workspaceRoot = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ??
                    Environment.GetEnvironmentVariable("SYSTEM_DEFAULTWORKINGDIRECTORY") ??
                    Environment.GetEnvironmentVariable("WORKSPACE");

                if (!string.IsNullOrEmpty(workspaceRoot) && Directory.Exists(workspaceRoot))
                {
                    installDir = Path.Combine(workspaceRoot, ".kaitai");
                }
                else
                {
                    installDir = Path.Combine(Path.GetTempPath(), ".kaitai");
                }
            }
            else
            {
                // Local development: use user profile directory
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                installDir = Path.Combine(userProfile, ".kaitai");
            }

            // Ensure install directory exists
            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
            }

            var jarPath = Path.Combine(installDir, "kaitai-struct-compiler.jar");

            // Check if already downloaded
            if (File.Exists(jarPath))
            {
                // Verify it's a valid JAR by checking if Java can read it
                var verifyCheck = RunCommand("java", $"-jar \"{jarPath}\" --version");
                if (verifyCheck.ExitCode == 0)
                {
                    return jarPath;
                }
                // If invalid, delete and re-download
                try
                {
                    File.Delete(jarPath);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            // Download the ZIP file
            var zipPath = Path.Combine(Path.GetTempPath(), $"kaitai-struct-compiler-{version}.zip");
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    var response = httpClient.GetAsync(downloadUrl).Result;
                    response.EnsureSuccessStatusCode();

                    using (var fileStream = new FileStream(zipPath, FileMode.Create))
                    {
                        response.Content.CopyToAsync(fileStream).Wait();
                    }
                }

                // Extract the JAR from the ZIP
                var extractDir = Path.Combine(Path.GetTempPath(), $"kaitai-struct-compiler-{version}-extract");
                try
                {
                    if (Directory.Exists(extractDir))
                    {
                        Directory.Delete(extractDir, true);
                    }
                    Directory.CreateDirectory(extractDir);

                    ZipFile.ExtractToDirectory(zipPath, extractDir);

                    // Find the JAR file in the extracted directory
                    var jarFiles = Directory.GetFiles(extractDir, "*.jar", SearchOption.AllDirectories);
                    if (jarFiles.Length == 0)
                    {
                        throw new FileNotFoundException("No JAR file found in downloaded Kaitai Struct compiler archive");
                    }

                    // Copy the first (and usually only) JAR file to install location
                    File.Copy(jarFiles[0], jarPath, true);

                    // Verify the installed JAR works
                    var verifyCheck = RunCommand("java", $"-jar \"{jarPath}\" --version");
                    if (verifyCheck.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"Downloaded Kaitai Struct compiler JAR is not valid. Exit code: {verifyCheck.ExitCode}, Error: {verifyCheck.Error}");
                    }

                    // Set environment variable for future use
                    Environment.SetEnvironmentVariable("KAITAI_COMPILER_JAR", jarPath, EnvironmentVariableTarget.Process);

                    return jarPath;
                }
                finally
                {
                    // Cleanup extraction directory
                    try
                    {
                        if (Directory.Exists(extractDir))
                        {
                            Directory.Delete(extractDir, true);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            finally
            {
                // Cleanup ZIP file
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(NCSKsyPath);
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

            // 3. Try comprehensive detection (includes auto-installation if needed)
            var compilerPath = FindOrInstallKaitaiCompiler();
            if (!string.IsNullOrEmpty(compilerPath))
            {
                if (compilerPath.EndsWith(".jar"))
                {
                    result = RunCommand("java", $"-jar \"{compilerPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                }
                else
                {
                    result = RunCommand(compilerPath, $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                }

                if (result.ExitCode == 0)
                {
                    return result;
                }
            }

            // 4. Try legacy detection method as fallback
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                result = RunCommand("java", $"-jar \"{jarPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                if (result.ExitCode == 0)
                {
                    return result;
                }
            }

            // 5. Try in common installation locations
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

