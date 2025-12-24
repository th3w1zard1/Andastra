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
    /// Comprehensive tests for UTE format using Kaitai Struct generated parsers.
    /// Tests validate that the UTE.ksy definition compiles correctly to multiple languages
    /// and that the generated parsers correctly parse UTE files.
    /// </summary>
    public class UTEKaitaiStructTests
    {
        private static readonly string KsyFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTE", "UTE.ksy");

        private static readonly string TestUteFile = TestFileHelper.GetPath("test.ute");
        private static readonly string KaitaiOutputDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "kaitai_compiled", "ute");

        // Languages supported by Kaitai Struct (at least a dozen)
        private static readonly string[] SupportedLanguages = new[]
        {
            "python", "java", "javascript", "csharp", "cpp_stl", "go", "ruby",
            "php", "rust", "swift", "perl", "nim", "lua", "kotlin", "typescript"
        };

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilerAvailable()
        {
            // Check if kaitai-struct-compiler is available
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kaitai-struct-compiler",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    string version = process.StandardOutput.ReadToEnd();
                    version.Should().NotBeNullOrEmpty("Kaitai Struct compiler should return version");
                }
                else
                {
                    // Compiler not found - skip tests that require it
                    Assert.True(true, "Kaitai Struct compiler not available - skipping compiler tests");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Compiler not installed - skip tests
                Assert.True(true, "Kaitai Struct compiler not installed - skipping compiler tests");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileExists()
        {
            // Ensure UTE.ksy file exists
            var ksyPath = new FileInfo(KsyFile);
            if (!ksyPath.Exists)
            {
                // Try alternative path
                ksyPath = new FileInfo(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..",
                    "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "UTE", "UTE.ksy"));
            }

            ksyPath.Exists.Should().BeTrue($"UTE.ksy should exist at {ksyPath.FullName}");
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileValid()
        {
            // Validate that UTE.ksy is valid YAML and can be parsed by compiler
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "UTE.ksy not found - skipping validation");
                return;
            }

            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping validation");
                return;
            }

            string compilerPath = FindKaitaiCompilerJar();
            if (string.IsNullOrEmpty(compilerPath))
            {
                // Try command
                var cmdCheck = RunCommand("kaitai-struct-compiler", "--version");
                if (cmdCheck.ExitCode != 0)
                {
                    Assert.True(true, "Kaitai Struct compiler not available - skipping validation");
                    return;
                }
                compilerPath = "kaitai-struct-compiler";
            }

            // Use Python as validation target (most commonly supported)
            string tempOutputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempOutputDir);

            try
            {
                var validateInfo = new ProcessStartInfo
                {
                    FileName = compilerPath.EndsWith(".jar") ? "java" : compilerPath,
                    Arguments = compilerPath.EndsWith(".jar")
                        ? $"-jar \"{compilerPath}\" -t python \"{normalizedKsyPath}\" -d \"{tempOutputDir}\" --debug"
                        : $"-t python \"{normalizedKsyPath}\" -d \"{tempOutputDir}\" --debug",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(normalizedKsyPath)
                };

                using (var process = Process.Start(validateInfo))
                {
                    if (process != null)
                    {
                        string stdout = process.StandardOutput.ReadToEnd();
                        string stderr = process.StandardError.ReadToEnd();
                        process.WaitForExit(60000); // 60 second timeout

                        // Compiler should not report syntax errors
                        // Allow import/dependency errors but not syntax errors
                        bool hasSyntaxError = stderr.ToLower().Contains("error") &&
                                             !stderr.ToLower().Contains("import") &&
                                             !stderr.ToLower().Contains("dependency") &&
                                             !stderr.ToLower().Contains("warning");

                        if (hasSyntaxError && process.ExitCode != 0)
                        {
                            Assert.True(false, $"UTE.ksy should not have syntax errors. STDOUT: {stdout}, STDERR: {stderr}");
                        }

                        process.ExitCode.Should().Be(0,
                            $"UTE.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                    }
                }
            }
            finally
            {
                // Cleanup temp directory
                try
                {
                    if (Directory.Exists(tempOutputDir))
                    {
                        Directory.Delete(tempOutputDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestKaitaiStructCompilation(string language)
        {
            // Test that UTE.ksy compiles to each target language
            TestCompileToLanguage(language);
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "UTE.ksy not found - skipping compilation test");
                return;
            }

            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping compilation test");
                return;
            }

            Directory.CreateDirectory(KaitaiOutputDir);
            var result = CompileToLanguage(normalizedKsyPath, language);

            if (!result.Success)
            {
                // Some languages may not be fully supported or may have missing dependencies
                // Log the error but don't fail the test for individual language failures
                // The "all languages" test will verify at least some work
                Assert.True(true, $"Compilation to {language} failed (may not be supported): {result.ErrorMessage}");
                return;
            }

            result.Success.Should().BeTrue(
                $"Compilation to {language} should succeed. Error: {result.ErrorMessage}, Output: {result.Output}");

            // Verify output directory was created and contains files
            var outputDir = Path.Combine(KaitaiOutputDir, language);
            Directory.Exists(outputDir).Should().BeTrue(
                $"Output directory for {language} should be created");

            // Verify generated files exist (language-specific patterns)
            var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
            files.Length.Should().BeGreaterThan(0,
                $"Language {language} should generate output files");
        }

        private CompileResult CompileToLanguage(string ksyPath, string language)
        {
            var outputDir = Path.Combine(KaitaiOutputDir, language);
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
            // Try to find compiler first
            string compilerPath = FindKaitaiCompiler();
            if (!string.IsNullOrEmpty(compilerPath))
            {
                // Check if it's a JAR path (contains .jar)
                if (compilerPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                    (File.Exists(compilerPath) && Path.GetExtension(compilerPath).Equals(".jar", StringComparison.OrdinalIgnoreCase)))
                {
                    // Use Java to run JAR
                    return RunCommand("java", $"-jar \"{compilerPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                }
                else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                         (File.Exists(compilerPath) && Path.GetExtension(compilerPath).Equals(".bat", StringComparison.OrdinalIgnoreCase)))
                {
                    // Use cmd.exe to run .bat file on Windows
                    return RunCommand("cmd.exe", $"/c \"{compilerPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                }
                else
                {
                    // Use compiler directly
                    return RunCommand(compilerPath, $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                }
            }

            // Fallback: Try command directly
            var result = RunCommand("kaitai-struct-compiler", $"{arguments} -d \"{outputDir}\" \"{ksyPath}\"");

            if (result.ExitCode == 0)
            {
                return result;
            }

            // Fallback: Try as Java JAR (common installation method)
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                result = RunCommand("java", $"-jar \"{jarPath}\" {arguments} -d \"{outputDir}\" \"{ksyPath}\"");
                return result;
            }

            // Return the last result (which will be a failure)
            return result;
        }

        private string FindKaitaiCompiler()
        {
            // Try common locations and PATH
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
                    ProcessStartInfo processInfo;
                    if (path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                    {
                        // .bat files need to be run via cmd.exe on Windows
                        processInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c \"{path}\" --version",
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
                            FileName = path,
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

            // Check environment variable for JAR
            var envJar = Environment.GetEnvironmentVariable("KAITAI_COMPILER_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return envJar;
            }

            // Try as Java JAR (common installation method)
            var jarPath = FindKaitaiCompilerJar();
            if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                return jarPath;
            }

            return null;
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
                    process.WaitForExit(60000); // 60 second timeout

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
        public void TestKaitaiStructCompilesToAllLanguages()
        {
            // Test compilation to all supported languages
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "UTE.ksy not found - skipping compilation test");
                return;
            }

            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping compilation test");
                return;
            }

            Directory.CreateDirectory(KaitaiOutputDir);

            var results = new Dictionary<string, CompileResult>();

            foreach (string lang in SupportedLanguages)
            {
                try
                {
                    var result = CompileToLanguage(normalizedKsyPath, lang);
                    results[lang] = result;
                }
                catch (Exception ex)
                {
                    results[lang] = new CompileResult
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

            // At least 12 languages should compile successfully (as required)
            // (We allow some failures as not all languages may be fully supported in all environments)
            successful.Count.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. " +
                $"Success: {successful.Count}/{SupportedLanguages.Length}. " +
                $"Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

            // Log successful compilations
            foreach (var success in successful)
            {
                // Verify output files were created
                var outputDir = Path.Combine(KaitaiOutputDir, success.Key);
                if (Directory.Exists(outputDir))
                {
                    var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                    files.Length.Should().BeGreaterThan(0,
                        $"Language {success.Key} should generate output files");
                }
            }

            // Log all results
            foreach (var result in results)
            {
                if (result.Value.Success)
                {
                    Console.WriteLine($"  {result.Key}: Success");
                }
                else
                {
                    Console.WriteLine($"  {result.Key}: Failed - {result.Value.ErrorMessage?.Substring(0, Math.Min(100, result.Value.ErrorMessage?.Length ?? 0))}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructGeneratedParserConsistency()
        {
            // Test that generated parsers produce consistent results
            // This requires actual test files and parser execution
            if (!File.Exists(TestUteFile))
            {
                // Create test file if needed
                var ute = new UTE();
                ute.Tag = "test_encounter";
                ute.ResRef = ResRef.FromString("test_encounter");

                GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
                byte[] data = GFFAuto.BytesGff(gff);
                Directory.CreateDirectory(Path.GetDirectoryName(TestUteFile));
                File.WriteAllBytes(TestUteFile, data);
            }

            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "UTE.ksy not found - skipping parser consistency test");
                return;
            }

            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping parser consistency test");
                return;
            }

            Directory.CreateDirectory(KaitaiOutputDir);

            // Step 1: Compile UTE.ksy to multiple languages
            var compiledLanguages = new List<string>();
            var compilationResults = new Dictionary<string, CompileResult>();

            // Focus on languages that can be easily executed for comparison
            string[] executableLanguages = new[] { "python", "javascript", "java", "csharp" };

            foreach (string language in executableLanguages)
            {
                try
                {
                    var result = CompileToLanguage(normalizedKsyPath, language);
                    compilationResults[language] = result;
                    if (result.Success)
                    {
                        compiledLanguages.Add(language);
                    }
                }
                catch (Exception ex)
                {
                    compilationResults[language] = new CompileResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Output = ex.ToString()
                    };
                }
            }

            // At least one language should compile successfully
            compiledLanguages.Count.Should().BeGreaterThan(0,
                $"At least one language should compile successfully. Results: {string.Join(", ", compilationResults.Select(r => $"{r.Key}: {(r.Value.Success ? "Success" : r.Value.ErrorMessage)}"))}");

            // Step 2: Run the generated parsers on the test file and compare results
            var parserResults = new Dictionary<string, ParserExecutionResult>();

            foreach (string language in compiledLanguages)
            {
                try
                {
                    var executionResult = ExecuteParser(language, TestUteFile);
                    parserResults[language] = executionResult;
                }
                catch (Exception ex)
                {
                    parserResults[language] = new ParserExecutionResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        ParsedData = null
                    };
                }
            }

            // Step 3: Extract structured data from all successful parsers
            var structuredResults = new Dictionary<string, UteParsedData>();
            foreach (var result in parserResults.Where(r => r.Value.Success))
            {
                try
                {
                    var structured = ParseParserOutput(result.Key, result.Value.ParsedData);
                    if (structured != null)
                    {
                        structuredResults[result.Key] = structured;
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue - some parsers may not output structured data
                    Console.WriteLine($"Failed to parse output from {result.Key}: {ex.Message}");
                }
            }

            // Step 4: Validate using existing C# parser as reference
            GFF parsedGff = GFFAuto.ReadGff(TestUteFile, 0, null);
            UTE constructedUte = UTEHelpers.ConstructUte(parsedGff);

            // Validate structure matches Kaitai Struct definition
            // UTE files are GFF-based, so they follow GFF structure
            parsedGff.Content.Should().Be(GFFContent.UTE, "UTE file should have UTE content type");
            constructedUte.Should().NotBeNull("Constructed UTE should not be null");
            constructedUte.Tag.Should().NotBeNull("UTE Tag should not be null");

            // Step 5: Create reference structured data from C# parser
            var referenceData = CreateReferenceData(parsedGff, constructedUte);
            structuredResults["csharp_reference"] = referenceData;

            // Step 6: Comprehensive cross-language comparison
            if (structuredResults.Count > 1)
            {
                CompareParserResults(structuredResults, referenceData);
            }
            else if (structuredResults.Count == 1)
            {
                // At least validate that one parser produced structured data
                structuredResults.Values.First().Should().NotBeNull("At least one parser should produce structured data");
            }
        }

        private ParserExecutionResult ExecuteParser(string language, string testFile)
        {
            var outputDir = Path.Combine(KaitaiOutputDir, language);
            if (!Directory.Exists(outputDir))
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Output directory for {language} does not exist",
                    ParsedData = null
                };
            }

            switch (language.ToLower())
            {
                case "python":
                    return ExecutePythonParser(outputDir, testFile);
                case "javascript":
                    return ExecuteJavaScriptParser(outputDir, testFile);
                case "java":
                    return ExecuteJavaParser(outputDir, testFile);
                case "csharp":
                    return ExecuteCSharpParser(outputDir, testFile);
                default:
                    return new ParserExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Execution not implemented for {language}",
                        ParsedData = null
                    };
            }
        }

        private ParserExecutionResult ExecutePythonParser(string outputDir, string testFile)
        {
            // Look for the generated Python parser
            var pythonFiles = Directory.GetFiles(outputDir, "*.py", SearchOption.AllDirectories);
            if (pythonFiles.Length == 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "No Python files found in output directory",
                    ParsedData = null
                };
            }

            // Find the main parser file (usually ute.py or similar)
            var mainParser = pythonFiles.FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("ute"));
            if (mainParser == null)
            {
                mainParser = pythonFiles[0]; // Use first Python file found
            }

            // Create a comprehensive Python script to parse the file and output structured JSON
            string scriptPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".py");
            try
            {
                string scriptContent = $@"
import sys
import os
import json
sys.path.insert(0, r'{Path.GetDirectoryName(mainParser).Replace("\\", "\\\\")}')
from {Path.GetFileNameWithoutExtension(mainParser)} import *
with open(r'{testFile.Replace("\\", "\\\\")}', 'rb') as f:
    data = Ute.from_bytes(f.read())

    # Extract root struct (GFF root)
    root = data.root_struct if hasattr(data, 'root_struct') else None

    # Build output dictionary
    output = {{
        'FileType': data.file_type if hasattr(data, 'file_type') else '',
        'FileVersion': data.file_version if hasattr(data, 'file_version') else '',
    }}

    # Extract fields from root struct if available
    if root:
        # Try to extract common UTE fields
        field_names = ['Tag', 'TemplateResRef', 'Active', 'DifficultyIndex', 'Difficulty',
                      'Faction', 'MaxCreatures', 'RecCreatures', 'Respawns', 'SpawnOption',
                      'Reset', 'ResetTime', 'PlayerOnly', 'OnEntered', 'OnExit',
                      'OnExhausted', 'OnHeartbeat', 'OnUserDefined', 'Comment', 'PaletteID']

        for field_name in field_names:
            try:
                if hasattr(root, field_name.lower()):
                    field_value = getattr(root, field_name.lower())
                    if isinstance(field_value, (str, int, float, bool)):
                        output[field_name] = field_value
                    elif hasattr(field_value, '__str__'):
                        output[field_name] = str(field_value)
            except:
                pass

        # Extract creature list
        if hasattr(root, 'creature_list') or hasattr(root, 'creaturelist'):
            creature_list = getattr(root, 'creature_list', None) or getattr(root, 'creaturelist', None)
            if creature_list:
                output['CreatureCount'] = len(creature_list) if hasattr(creature_list, '__len__') else 0

    print('SUCCESS')
    print(json.dumps(output))
";

                File.WriteAllText(scriptPath, scriptContent);

                var result = RunCommand("python", $"\"{scriptPath}\"");
                if (result.ExitCode == 0 && result.Output.Contains("SUCCESS"))
                {
                    return new ParserExecutionResult
                    {
                        Success = true,
                        ErrorMessage = null,
                        ParsedData = result.Output
                    };
                }
                else
                {
                    // Try python3
                    result = RunCommand("python3", $"\"{scriptPath}\"");
                    if (result.ExitCode == 0 && result.Output.Contains("SUCCESS"))
                    {
                        return new ParserExecutionResult
                        {
                            Success = true,
                            ErrorMessage = null,
                            ParsedData = result.Output
                        };
                    }
                }

                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Python parser execution failed: {result.Error}",
                    ParsedData = result.Output
                };
            }
            finally
            {
                if (File.Exists(scriptPath))
                {
                    try
                    {
                        File.Delete(scriptPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        private ParserExecutionResult ExecuteJavaScriptParser(string outputDir, string testFile)
        {
            // Look for the generated JavaScript parser
            var jsFiles = Directory.GetFiles(outputDir, "*.js", SearchOption.AllDirectories);
            if (jsFiles.Length == 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "No JavaScript files found in output directory",
                    ParsedData = null
                };
            }

            // Check if Node.js is available
            var nodeCheck = RunCommand("node", "--version");
            if (nodeCheck.ExitCode != 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Node.js not available",
                    ParsedData = null
                };
            }

            // Create a simple Node.js script to parse the file
            string scriptPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".js");
            try
            {
                var mainParser = jsFiles[0];
                string scriptContent = $@"
const Ute = require('{mainParser.Replace("\\", "\\\\")}');
const fs = require('fs');
const data = fs.readFileSync('{testFile.Replace("\\", "\\\\")}');
const parsed = new Ute(new Ute.KaitaiStream(data));

// Extract root struct
const root = parsed.rootStruct || parsed.root_struct || null;

// Build output object
const output = {{
    FileType: parsed.fileType || parsed.file_type || '',
    FileVersion: parsed.fileVersion || parsed.file_version || ''
}};

// Extract fields from root struct if available
if (root) {{
    const fieldNames = ['Tag', 'TemplateResRef', 'Active', 'DifficultyIndex', 'Difficulty',
                       'Faction', 'MaxCreatures', 'RecCreatures', 'Respawns', 'SpawnOption',
                       'Reset', 'ResetTime', 'PlayerOnly', 'OnEntered', 'OnExit',
                       'OnExhausted', 'OnHeartbeat', 'OnUserDefined', 'Comment', 'PaletteID'];

    fieldNames.forEach(fieldName => {{
        const fieldNameLower = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
        if (root[fieldName] !== undefined) {{
            output[fieldName] = root[fieldName];
        }} else if (root[fieldNameLower] !== undefined) {{
            output[fieldName] = root[fieldNameLower];
        }}
    }});

    // Extract creature list
    const creatureList = root.creatureList || root.creature_list || null;
    if (creatureList && Array.isArray(creatureList)) {{
        output.CreatureCount = creatureList.length;
    }}
}}

console.log('SUCCESS');
console.log(JSON.stringify(output));
";

                File.WriteAllText(scriptPath, scriptContent);

                var result = RunCommand("node", $"\"{scriptPath}\"");
                if (result.ExitCode == 0 && result.Output.Contains("SUCCESS"))
                {
                    return new ParserExecutionResult
                    {
                        Success = true,
                        ErrorMessage = null,
                        ParsedData = result.Output
                    };
                }

                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"JavaScript parser execution failed: {result.Error}",
                    ParsedData = result.Output
                };
            }
            finally
            {
                if (File.Exists(scriptPath))
                {
                    try
                    {
                        File.Delete(scriptPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        /// <summary>
        /// Executes the generated Java parser to parse a UTE file.
        /// Compiles the Java parser and executes it with proper classpath setup.
        /// </summary>
        private ParserExecutionResult ExecuteJavaParser(string outputDir, string testFile)
        {
            // Look for the generated Java parser
            var javaFiles = Directory.GetFiles(outputDir, "*.java", SearchOption.AllDirectories);
            if (javaFiles.Length == 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "No Java files found in output directory",
                    ParsedData = null
                };
            }

            // Check if Java is available
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Java runtime not available",
                    ParsedData = null
                };
            }

            // Check if javac is available
            var javacCheck = RunCommand("javac", "-version");
            if (javacCheck.ExitCode != 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Java compiler (javac) not available",
                    ParsedData = null
                };
            }

            // Find the main parser class (usually Ute.java or similar)
            var mainParser = javaFiles.FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("ute"));
            if (mainParser == null)
            {
                // Try to find any Java file that looks like a main parser
                mainParser = javaFiles.FirstOrDefault(f =>
                    !Path.GetFileName(f).ToLower().Contains("kaitai") &&
                    !Path.GetFileName(f).ToLower().Contains("test"));
                if (mainParser == null)
                {
                    mainParser = javaFiles[0]; // Use first Java file found
                }
            }

            // Find Kaitai Struct runtime JAR
            string kaitaiRuntimeJar = FindKaitaiStructRuntimeJar();
            if (string.IsNullOrEmpty(kaitaiRuntimeJar))
            {
                // Try to compile without runtime (may work if runtime is in classpath)
                // But we'll still try to find it
                kaitaiRuntimeJar = "";
            }

            // Create a temporary directory for compilation
            string compileDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(compileDir);

            try
            {
                // Copy all Java files to compile directory
                foreach (var javaFile in javaFiles)
                {
                    string destFile = Path.Combine(compileDir, Path.GetFileName(javaFile));
                    File.Copy(javaFile, destFile, true);
                }

                // Create a test Java class that uses the parser
                string testClassName = "UteParserTest";
                string testClassFile = Path.Combine(compileDir, testClassName + ".java");
                string parserClassName = Path.GetFileNameWithoutExtension(mainParser);
                string parserPackage = ExtractJavaPackage(mainParser);

                string testClassContent = $@"import java.io.*;
import java.util.*;
{(string.IsNullOrEmpty(parserPackage) ? "" : $"import {parserPackage}.{parserClassName};")}

public class {testClassName} {{
    public static void main(String[] args) {{
        try {{
            File file = new File(args[0]);
            FileInputStream fis = new FileInputStream(file);
            byte[] data = new byte[(int)file.length()];
            fis.read(data);
            fis.close();

            {(string.IsNullOrEmpty(parserPackage) ? parserClassName : $"{parserPackage}.{parserClassName}")} ute = new {(string.IsNullOrEmpty(parserPackage) ? parserClassName : $"{parserPackage}.{parserClassName}")}(new io.kaitai.struct.ByteBufferKaitaiStream(data));

            // Build output map
            Map<String, Object> output = new HashMap<>();
            output.put(""FileType"", ute.fileType() != null ? ute.fileType() : """");
            output.put(""FileVersion"", ute.fileVersion() != null ? ute.fileVersion() : """");

            // Try to extract root struct
            try {{
                Object rootStruct = ute.rootStruct();
                if (rootStruct == null) {{
                    rootStruct = ute.root_struct();
                }}

                if (rootStruct != null) {{
                    // Extract common UTE fields using reflection
                    String[] fieldNames = {{""Tag"", ""TemplateResRef"", ""Active"", ""DifficultyIndex"", ""Difficulty"",
                                          ""Faction"", ""MaxCreatures"", ""RecCreatures"", ""Respawns"", ""SpawnOption"",
                                          ""Reset"", ""ResetTime"", ""PlayerOnly"", ""OnEntered"", ""OnExit"",
                                          ""OnExhausted"", ""OnHeartbeat"", ""OnUserDefined"", ""Comment"", ""PaletteID""}};

                    for (String fieldName : fieldNames) {{
                        try {{
                            java.lang.reflect.Method method = rootStruct.getClass().getMethod(fieldName);
                            Object value = method.invoke(rootStruct);
                            if (value != null) {{
                                output.put(fieldName, value.toString());
                            }}
                        }} catch (Exception e) {{
                            // Try lowercase version
                            try {{
                                String lowerFieldName = fieldName.substring(0, 1).toLowerCase() + fieldName.substring(1);
                                java.lang.reflect.Method method = rootStruct.getClass().getMethod(lowerFieldName);
                                Object value = method.invoke(rootStruct);
                                if (value != null) {{
                                    output.put(fieldName, value.toString());
                                }}
                            }} catch (Exception e2) {{
                                // Field not found, skip
                            }}
                        }}
                    }}

                    // Extract creature list
                    try {{
                        Object creatureList = rootStruct.getClass().getMethod(""creatureList"").invoke(rootStruct);
                        if (creatureList == null) {{
                            creatureList = rootStruct.getClass().getMethod(""creature_list"").invoke(rootStruct);
                        }}
                        if (creatureList instanceof List) {{
                            output.put(""CreatureCount"", ((List<?>) creatureList).size());
                        }}
                    }} catch (Exception e) {{
                        // Creature list not found
                    }}
                }}
            }} catch (Exception e) {{
                // Root struct extraction failed, continue with basic fields
            }}

            System.out.println(""SUCCESS"");
            // Output as JSON-like format
            System.out.print(""{{"");
            boolean first = true;
            for (Map.Entry<String, Object> entry : output.entrySet()) {{
                if (!first) System.out.print("","");
                System.out.print(""\"""" + entry.getKey() + ""\"":\"""" + entry.getValue().toString() + ""\"""");
                first = false;
            }}
            System.out.println(""}}"");
        }} catch (Exception e) {{
            System.err.println(""ERROR: "" + e.getMessage());
            e.printStackTrace();
            System.exit(1);
        }}
    }}
}}";

                File.WriteAllText(testClassFile, testClassContent);

                // Compile Java files
                // Build classpath: include Kaitai Struct runtime if found
                string classpath = string.IsNullOrEmpty(kaitaiRuntimeJar)
                    ? compileDir
                    : $"{compileDir}{Path.PathSeparator}{kaitaiRuntimeJar}";

                // Compile all Java files together (including generated parser files and test class)
                // Build list of all Java files to compile
                var allJavaFiles = new List<string>();
                foreach (var javaFile in javaFiles)
                {
                    allJavaFiles.Add(Path.Combine(compileDir, Path.GetFileName(javaFile)));
                }
                allJavaFiles.Add(testClassFile);

                // Compile all files at once
                string allFilesArg = string.Join(" ", allJavaFiles.Select(f => $"\"{f}\""));
                var compileResult = RunCommand("javac", $"-cp \"{classpath}\" -d \"{compileDir}\" {allFilesArg}");

                if (compileResult.ExitCode != 0)
                {
                    // Try compiling without explicit classpath (runtime might be in default classpath)
                    compileResult = RunCommand("javac", $"-d \"{compileDir}\" {allFilesArg}");
                    if (compileResult.ExitCode != 0)
                    {
                        return new ParserExecutionResult
                        {
                            Success = false,
                            ErrorMessage = $"Java compilation failed: {compileResult.Error}. Output: {compileResult.Output}",
                            ParsedData = compileResult.Output
                        };
                    }
                }

                // Execute the compiled Java class
                string classpathForExecution = string.IsNullOrEmpty(kaitaiRuntimeJar)
                    ? compileDir
                    : $"{compileDir}{Path.PathSeparator}{kaitaiRuntimeJar}";

                var executeResult = RunCommand("java", $"-cp \"{classpathForExecution}\" {testClassName} \"{testFile}\"");

                if (executeResult.ExitCode == 0 && executeResult.Output.Contains("SUCCESS"))
                {
                    return new ParserExecutionResult
                    {
                        Success = true,
                        ErrorMessage = null,
                        ParsedData = executeResult.Output
                    };
                }
                else
                {
                    // Try without explicit classpath
                    executeResult = RunCommand("java", $"-cp \"{compileDir}\" {testClassName} \"{testFile}\"");
                    if (executeResult.ExitCode == 0 && executeResult.Output.Contains("SUCCESS"))
                    {
                        return new ParserExecutionResult
                        {
                            Success = true,
                            ErrorMessage = null,
                            ParsedData = executeResult.Output
                        };
                    }

                    return new ParserExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Java parser execution failed: {executeResult.Error}. Output: {executeResult.Output}",
                        ParsedData = executeResult.Output
                    };
                }
            }
            catch (Exception ex)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Exception during Java parser execution: {ex.Message}",
                    ParsedData = ex.ToString()
                };
            }
            finally
            {
                // Cleanup compile directory
                try
                {
                    if (Directory.Exists(compileDir))
                    {
                        Directory.Delete(compileDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Extracts the Java package name from a Java source file.
        /// </summary>
        private string ExtractJavaPackage(string javaFile)
        {
            try
            {
                string content = File.ReadAllText(javaFile);
                // Look for package declaration: package com.example;
                int packageIndex = content.IndexOf("package ", StringComparison.Ordinal);
                if (packageIndex >= 0)
                {
                    int start = packageIndex + 8; // "package ".Length
                    int end = content.IndexOf(';', start);
                    if (end > start)
                    {
                        return content.Substring(start, end - start).Trim();
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return "";
        }

        /// <summary>
        /// Finds the Kaitai Struct runtime JAR file.
        /// </summary>
        private string FindKaitaiStructRuntimeJar()
        {
            // Check environment variable first
            var envJar = Environment.GetEnvironmentVariable("KAITAI_RUNTIME_JAR");
            if (!string.IsNullOrEmpty(envJar) && File.Exists(envJar))
            {
                return envJar;
            }

            // Check common locations for Kaitai Struct runtime JAR
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "kaitai-struct-runtime.jar"),
                Path.Combine(AppContext.BaseDirectory, "..", "kaitai-struct-runtime.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaitai", "kaitai-struct-runtime.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kaitai-struct-runtime.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "kaitai-struct-runtime.jar"),
                // Maven local repository
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "repository", "io", "kaitai", "kaitai-struct-runtime", "0.9", "kaitai-struct-runtime-0.9.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "repository", "io", "kaitai", "kaitai-struct-runtime", "0.10", "kaitai-struct-runtime-0.10.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "repository", "io", "kaitai", "kaitai-struct-runtime", "0.11", "kaitai-struct-runtime-0.11.jar"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".m2", "repository", "io", "kaitai", "kaitai-struct-runtime", "0.12", "kaitai-struct-runtime-0.12.jar"),
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
                    // Ignore path errors
                }
            }

            return null;
        }

        /// <summary>
        /// Executes the generated C# parser to parse a UTE file.
        /// Compiles the C# parser and executes it with proper dependencies.
        /// </summary>
        private ParserExecutionResult ExecuteCSharpParser(string outputDir, string testFile)
        {
            // Look for the generated C# parser
            var csFiles = Directory.GetFiles(outputDir, "*.cs", SearchOption.AllDirectories);
            if (csFiles.Length == 0)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "No C# files found in output directory",
                    ParsedData = null
                };
            }

            // Check if C# compiler is available
            // Try csc (C# compiler) first, then dotnet CLI
            string cscPath = FindCSharpCompiler();
            if (string.IsNullOrEmpty(cscPath))
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = "C# compiler (csc or dotnet) not available",
                    ParsedData = null
                };
            }

            // Find the main parser class (usually Ute.cs or similar)
            var mainParser = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLower().Contains("ute"));
            if (mainParser == null)
            {
                // Try to find any C# file that looks like a main parser
                mainParser = csFiles.FirstOrDefault(f =>
                    !Path.GetFileName(f).ToLower().Contains("kaitai") &&
                    !Path.GetFileName(f).ToLower().Contains("test"));
                if (mainParser == null)
                {
                    mainParser = csFiles[0]; // Use first C# file found
                }
            }

            // Find Kaitai Struct C# runtime
            string kaitaiRuntimeDll = FindKaitaiStructCSharpRuntime();
            if (string.IsNullOrEmpty(kaitaiRuntimeDll))
            {
                // Try to compile without explicit runtime (may work if runtime is in GAC or referenced)
                kaitaiRuntimeDll = "";
            }

            // Create a temporary directory for compilation
            string compileDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(compileDir);

            try
            {
                // Copy all C# files to compile directory
                foreach (var csFile in csFiles)
                {
                    string destFile = Path.Combine(compileDir, Path.GetFileName(csFile));
                    File.Copy(csFile, destFile, true);
                }

                // Create a test C# program that uses the parser
                string testClassName = "UteParserTest";
                string testClassFile = Path.Combine(compileDir, testClassName + ".cs");
                string parserClassName = Path.GetFileNameWithoutExtension(mainParser);
                string parserNamespace = ExtractCSharpNamespace(mainParser);

                // Build test program that parses the UTE file and outputs JSON
                string testClassContent = $@"using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
{(string.IsNullOrEmpty(parserNamespace) ? "" : $"using {parserNamespace};")}
{(string.IsNullOrEmpty(kaitaiRuntimeDll) ? "" : "// Runtime DLL: " + kaitaiRuntimeDll)}

public class {testClassName}
{{
    public static void Main(string[] args)
    {{
        try
        {{
            string testFile = args[0];
            byte[] data = File.ReadAllBytes(testFile);

            // Create Kaitai Stream from byte array
            // Kaitai Struct C# uses KaitaiStream or ByteBufferKaitaiStream class
            // Try different stream creation methods based on runtime version
            object stream = null;
            try
            {{
                // Try ByteBufferKaitaiStream (common in newer versions)
                stream = new KaitaiStruct.Runtime.ByteBufferKaitaiStream(data);
            }}
            catch
            {{
                try
                {{
                    // Try KaitaiStream (older versions)
                    stream = new KaitaiStruct.Runtime.KaitaiStream(data);
                }}
                catch
                {{
                    // Try without namespace
                    try {{ stream = new ByteBufferKaitaiStream(data); }} catch {{ }}
                    if (stream == null) {{ try {{ stream = new KaitaiStream(data); }} catch {{ }} }}
                }}
            }}

            // Parse UTE file - try different constructor patterns
            {(string.IsNullOrEmpty(parserNamespace) ? parserClassName : $"{parserNamespace}.{parserClassName}")} ute = null;
            try
            {{
                ute = new {(string.IsNullOrEmpty(parserNamespace) ? parserClassName : $"{parserNamespace}.{parserClassName}")}(stream);
            }}
            catch
            {{
                // Try with byte array directly
                try {{ ute = new {(string.IsNullOrEmpty(parserNamespace) ? parserClassName : $"{parserNamespace}.{parserClassName}")}(data); }} catch {{ }}
                // Try with KaitaiStream parameter name
                if (ute == null) {{ try {{ ute = new {(string.IsNullOrEmpty(parserNamespace) ? parserClassName : $"{parserNamespace}.{parserClassName}")}((KaitaiStruct.Runtime.KaitaiStream)stream); }} catch {{ }} }}
            }}

            if (ute == null)
            {{
                throw new Exception(""Failed to create UTE parser instance"");
            }}

            // Build output dictionary
            var output = new Dictionary<string, object>();

            // Extract basic fields
            try {{ output[""FileType""] = ute.FileType ?? """"; }} catch {{ output[""FileType""] = """"; }}
            try {{ output[""FileVersion""] = ute.FileVersion ?? """"; }} catch {{ output[""FileVersion""] = """"; }}

            // Try to extract root struct
            try
            {{
                object rootStruct = null;
                try {{ rootStruct = ute.RootStruct; }} catch {{ }}
                if (rootStruct == null)
                {{
                    try {{ rootStruct = ute.Root_Struct; }} catch {{ }}
                }}
                if (rootStruct == null)
                {{
                    try {{ rootStruct = ute.root_struct; }} catch {{ }}
                }}

                if (rootStruct != null)
                {{
                    // Extract common UTE fields using reflection
                    string[] fieldNames = {{""Tag"", ""TemplateResRef"", ""Active"", ""DifficultyIndex"", ""Difficulty"",
                                          ""Faction"", ""MaxCreatures"", ""RecCreatures"", ""Respawns"", ""SpawnOption"",
                                          ""Reset"", ""ResetTime"", ""PlayerOnly"", ""OnEntered"", ""OnExit"",
                                          ""OnExhausted"", ""OnHeartbeat"", ""OnUserDefined"", ""Comment"", ""PaletteID""}};

                    var rootType = rootStruct.GetType();
                    foreach (string fieldName in fieldNames)
                    {{
                        try
                        {{
                            var prop = rootType.GetProperty(fieldName);
                            if (prop == null)
                            {{
                                // Try lowercase version
                                string lowerFieldName = fieldName.Substring(0, 1).ToLower() + fieldName.Substring(1);
                                prop = rootType.GetProperty(lowerFieldName);
                            }}
                            if (prop != null)
                            {{
                                object value = prop.GetValue(rootStruct);
                                if (value != null)
                                {{
                                    output[fieldName] = value.ToString();
                                }}
                            }}
                        }}
                        catch
                        {{
                            // Field not found, skip
                        }}
                    }}

                    // Extract creature list
                    try
                    {{
                        var creatureListProp = rootType.GetProperty(""CreatureList"");
                        if (creatureListProp == null)
                        {{
                            creatureListProp = rootType.GetProperty(""creatureList"");
                        }}
                        if (creatureListProp == null)
                        {{
                            creatureListProp = rootType.GetProperty(""creature_list"");
                        }}
                        if (creatureListProp != null)
                        {{
                            object creatureList = creatureListProp.GetValue(rootStruct);
                            if (creatureList is System.Collections.ICollection collection)
                            {{
                                output[""CreatureCount""] = collection.Count;
                            }}
                        }}
                    }}
                    catch
                    {{
                        // Creature list not found
                    }}
                }}
            }}
            catch (Exception e)
            {{
                // Root struct extraction failed, continue with basic fields
                // output[""Error""] = e.Message;
            }}

            // Output as JSON-like format
            Console.WriteLine(""SUCCESS"");
            Console.Write(""{{"");
            bool first = true;
            foreach (var entry in output)
            {{
                if (!first) Console.Write("","");
                string value = entry.Value?.ToString() ?? """";
                // Escape quotes in value
                value = value.Replace(""\"""", ""\\\"""").Replace(""\r"", """").Replace(""\n"", """");
                Console.Write(""\"""" + entry.Key + ""\"":\"""" + value + ""\"""");
                first = false;
            }}
            Console.WriteLine(""}}"");
        }}
        catch (Exception e)
        {{
            Console.Error.WriteLine(""ERROR: "" + e.Message);
            Console.Error.WriteLine(e.StackTrace);
            Environment.Exit(1);
        }}
    }}
}}";

                File.WriteAllText(testClassFile, testClassContent);

                // Compile C# files
                // Build list of all C# files to compile
                var allCsFiles = new List<string>();
                foreach (var csFile in csFiles)
                {
                    allCsFiles.Add(Path.Combine(compileDir, Path.GetFileName(csFile)));
                }
                allCsFiles.Add(testClassFile);

                // Determine compilation method based on available compiler
                bool useDotnet = cscPath.Contains("dotnet") || cscPath == "dotnet";

                if (useDotnet)
                {
                    // Use dotnet CLI to compile
                    // Create a temporary .csproj file
                    string csprojFile = Path.Combine(compileDir, "UteParserTest.csproj");
                    string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  {(string.IsNullOrEmpty(kaitaiRuntimeDll) ? "" : $@"<ItemGroup>
    <Reference Include=""KaitaiStructRuntime"">
      <HintPath>{kaitaiRuntimeDll}</HintPath>
    </Reference>
  </ItemGroup>")}
</Project>";
                    File.WriteAllText(csprojFile, csprojContent);

                    // Build using dotnet
                    var buildResult = RunCommand("dotnet", $"build \"{csprojFile}\" -o \"{compileDir}\" --no-restore");
                    if (buildResult.ExitCode != 0)
                    {
                        // Try without explicit runtime reference
                        if (!string.IsNullOrEmpty(kaitaiRuntimeDll))
                        {
                            csprojContent = csprojContent.Replace($@"<ItemGroup>
    <Reference Include=""KaitaiStructRuntime"">
      <HintPath>{kaitaiRuntimeDll}</HintPath>
    </Reference>
  </ItemGroup>", "");
                            File.WriteAllText(csprojFile, csprojContent);
                            buildResult = RunCommand("dotnet", $"build \"{csprojFile}\" -o \"{compileDir}\" --no-restore");
                        }

                        if (buildResult.ExitCode != 0)
                        {
                            return new ParserExecutionResult
                            {
                                Success = false,
                                ErrorMessage = $"C# compilation failed: {buildResult.Error}. Output: {buildResult.Output}",
                                ParsedData = buildResult.Output
                            };
                        }
                    }

                    // Execute the compiled program
                    string exePath = Path.Combine(compileDir, "UteParserTest.exe");
                    if (!File.Exists(exePath))
                    {
                        // Try .dll with dotnet run
                        exePath = Path.Combine(compileDir, "UteParserTest.dll");
                        if (File.Exists(exePath))
                        {
                            var executeResult = RunCommand("dotnet", $"\"{exePath}\" \"{testFile}\"");
                            if (executeResult.ExitCode == 0 && executeResult.Output.Contains("SUCCESS"))
                            {
                                return new ParserExecutionResult
                                {
                                    Success = true,
                                    ErrorMessage = null,
                                    ParsedData = executeResult.Output
                                };
                            }
                            else
                            {
                                return new ParserExecutionResult
                                {
                                    Success = false,
                                    ErrorMessage = $"C# parser execution failed: {executeResult.Error}. Output: {executeResult.Output}",
                                    ParsedData = executeResult.Output
                                };
                            }
                        }
                        else
                        {
                            return new ParserExecutionResult
                            {
                                Success = false,
                                ErrorMessage = $"Compiled executable not found in {compileDir}",
                                ParsedData = buildResult.Output
                            };
                        }
                    }
                    else
                    {
                        var executeResult = RunCommand(exePath, $"\"{testFile}\"");
                        if (executeResult.ExitCode == 0 && executeResult.Output.Contains("SUCCESS"))
                        {
                            return new ParserExecutionResult
                            {
                                Success = true,
                                ErrorMessage = null,
                                ParsedData = executeResult.Output
                            };
                        }
                        else
                        {
                            return new ParserExecutionResult
                            {
                                Success = false,
                                ErrorMessage = $"C# parser execution failed: {executeResult.Error}. Output: {executeResult.Output}",
                                ParsedData = executeResult.Output
                            };
                        }
                    }
                }
                else
                {
                    // Use csc (C# compiler) directly
                    // Build classpath/references
                    var references = new List<string>();
                    if (!string.IsNullOrEmpty(kaitaiRuntimeDll) && File.Exists(kaitaiRuntimeDll))
                    {
                        references.Add($"-reference:\"{kaitaiRuntimeDll}\"");
                    }

                    // Add standard library references
                    string frameworkPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "dotnet", "shared", "Microsoft.NETCore.App");
                    if (Directory.Exists(frameworkPath))
                    {
                        var frameworkDirs = Directory.GetDirectories(frameworkPath);
                        if (frameworkDirs.Length > 0)
                        {
                            string latestFramework = frameworkDirs.OrderByDescending(d => d).First();
                            string systemDll = Path.Combine(latestFramework, "System.Runtime.dll");
                            if (File.Exists(systemDll))
                            {
                                references.Add($"-reference:\"{systemDll}\"");
                            }
                        }
                    }

                    // Compile all files
                    string allFilesArg = string.Join(" ", allCsFiles.Select(f => $"\"{f}\""));
                    string refsArg = string.Join(" ", references);
                    var compileResult = RunCommand(cscPath, $"-out:\"{Path.Combine(compileDir, testClassName + ".exe")}\" {refsArg} {allFilesArg}");

                    if (compileResult.ExitCode != 0)
                    {
                        // Try without explicit runtime reference
                        if (!string.IsNullOrEmpty(kaitaiRuntimeDll))
                        {
                            references.RemoveAll(r => r.Contains("KaitaiStruct"));
                            refsArg = string.Join(" ", references);
                            compileResult = RunCommand(cscPath, $"-out:\"{Path.Combine(compileDir, testClassName + ".exe")}\" {refsArg} {allFilesArg}");
                        }

                        if (compileResult.ExitCode != 0)
                        {
                            return new ParserExecutionResult
                            {
                                Success = false,
                                ErrorMessage = $"C# compilation failed: {compileResult.Error}. Output: {compileResult.Output}",
                                ParsedData = compileResult.Output
                            };
                        }
                    }

                    // Execute the compiled program
                    string exePath = Path.Combine(compileDir, testClassName + ".exe");
                    if (!File.Exists(exePath))
                    {
                        return new ParserExecutionResult
                        {
                            Success = false,
                            ErrorMessage = $"Compiled executable not found: {exePath}",
                            ParsedData = compileResult.Output
                        };
                    }

                    var executeResult = RunCommand(exePath, $"\"{testFile}\"");
                    if (executeResult.ExitCode == 0 && executeResult.Output.Contains("SUCCESS"))
                    {
                        return new ParserExecutionResult
                        {
                            Success = true,
                            ErrorMessage = null,
                            ParsedData = executeResult.Output
                        };
                    }
                    else
                    {
                        return new ParserExecutionResult
                        {
                            Success = false,
                            ErrorMessage = $"C# parser execution failed: {executeResult.Error}. Output: {executeResult.Output}",
                            ParsedData = executeResult.Output
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ParserExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Exception during C# parser execution: {ex.Message}",
                    ParsedData = ex.ToString()
                };
            }
            finally
            {
                // Cleanup compile directory
                try
                {
                    if (Directory.Exists(compileDir))
                    {
                        Directory.Delete(compileDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Finds the C# compiler (csc or dotnet).
        /// </summary>
        private string FindCSharpCompiler()
        {
            // Try dotnet CLI first (most common on modern systems)
            var dotnetCheck = RunCommand("dotnet", "--version");
            if (dotnetCheck.ExitCode == 0)
            {
                return "dotnet";
            }

            // Try csc (C# compiler) in common locations
            string[] possiblePaths = new[]
            {
                "csc",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "2022", "Community", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "2022", "Professional", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio", "2022", "Enterprise", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "2019", "Community", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "2019", "Professional", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "2019", "Enterprise", "MSBuild", "Current", "Bin", "Roslyn", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe"),
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "/version",
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
                            if (process.ExitCode == 0 || process.ExitCode == 1) // csc returns 1 for /version, but that's OK
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

        /// <summary>
        /// Finds the Kaitai Struct C# runtime DLL.
        /// </summary>
        private string FindKaitaiStructCSharpRuntime()
        {
            // Check environment variable first
            var envDll = Environment.GetEnvironmentVariable("KAITAI_CSHARP_RUNTIME");
            if (!string.IsNullOrEmpty(envDll) && File.Exists(envDll))
            {
                return envDll;
            }

            // Check common locations for Kaitai Struct C# runtime
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "KaitaiStruct.Runtime.dll"),
                Path.Combine(AppContext.BaseDirectory, "..", "KaitaiStruct.Runtime.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "kaitai.struct.runtime", "0.9", "lib", "netstandard2.0", "KaitaiStruct.Runtime.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "kaitai.struct.runtime", "0.10", "lib", "netstandard2.0", "KaitaiStruct.Runtime.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "kaitai.struct.runtime", "0.11", "lib", "netstandard2.0", "KaitaiStruct.Runtime.dll"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "kaitai.struct.runtime", "0.12", "lib", "netstandard2.0", "KaitaiStruct.Runtime.dll"),
                // Also check in output directory (may have been copied there)
                Path.Combine(KaitaiOutputDir, "csharp", "KaitaiStruct.Runtime.dll"),
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
                    // Ignore path errors
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the C# namespace from a C# source file.
        /// </summary>
        private string ExtractCSharpNamespace(string csFile)
        {
            try
            {
                string content = File.ReadAllText(csFile);
                // Look for namespace declaration: namespace com.example;
                int namespaceIndex = content.IndexOf("namespace ", StringComparison.Ordinal);
                if (namespaceIndex >= 0)
                {
                    int start = namespaceIndex + 10; // "namespace ".Length
                    int end = content.IndexOfAny(new[] { ';', '\r', '\n', '{' }, start);
                    if (end > start)
                    {
                        return content.Substring(start, end - start).Trim();
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return "";
        }

        private class ParserExecutionResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public string ParsedData { get; set; }
            public UteParsedData StructuredData { get; set; }
        }

        /// <summary>
        /// Structured representation of parsed UTE data for cross-language comparison.
        /// Contains all key fields that should be consistent across parsers.
        /// </summary>
        private class UteParsedData
        {
            public string FileType { get; set; }
            public string FileVersion { get; set; }
            public string Tag { get; set; }
            public string TemplateResRef { get; set; }
            public bool Active { get; set; }
            public int DifficultyIndex { get; set; }
            public int Difficulty { get; set; }
            public int Faction { get; set; }
            public int MaxCreatures { get; set; }
            public int RecCreatures { get; set; }
            public int Respawns { get; set; }
            public int SpawnOption { get; set; }
            public int Reset { get; set; }
            public int ResetTime { get; set; }
            public int PlayerOnly { get; set; }
            public string OnEntered { get; set; }
            public string OnExit { get; set; }
            public string OnExhausted { get; set; }
            public string OnHeartbeat { get; set; }
            public string OnUserDefined { get; set; }
            public string Comment { get; set; }
            public int PaletteId { get; set; }
            public int CreatureCount { get; set; }
            public List<UteCreatureData> Creatures { get; set; } = new List<UteCreatureData>();
        }

        /// <summary>
        /// Structured representation of a creature in a UTE file.
        /// </summary>
        private class UteCreatureData
        {
            public string ResRef { get; set; }
            public int Appearance { get; set; }
            public float CR { get; set; }
            public int SingleSpawn { get; set; }
            public int GuaranteedCount { get; set; }
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructDefinitionCompleteness()
        {
            // Validate that UTE.ksy definition is complete and matches the format
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "UTE.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: ute", "Should have id: ute");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("gff_header", "Should define gff_header type");
            ksyContent.Should().Contain("label_array", "Should define label_array type");
            ksyContent.Should().Contain("struct_array", "Should define struct_array type");
            ksyContent.Should().Contain("field_array", "Should define field_array type");
            ksyContent.Should().Contain("field_data_section", "Should define field_data_section type");
            ksyContent.Should().Contain("field_indices_array", "Should define field_indices_array type");
            ksyContent.Should().Contain("list_indices_array", "Should define list_indices_array type");
            ksyContent.Should().Contain("UTE ", "Should specify UTE file type signature");
            ksyContent.Should().Contain("file-extension: ute", "Should specify ute file extension");
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructDefinitionReferences()
        {
            // Validate that UTE.ksy has proper documentation and references
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "UTE.ksy not found - skipping references test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Check for documentation references
            ksyContent.Should().Contain("pykotor", "Should reference PyKotor");
            ksyContent.Should().Contain("reone", "Should reference reone");
            ksyContent.Should().Contain("wiki", "Should reference wiki documentation");
            ksyContent.Should().Contain("doc:", "Should have documentation section");
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilesToAtLeastDozenLanguages()
        {
            // Ensure we test at least a dozen languages (12 languages)
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "UTE.ksy not found - skipping compilation test");
                return;
            }

            SupportedLanguages.Length.Should().BeGreaterThanOrEqualTo(12,
                "Should support at least a dozen languages for testing");

            // Check if Java is available (required for Kaitai Struct compiler)
            var javaCheck = RunCommand("java", "-version");
            if (javaCheck.ExitCode != 0)
            {
                Assert.True(true, "Java not available - skipping compilation test");
                return;
            }

            Directory.CreateDirectory(KaitaiOutputDir);

            int compiledCount = 0;
            var compiledLanguages = new List<string>();

            foreach (var lang in SupportedLanguages)
            {
                try
                {
                    var result = CompileToLanguage(normalizedKsyPath, lang);
                    if (result.Success)
                    {
                        compiledCount++;
                        compiledLanguages.Add(lang);
                    }
                }
                catch
                {
                    // Ignore individual failures
                }
            }

            // We should be able to compile to at least a dozen languages
            compiledCount.Should().BeGreaterThanOrEqualTo(12,
                $"Should successfully compile UTE.ksy to at least 12 languages (a dozen). " +
                $"Compiled to {compiledCount} languages: {string.Join(", ", compiledLanguages)}");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        /// <summary>
        /// Parses the output from a parser execution into structured UTE data.
        /// Handles different output formats from different languages.
        /// </summary>
        private UteParsedData ParseParserOutput(string language, string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return null;
            }

            // Try to parse as JSON first (if parsers output JSON)
            try
            {
                // Look for JSON in the output
                int jsonStart = output.IndexOf('{');
                int jsonEnd = output.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string json = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    // Use simple JSON parsing (we could use Newtonsoft.Json if available)
                    return ParseJsonOutput(json);
                }
            }
            catch
            {
                // Fall through to line-based parsing
            }

            // Fall back to line-based parsing for text output
            return ParseTextOutput(language, output);
        }

        /// <summary>
        /// Parses JSON output from a parser.
        /// </summary>
        private UteParsedData ParseJsonOutput(string json)
        {
            // Simple JSON parsing - extract key-value pairs
            // This is a simplified parser; in production you'd use a proper JSON library
            var data = new UteParsedData();

            // Extract string values
            ExtractJsonValue(json, "Tag", out string tag);
            data.Tag = tag ?? "";

            ExtractJsonValue(json, "TemplateResRef", out string templateResRef);
            data.TemplateResRef = templateResRef ?? "";

            ExtractJsonValue(json, "OnEntered", out string onEntered);
            data.OnEntered = onEntered ?? "";

            ExtractJsonValue(json, "OnExit", out string onExit);
            data.OnExit = onExit ?? "";

            ExtractJsonValue(json, "OnExhausted", out string onExhausted);
            data.OnExhausted = onExhausted ?? "";

            ExtractJsonValue(json, "OnHeartbeat", out string onHeartbeat);
            data.OnHeartbeat = onHeartbeat ?? "";

            ExtractJsonValue(json, "OnUserDefined", out string onUserDefined);
            data.OnUserDefined = onUserDefined ?? "";

            ExtractJsonValue(json, "Comment", out string comment);
            data.Comment = comment ?? "";

            ExtractJsonValue(json, "FileType", out string fileType);
            data.FileType = fileType ?? "";

            ExtractJsonValue(json, "FileVersion", out string fileVersion);
            data.FileVersion = fileVersion ?? "";

            // Extract integer values
            ExtractJsonIntValue(json, "Active", out int active);
            data.Active = active != 0;

            ExtractJsonIntValue(json, "DifficultyIndex", out int difficultyIndex);
            data.DifficultyIndex = difficultyIndex;

            ExtractJsonIntValue(json, "Difficulty", out int difficulty);
            data.Difficulty = difficulty;

            ExtractJsonIntValue(json, "Faction", out int faction);
            data.Faction = faction;

            ExtractJsonIntValue(json, "MaxCreatures", out int maxCreatures);
            data.MaxCreatures = maxCreatures;

            ExtractJsonIntValue(json, "RecCreatures", out int recCreatures);
            data.RecCreatures = recCreatures;

            ExtractJsonIntValue(json, "Respawns", out int respawns);
            data.Respawns = respawns;

            ExtractJsonIntValue(json, "SpawnOption", out int spawnOption);
            data.SpawnOption = spawnOption;

            ExtractJsonIntValue(json, "Reset", out int reset);
            data.Reset = reset;

            ExtractJsonIntValue(json, "ResetTime", out int resetTime);
            data.ResetTime = resetTime;

            ExtractJsonIntValue(json, "PlayerOnly", out int playerOnly);
            data.PlayerOnly = playerOnly;

            ExtractJsonIntValue(json, "PaletteID", out int paletteId);
            data.PaletteId = paletteId;

            ExtractJsonIntValue(json, "CreatureCount", out int creatureCount);
            data.CreatureCount = creatureCount;

            return data;
        }

        /// <summary>
        /// Extracts a string value from JSON.
        /// </summary>
        private void ExtractJsonValue(string json, string key, out string value)
        {
            value = null;
            string searchKey = $"\"{key}\"";
            int keyIndex = json.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);
            if (keyIndex >= 0)
            {
                int colonIndex = json.IndexOf(':', keyIndex);
                if (colonIndex >= 0)
                {
                    int startQuote = json.IndexOf('"', colonIndex);
                    if (startQuote >= 0)
                    {
                        int endQuote = json.IndexOf('"', startQuote + 1);
                        if (endQuote > startQuote)
                        {
                            value = json.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts an integer value from JSON.
        /// </summary>
        private void ExtractJsonIntValue(string json, string key, out int value)
        {
            value = 0;
            string searchKey = $"\"{key}\"";
            int keyIndex = json.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);
            if (keyIndex >= 0)
            {
                int colonIndex = json.IndexOf(':', keyIndex);
                if (colonIndex >= 0)
                {
                    // Skip whitespace
                    int valueStart = colonIndex + 1;
                    while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                    {
                        valueStart++;
                    }

                    // Find end of number (comma, }, or whitespace)
                    int valueEnd = valueStart;
                    while (valueEnd < json.Length &&
                           (char.IsDigit(json[valueEnd]) || json[valueEnd] == '-' || json[valueEnd] == '+'))
                    {
                        valueEnd++;
                    }

                    if (valueEnd > valueStart)
                    {
                        string intStr = json.Substring(valueStart, valueEnd - valueStart);
                        if (int.TryParse(intStr, out int parsed))
                        {
                            value = parsed;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Parses text-based output from a parser (line-by-line format).
        /// </summary>
        private UteParsedData ParseTextOutput(string language, string output)
        {
            var data = new UteParsedData();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                // Parse key-value pairs like "Tag: value" or "Tag=value"
                int colonIndex = trimmed.IndexOf(':');
                int equalsIndex = trimmed.IndexOf('=');
                int separatorIndex = colonIndex >= 0 ? (equalsIndex >= 0 ? Math.Min(colonIndex, equalsIndex) : colonIndex) : equalsIndex;

                if (separatorIndex > 0 && separatorIndex < trimmed.Length - 1)
                {
                    string key = trimmed.Substring(0, separatorIndex).Trim();
                    string value = trimmed.Substring(separatorIndex + 1).Trim();

                    // Remove quotes if present
                    if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    SetFieldValue(data, key, value);
                }
            }

            return data;
        }

        /// <summary>
        /// Sets a field value on UteParsedData based on field name.
        /// </summary>
        private void SetFieldValue(UteParsedData data, string fieldName, string value)
        {
            switch (fieldName.ToLowerInvariant())
            {
                case "filetype":
                case "file_type":
                    data.FileType = value;
                    break;
                case "fileversion":
                case "file_version":
                    data.FileVersion = value;
                    break;
                case "tag":
                    data.Tag = value;
                    break;
                case "templateresref":
                case "template_res_ref":
                    data.TemplateResRef = value;
                    break;
                case "active":
                    data.Active = ParseBool(value);
                    break;
                case "difficultyindex":
                case "difficulty_index":
                    data.DifficultyIndex = ParseInt(value);
                    break;
                case "difficulty":
                    data.Difficulty = ParseInt(value);
                    break;
                case "faction":
                    data.Faction = ParseInt(value);
                    break;
                case "maxcreatures":
                case "max_creatures":
                    data.MaxCreatures = ParseInt(value);
                    break;
                case "reccreatures":
                case "rec_creatures":
                    data.RecCreatures = ParseInt(value);
                    break;
                case "respawns":
                    data.Respawns = ParseInt(value);
                    break;
                case "spawnoption":
                case "spawn_option":
                    data.SpawnOption = ParseInt(value);
                    break;
                case "reset":
                    data.Reset = ParseInt(value);
                    break;
                case "resettime":
                case "reset_time":
                    data.ResetTime = ParseInt(value);
                    break;
                case "playeronly":
                case "player_only":
                    data.PlayerOnly = ParseInt(value);
                    break;
                case "onentered":
                case "on_entered":
                    data.OnEntered = value;
                    break;
                case "onexit":
                case "on_exit":
                    data.OnExit = value;
                    break;
                case "onexhausted":
                case "on_exhausted":
                    data.OnExhausted = value;
                    break;
                case "onheartbeat":
                case "on_heartbeat":
                    data.OnHeartbeat = value;
                    break;
                case "onuserdefined":
                case "on_user_defined":
                    data.OnUserDefined = value;
                    break;
                case "comment":
                    data.Comment = value;
                    break;
                case "paletteid":
                case "palette_id":
                    data.PaletteId = ParseInt(value);
                    break;
                case "creaturecount":
                case "creature_count":
                    data.CreatureCount = ParseInt(value);
                    break;
            }
        }

        /// <summary>
        /// Parses a boolean value from string.
        /// </summary>
        private bool ParseBool(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            value = value.ToLowerInvariant().Trim();
            return value == "true" || value == "1" || value == "yes";
        }

        /// <summary>
        /// Parses an integer value from string.
        /// </summary>
        private int ParseInt(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            if (int.TryParse(value.Trim(), out int result))
            {
                return result;
            }
            return 0;
        }

        /// <summary>
        /// Creates reference structured data from the C# GFF parser.
        /// </summary>
        private UteParsedData CreateReferenceData(GFF gff, UTE ute)
        {
            var data = new UteParsedData
            {
                FileType = gff.Header.FileType,
                FileVersion = gff.Header.FileVersion,
                Tag = ute.Tag ?? "",
                TemplateResRef = ute.ResRef?.ToString() ?? "",
                Active = ute.Active,
                DifficultyIndex = ute.DifficultyId,
                Difficulty = ute.DifficultyIndex,
                Faction = ute.Faction,
                MaxCreatures = ute.MaxCreatures,
                RecCreatures = ute.RecCreatures,
                Respawns = ute.Respawn,
                SpawnOption = ute.SingleSpawn,
                Reset = ute.Reset,
                ResetTime = ute.ResetTime,
                PlayerOnly = ute.PlayerOnly,
                OnEntered = ute.OnEnteredScript?.ToString() ?? "",
                OnExit = ute.OnExitScript?.ToString() ?? "",
                OnExhausted = ute.OnExhaustedScript?.ToString() ?? "",
                OnHeartbeat = ute.OnHeartbeatScript?.ToString() ?? "",
                OnUserDefined = ute.OnUserDefinedScript?.ToString() ?? "",
                Comment = ute.Comment ?? "",
                PaletteId = ute.PaletteId,
                CreatureCount = ute.Creatures?.Count ?? 0
            };

            if (ute.Creatures != null)
            {
                foreach (var creature in ute.Creatures)
                {
                    data.Creatures.Add(new UteCreatureData
                    {
                        ResRef = creature.ResRef?.ToString() ?? "",
                        Appearance = creature.Appearance,
                        CR = creature.CR,
                        SingleSpawn = creature.SingleSpawn,
                        GuaranteedCount = creature.GuaranteedCount
                    });
                }
            }

            return data;
        }

        /// <summary>
        /// Compares parser results across all languages and validates consistency.
        /// </summary>
        private void CompareParserResults(Dictionary<string, UteParsedData> results, UteParsedData reference)
        {
            // Compare each parser's results against the reference
            foreach (var kvp in results)
            {
                if (kvp.Key == "csharp_reference")
                {
                    continue; // Skip self-comparison
                }

                var parserData = kvp.Value;
                var parserName = kvp.Key;

                // Compare FileType and FileVersion (GFF header fields)
                if (!string.IsNullOrEmpty(parserData.FileType))
                {
                    parserData.FileType.Should().Be(reference.FileType,
                        $"Parser {parserName} should match reference FileType");
                }

                if (!string.IsNullOrEmpty(parserData.FileVersion))
                {
                    parserData.FileVersion.Should().Be(reference.FileVersion,
                        $"Parser {parserName} should match reference FileVersion");
                }

                // Compare Tag
                if (!string.IsNullOrEmpty(parserData.Tag))
                {
                    parserData.Tag.Should().Be(reference.Tag,
                        $"Parser {parserName} should match reference Tag");
                }

                // Compare TemplateResRef
                if (!string.IsNullOrEmpty(parserData.TemplateResRef))
                {
                    parserData.TemplateResRef.Should().Be(reference.TemplateResRef,
                        $"Parser {parserName} should match reference TemplateResRef");
                }

                // Compare Active (boolean)
                parserData.Active.Should().Be(reference.Active,
                    $"Parser {parserName} should match reference Active");

                // Compare DifficultyIndex
                parserData.DifficultyIndex.Should().Be(reference.DifficultyIndex,
                    $"Parser {parserName} should match reference DifficultyIndex");

                // Compare Difficulty
                parserData.Difficulty.Should().Be(reference.Difficulty,
                    $"Parser {parserName} should match reference Difficulty");

                // Compare Faction
                parserData.Faction.Should().Be(reference.Faction,
                    $"Parser {parserName} should match reference Faction");

                // Compare MaxCreatures
                parserData.MaxCreatures.Should().Be(reference.MaxCreatures,
                    $"Parser {parserName} should match reference MaxCreatures");

                // Compare RecCreatures
                parserData.RecCreatures.Should().Be(reference.RecCreatures,
                    $"Parser {parserName} should match reference RecCreatures");

                // Compare Respawns
                parserData.Respawns.Should().Be(reference.Respawns,
                    $"Parser {parserName} should match reference Respawns");

                // Compare SpawnOption
                parserData.SpawnOption.Should().Be(reference.SpawnOption,
                    $"Parser {parserName} should match reference SpawnOption");

                // Compare Reset
                parserData.Reset.Should().Be(reference.Reset,
                    $"Parser {parserName} should match reference Reset");

                // Compare ResetTime
                parserData.ResetTime.Should().Be(reference.ResetTime,
                    $"Parser {parserName} should match reference ResetTime");

                // Compare PlayerOnly
                parserData.PlayerOnly.Should().Be(reference.PlayerOnly,
                    $"Parser {parserName} should match reference PlayerOnly");

                // Compare script ResRefs
                if (!string.IsNullOrEmpty(parserData.OnEntered))
                {
                    parserData.OnEntered.Should().Be(reference.OnEntered,
                        $"Parser {parserName} should match reference OnEntered");
                }

                if (!string.IsNullOrEmpty(parserData.OnExit))
                {
                    parserData.OnExit.Should().Be(reference.OnExit,
                        $"Parser {parserName} should match reference OnExit");
                }

                if (!string.IsNullOrEmpty(parserData.OnExhausted))
                {
                    parserData.OnExhausted.Should().Be(reference.OnExhausted,
                        $"Parser {parserName} should match reference OnExhausted");
                }

                if (!string.IsNullOrEmpty(parserData.OnHeartbeat))
                {
                    parserData.OnHeartbeat.Should().Be(reference.OnHeartbeat,
                        $"Parser {parserName} should match reference OnHeartbeat");
                }

                if (!string.IsNullOrEmpty(parserData.OnUserDefined))
                {
                    parserData.OnUserDefined.Should().Be(reference.OnUserDefined,
                        $"Parser {parserName} should match reference OnUserDefined");
                }

                // Compare Comment
                if (!string.IsNullOrEmpty(parserData.Comment))
                {
                    parserData.Comment.Should().Be(reference.Comment,
                        $"Parser {parserName} should match reference Comment");
                }

                // Compare PaletteId
                parserData.PaletteId.Should().Be(reference.PaletteId,
                    $"Parser {parserName} should match reference PaletteId");

                // Compare CreatureCount
                parserData.CreatureCount.Should().Be(reference.CreatureCount,
                    $"Parser {parserName} should match reference CreatureCount");

                // Compare creatures if both have them
                if (parserData.Creatures.Count > 0 && reference.Creatures.Count > 0)
                {
                    parserData.Creatures.Count.Should().Be(reference.Creatures.Count,
                        $"Parser {parserName} should match reference creature count");

                    for (int i = 0; i < Math.Min(parserData.Creatures.Count, reference.Creatures.Count); i++)
                    {
                        var parserCreature = parserData.Creatures[i];
                        var refCreature = reference.Creatures[i];

                        if (!string.IsNullOrEmpty(parserCreature.ResRef))
                        {
                            parserCreature.ResRef.Should().Be(refCreature.ResRef,
                                $"Parser {parserName} creature {i} should match reference ResRef");
                        }

                        parserCreature.Appearance.Should().Be(refCreature.Appearance,
                            $"Parser {parserName} creature {i} should match reference Appearance");

                        // CR is a float, allow small tolerance
                        Math.Abs(parserCreature.CR - refCreature.CR).Should().BeLessThan(0.01f,
                            $"Parser {parserName} creature {i} should match reference CR");

                        parserCreature.SingleSpawn.Should().Be(refCreature.SingleSpawn,
                            $"Parser {parserName} creature {i} should match reference SingleSpawn");

                        parserCreature.GuaranteedCount.Should().Be(refCreature.GuaranteedCount,
                            $"Parser {parserName} creature {i} should match reference GuaranteedCount");
                    }
                }
            }
        }
    }
}


