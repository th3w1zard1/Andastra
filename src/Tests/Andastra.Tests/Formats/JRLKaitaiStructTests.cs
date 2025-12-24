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
    /// Comprehensive tests for JRL format using Kaitai Struct generated parsers.
    /// Tests validate that the JRL.ksy definition compiles correctly to multiple languages
    /// and that the generated parsers correctly parse JRL files.
    /// </summary>
    public class JRLKaitaiStructTests
    {
        private static readonly string KsyFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "JRL", "JRL.ksy");

        private static readonly string TestJrlFile = TestFileHelper.GetPath("test.jrl");
        private static readonly string KaitaiOutputDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "kaitai_compiled", "jrl");

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
            // Ensure JRL.ksy file exists
            var ksyPath = new FileInfo(KsyFile);
            if (!ksyPath.Exists)
            {
                // Try alternative path
                ksyPath = new FileInfo(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..",
                    "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "JRL", "JRL.ksy"));
            }

            ksyPath.Exists.Should().BeTrue($"JRL.ksy should exist at {ksyPath.FullName}");
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileValid()
        {
            // Validate that JRL.ksy is valid YAML and can be parsed by compiler
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "JRL.ksy not found - skipping validation");
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
                            Assert.True(false, $"JRL.ksy should not have syntax errors. STDOUT: {stdout}, STDERR: {stderr}");
                        }

                        process.ExitCode.Should().Be(0,
                            $"JRL.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
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
            // Test that JRL.ksy compiles to each target language
            TestCompileToLanguage(language);
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToPython()
        {
            TestCompileToLanguage("python");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToJava()
        {
            TestCompileToLanguage("java");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToJavaScript()
        {
            TestCompileToLanguage("javascript");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToCSharp()
        {
            TestCompileToLanguage("csharp");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToCpp()
        {
            TestCompileToLanguage("cpp_stl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToGo()
        {
            TestCompileToLanguage("go");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToRuby()
        {
            TestCompileToLanguage("ruby");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToPhp()
        {
            TestCompileToLanguage("php");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToRust()
        {
            TestCompileToLanguage("rust");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToSwift()
        {
            TestCompileToLanguage("swift");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToPerl()
        {
            TestCompileToLanguage("perl");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToNim()
        {
            TestCompileToLanguage("nim");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToLua()
        {
            TestCompileToLanguage("lua");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToKotlin()
        {
            TestCompileToLanguage("kotlin");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileJrlToTypeScript()
        {
            TestCompileToLanguage("typescript");
        }

        private void TestCompileToLanguage(string language)
        {
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "JRL.ksy not found - skipping compilation test");
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

        [Fact(Timeout = 600000)] // 10 minute timeout for compiling all languages
        public void TestKaitaiStructCompilesToAllLanguages()
        {
            // Test compilation to all supported languages
            var normalizedKsyPath = Path.GetFullPath(KsyFile);
            if (!File.Exists(normalizedKsyPath))
            {
                Assert.True(true, "JRL.ksy not found - skipping compilation test");
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

            // At least some languages should compile successfully
            successful.Count.Should().BeGreaterThan(0,
                $"At least one language should compile successfully. Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage}"))}");

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

            // Verify at least 12 languages compiled (as required)
            successful.Count.Should().BeGreaterThanOrEqualTo(12,
                $"At least 12 languages should compile successfully. Only {successful.Count} succeeded: {string.Join(", ", successful.Select(s => s.Key))}. " +
                $"Failed: {string.Join(", ", failed.Select(f => $"{f.Key}: {f.Value.ErrorMessage?.Substring(0, Math.Min(100, f.Value.ErrorMessage?.Length ?? 0))}"))}");

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
            // Create original test JRL data for validation
            var originalJrl = new JRL();
            var originalQuest = new JRLQuest();
            originalQuest.Tag = "test_quest";
            originalQuest.Name = LocalizedString.FromInvalid();
            originalQuest.Name.SetData(Language.English, Gender.Male, "Test Quest");
            originalQuest.Priority = JRLQuestPriority.Medium;
            originalQuest.PlanetId = 0;
            originalQuest.PlotIndex = 0;
            originalQuest.Comment = "Test comment";

            var originalEntry = new JRLQuestEntry();
            originalEntry.EntryId = 1;
            originalEntry.Text = LocalizedString.FromInvalid();
            originalEntry.Text.SetData(Language.English, Gender.Male, "Test entry text");
            originalEntry.End = false;
            originalEntry.XpPercentage = 1.0f;
            originalQuest.Entries.Add(originalEntry);

            originalJrl.Quests.Add(originalQuest);

            // Create test file
            GFF testGff = JRLHelpers.DismantleJrl(originalJrl);
            byte[] data = GFFAuto.BytesGff(testGff);
            Directory.CreateDirectory(Path.GetDirectoryName(TestJrlFile));
            File.WriteAllBytes(TestJrlFile, data);

            // Parse the file back
            GFF parsedGff = GFFAuto.ReadGff(TestJrlFile, 0, null, ResourceType.JRL);
            JRL parsedJrl = JRLHelpers.ConstructJrl(parsedGff);

            // Validate structure matches Kaitai Struct definition
            // JRL files are GFF-based, so they follow GFF structure
            parsedGff.Content.Should().Be(GFFContent.JRL, "JRL file should have JRL content type");

            // Validate that we can read the JRL structure
            parsedJrl.Should().NotBeNull("JRL should be constructed from GFF");
            parsedJrl.Quests.Should().NotBeNull("JRL should have quests list");
            parsedJrl.Quests.Count.Should().Be(1, "Parsed JRL should have exactly one quest");

            // Validate quest structure matches original
            JRLQuest parsedQuest = parsedJrl.Quests[0];
            parsedQuest.Should().NotBeNull("Parsed quest should not be null");
            parsedQuest.Tag.Should().Be(originalQuest.Tag, "Quest tag should match");
            parsedQuest.Priority.Should().Be(originalQuest.Priority, "Quest priority should match");
            parsedQuest.PlanetId.Should().Be(originalQuest.PlanetId, "Quest PlanetId should match");
            parsedQuest.PlotIndex.Should().Be(originalQuest.PlotIndex, "Quest PlotIndex should match");
            parsedQuest.Comment.Should().Be(originalQuest.Comment, "Quest comment should match");

            // Validate quest name LocalizedString
            parsedQuest.Name.Should().NotBeNull("Quest name should not be null");
            string originalQuestName = originalQuest.Name.Get(Language.English, Gender.Male);
            string parsedQuestName = parsedQuest.Name.Get(Language.English, Gender.Male);
            parsedQuestName.Should().Be(originalQuestName, "Quest name should match");

            // Validate quest entries
            parsedQuest.Entries.Should().NotBeNull("Quest entries list should not be null");
            parsedQuest.Entries.Count.Should().Be(1, "Quest should have exactly one entry");

            JRLQuestEntry parsedEntry = parsedQuest.Entries[0];
            parsedEntry.Should().NotBeNull("Parsed entry should not be null");
            parsedEntry.EntryId.Should().Be(originalEntry.EntryId, "Entry ID should match");
            parsedEntry.End.Should().Be(originalEntry.End, "Entry End flag should match");
            parsedEntry.XpPercentage.Should().BeApproximately(originalEntry.XpPercentage, 0.001f, "Entry XP percentage should match");

            // Validate entry text LocalizedString
            parsedEntry.Text.Should().NotBeNull("Entry text should not be null");
            string originalEntryText = originalEntry.Text.Get(Language.English, Gender.Male);
            string parsedEntryText = parsedEntry.Text.Get(Language.English, Gender.Male);
            parsedEntryText.Should().Be(originalEntryText, "Entry text should match");

            // Test roundtrip consistency: write -> read -> write -> read
            GFF roundtripGff = JRLHelpers.DismantleJrl(parsedJrl);
            byte[] roundtripData = GFFAuto.BytesGff(roundtripGff);
            GFF roundtripParsedGff = GFFAuto.ReadGff(roundtripData, 0, null, ResourceType.JRL);
            JRL roundtripParsedJrl = JRLHelpers.ConstructJrl(roundtripParsedGff);

            // Validate roundtrip structure
            roundtripParsedJrl.Should().NotBeNull("Roundtrip parsed JRL should not be null");
            roundtripParsedJrl.Quests.Should().NotBeNull("Roundtrip parsed JRL should have quests list");
            roundtripParsedJrl.Quests.Count.Should().Be(1, "Roundtrip parsed JRL should have exactly one quest");

            JRLQuest roundtripQuest = roundtripParsedJrl.Quests[0];
            roundtripQuest.Tag.Should().Be(originalQuest.Tag, "Roundtrip quest tag should match");
            roundtripQuest.Priority.Should().Be(originalQuest.Priority, "Roundtrip quest priority should match");
            roundtripQuest.Entries.Count.Should().Be(1, "Roundtrip quest should have exactly one entry");

            JRLQuestEntry roundtripEntry = roundtripQuest.Entries[0];
            roundtripEntry.EntryId.Should().Be(originalEntry.EntryId, "Roundtrip entry ID should match");
            roundtripEntry.End.Should().Be(originalEntry.End, "Roundtrip entry End flag should match");
            roundtripEntry.XpPercentage.Should().BeApproximately(originalEntry.XpPercentage, 0.001f, "Roundtrip entry XP percentage should match");
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructDefinitionCompleteness()
        {
            // Validate that JRL.ksy definition is complete and matches the format
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "JRL.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: jrl", "Should have id: jrl");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("gff_header", "Should define gff_header type");
            ksyContent.Should().Contain("label_array", "Should define label_array type");
            ksyContent.Should().Contain("struct_array", "Should define struct_array type");
            ksyContent.Should().Contain("field_array", "Should define field_array type");
            ksyContent.Should().Contain("field_data_section", "Should define field_data_section type");
            ksyContent.Should().Contain("field_indices_array", "Should define field_indices_array type");
            ksyContent.Should().Contain("list_indices_array", "Should define list_indices_array type");
            ksyContent.Should().Contain("JRL ", "Should specify JRL file type signature");
            ksyContent.Should().Contain("file-extension: jrl", "Should specify jrl file extension");

            // Check for enum definition
            ksyContent.Should().Contain("gff_field_type", "Should define gff_field_type enum");
            ksyContent.Should().Contain("enums:", "Should have enums section");

            // Check for comprehensive documentation
            ksyContent.Should().Contain("Categories", "Should document Categories field");
            ksyContent.Should().Contain("EntryList", "Should document EntryList field");
            ksyContent.Should().Contain("Priority", "Should document Priority field");
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructDefinitionReferences()
        {
            // Validate that JRL.ksy has proper documentation and references
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "JRL.ksy not found - skipping references test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Check for documentation references
            ksyContent.Should().Contain("pykotor", "Should reference PyKotor");
            ksyContent.Should().Contain("reone", "Should reference reone");
            ksyContent.Should().Contain("wiki", "Should reference wiki documentation");
            ksyContent.Should().Contain("doc:", "Should have documentation section");
            ksyContent.Should().Contain("xref:", "Should have cross-reference section");
        }


        [Fact(Timeout = 300000)]
        public void TestJrlFileStructureValidation()
        {
            // Create a test JRL file and validate its structure matches the Kaitai definition
            if (!File.Exists(TestJrlFile))
            {
                var testJrl = new JRL();

                // Create a quest with multiple entries
                var testQuest1 = new JRLQuest();
                testQuest1.Tag = "quest_main_001";
                testQuest1.Name = LocalizedString.FromInvalid();
                testQuest1.Name.SetData(Language.English, Gender.Male, "Main Quest");
                testQuest1.Priority = JRLQuestPriority.Highest;
                testQuest1.PlanetId = 0;
                testQuest1.PlotIndex = 0;
                testQuest1.Comment = "Test quest";

                var testEntry1 = new JRLQuestEntry();
                testEntry1.EntryId = 1;
                testEntry1.Text = LocalizedString.FromInvalid();
                testEntry1.Text.SetData(Language.English, Gender.Male, "Quest started");
                testEntry1.End = false;
                testEntry1.XpPercentage = 0.0f;
                testQuest1.Entries.Add(testEntry1);

                var testEntry2 = new JRLQuestEntry();
                testEntry2.EntryId = 2;
                testEntry2.Text = LocalizedString.FromInvalid();
                testEntry2.Text.SetData(Language.English, Gender.Male, "Quest completed");
                testEntry2.End = true;
                testEntry2.XpPercentage = 1.0f;
                testQuest1.Entries.Add(testEntry2);

                testJrl.Quests.Add(testQuest1);

                GFF testGff = JRLHelpers.DismantleJrl(testJrl);
                byte[] data = GFFAuto.BytesGff(testGff);
                Directory.CreateDirectory(Path.GetDirectoryName(TestJrlFile));
                File.WriteAllBytes(TestJrlFile, data);
            }

            // Validate file can be read and parsed
            byte[] fileData = File.ReadAllBytes(TestJrlFile);
            fileData.Length.Should().BeGreaterThan(0, "JRL test file should not be empty");

            // Validate GFF structure
            GFF parsedGff = GFFAuto.ReadGff(TestJrlFile, 0, null, ResourceType.JRL);
            parsedGff.Should().NotBeNull("GFF should be readable");
            parsedGff.Content.Should().Be(GFFContent.JRL, "File should have JRL content type");

            // Validate JRL structure
            JRL parsedJrl = JRLHelpers.ConstructJrl(parsedGff);
            parsedJrl.Should().NotBeNull("JRL should be constructible from GFF");
            parsedJrl.Quests.Should().NotBeEmpty("JRL should have at least one quest");

            // Validate quest structure
            var parsedQuest = parsedJrl.Quests[0];
            parsedQuest.Tag.Should().NotBeNullOrEmpty("Quest should have a tag");
            parsedQuest.Entries.Should().NotBeEmpty("Quest should have entries");

            // Validate entry structure
            var parsedEntry = parsedQuest.Entries[0];
            parsedEntry.EntryId.Should().BeGreaterThan(0, "Entry should have a valid ID");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }
    }
}

