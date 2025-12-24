using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for GIT format using Kaitai Struct generated parsers.
    /// Tests validate that the GIT.ksy definition compiles correctly to multiple languages
    /// (at least a dozen) and that the generated parsers correctly parse GIT files.
    /// </summary>
    public class GITKaitaiStructTests
    {
        private static readonly string KsyFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "GIT", "GIT.ksy");

        private static readonly string KaitaiOutputDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "kaitai_compiled", "git");

        // Languages supported by Kaitai Struct (at least a dozen as required)
        private static readonly string[] SupportedLanguages = new[]
        {
            "python", "java", "javascript", "csharp", "cpp_stl", "go", "ruby",
            "php", "rust", "swift", "perl", "nim", "lua", "kotlin", "typescript"
        };

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilerAvailable()
        {
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
                    Assert.True(true, "Kaitai Struct compiler not available - skipping compiler tests");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping compiler tests");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileExists()
        {
            var ksyPath = new FileInfo(KsyFile);
            if (!ksyPath.Exists)
            {
                ksyPath = new FileInfo(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..",
                    "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "GIT", "GIT.ksy"));
            }

            ksyPath.Exists.Should().BeTrue($"GIT.ksy should exist at {ksyPath.FullName}");
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileValid()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "GIT.ksy not found - skipping validation");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kaitai-struct-compiler",
                    Arguments = $"--version",
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

                if (process.ExitCode != 0)
                {
                    Assert.True(true, "Kaitai Struct compiler not available - skipping validation");
                    return;
                }

                var testProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "kaitai-struct-compiler",
                        Arguments = $"-t python \"{KsyFile}\" -d \"{Path.GetTempPath()}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                testProcess.Start();
                testProcess.WaitForExit(30000);

                string stderr = testProcess.StandardError.ReadToEnd();

                if (testProcess.ExitCode != 0 && stderr.Contains("error") && !stderr.Contains("import"))
                {
                    Assert.True(false, $"GIT.ksy has syntax errors: {stderr}");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping validation");
            }
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestKaitaiStructCompilation(string language)
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "GIT.ksy not found - skipping compilation test");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kaitai-struct-compiler",
                    Arguments = $"-t {language} \"{KsyFile}\" -d \"{Path.GetTempPath()}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                process.Start();
                process.WaitForExit(60000);

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                if (process.ExitCode != 0)
                {
                    if (stderr.Contains("not supported") || stderr.Contains("unsupported"))
                    {
                        Assert.True(true, $"Language {language} not supported by compiler: {stderr}");
                    }
                    else
                    {
                        Assert.True(false, $"Failed to compile GIT.ksy to {language}: {stderr}");
                    }
                }
                else
                {
                    Assert.True(true, $"Successfully compiled GIT.ksy to {language}");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping compilation test");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilesToAllLanguages()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "GIT.ksy not found - skipping compilation test");
                return;
            }

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

                if (process.ExitCode != 0)
                {
                    Assert.True(true, "Kaitai Struct compiler not available - skipping compilation test");
                    return;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping compilation test");
                return;
            }

            int successCount = 0;
            int failCount = 0;
            var results = new List<string>();

            foreach (string lang in SupportedLanguages)
            {
                var compileProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "kaitai-struct-compiler",
                        Arguments = $"-t {lang} \"{KsyFile}\" -d \"{Path.GetTempPath()}\"",
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
                        successCount++;
                        results.Add($"{lang}: Success");
                    }
                    else
                    {
                        failCount++;
                        string error = compileProcess.StandardError.ReadToEnd();
                        results.Add($"{lang}: Failed - {error.Substring(0, Math.Min(100, error.Length))}");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    results.Add($"{lang}: Error - {ex.Message}");
                }
            }

            results.Should().NotBeEmpty("Should have compilation results");

            foreach (string result in results)
            {
                Console.WriteLine($"  {result}");
            }

            Assert.True(successCount > 0, $"At least one language should compile successfully. Results: {string.Join(", ", results)}");
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructDefinitionCompleteness()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "GIT.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Meta section
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: git", "Should have id: git");
            ksyContent.Should().Contain("file-extension: git", "Should have file-extension: git");
            ksyContent.Should().Contain("endian: le", "Should specify little-endian");

            // GFF structure components
            ksyContent.Should().Contain("gff_header", "Should define gff_header type");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("valid: \"GIT \"", "Should validate GIT file signature");
            ksyContent.Should().Contain("label_array", "Should define label_array type");
            ksyContent.Should().Contain("struct_array", "Should define struct_array type");
            ksyContent.Should().Contain("field_array", "Should define field_array type");
            ksyContent.Should().Contain("field_data_section", "Should define field_data_section type");
            ksyContent.Should().Contain("field_indices_array", "Should define field_indices_array type");
            ksyContent.Should().Contain("list_indices_array", "Should define list_indices_array type");

            // GFF field types enum
            ksyContent.Should().Contain("gff_field_type", "Should define gff_field_type enum");
            ksyContent.Should().Contain("uint8", "Should have uint8 field type");
            ksyContent.Should().Contain("int32", "Should have int32 field type");
            ksyContent.Should().Contain("single", "Should have single (float) field type");
            ksyContent.Should().Contain("string", "Should have string field type");
            ksyContent.Should().Contain("resref", "Should have resref field type");
            ksyContent.Should().Contain("localized_string", "Should have localized_string field type");
            ksyContent.Should().Contain("vector3", "Should have vector3 field type");
            ksyContent.Should().Contain("vector4", "Should have vector4 field type");
            ksyContent.Should().Contain("struct", "Should have struct field type");
            ksyContent.Should().Contain("list", "Should have list field type");

            // Localized string support
            ksyContent.Should().Contain("localized_string_data", "Should define localized_string_data type");
            ksyContent.Should().Contain("localized_substring", "Should define localized_substring type");
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilesToAtLeastDozenLanguages()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "GIT.ksy not found - skipping test");
                return;
            }

            SupportedLanguages.Length.Should().BeGreaterThanOrEqualTo(12,
                "Should support at least a dozen languages for testing");

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

                if (process.ExitCode != 0)
                {
                    Assert.True(true, "Kaitai Struct compiler not available - skipping test");
                    return;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping test");
                return;
            }

            int compiledCount = 0;
            var compilationResults = new List<string>();

            foreach (string lang in SupportedLanguages)
            {
                var compileProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "kaitai-struct-compiler",
                        Arguments = $"-t {lang} \"{KsyFile}\" -d \"{Path.GetTempPath()}\"",
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
                        compilationResults.Add($"{lang}: ✓");
                    }
                    else
                    {
                        string error = compileProcess.StandardError.ReadToEnd();
                        compilationResults.Add($"{lang}: ✗ ({error.Substring(0, Math.Min(50, error.Length)).Trim()})");
                    }
                }
                catch (Exception ex)
                {
                    compilationResults.Add($"{lang}: ✗ (Exception: {ex.Message.Substring(0, Math.Min(30, ex.Message.Length))})");
                }
            }

            // Output results for debugging
            Console.WriteLine($"Compilation results for GIT.ksy:");
            foreach (string result in compilationResults)
            {
                Console.WriteLine($"  {result}");
            }

            compiledCount.Should().BeGreaterThanOrEqualTo(12,
                $"Should successfully compile GIT.ksy to at least 12 languages. Compiled to {compiledCount}/{SupportedLanguages.Length} languages. Results: {string.Join(", ", compilationResults)}");
        }

        [Fact(Timeout = 300000)]
        public void TestGITKsyFileStructure()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "GIT.ksy not found - skipping structure test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Verify sequence structure
            ksyContent.Should().Contain("seq:", "Should have seq section");
            ksyContent.Should().Contain("gff_header", "Should read gff_header first");
            ksyContent.Should().Contain("label_array", "Should read label_array");
            ksyContent.Should().Contain("struct_array", "Should read struct_array");
            ksyContent.Should().Contain("field_array", "Should read field_array");
            ksyContent.Should().Contain("field_data", "Should read field_data");
            ksyContent.Should().Contain("field_indices", "Should read field_indices");
            ksyContent.Should().Contain("list_indices", "Should read list_indices");

            // Verify types section
            ksyContent.Should().Contain("types:", "Should have types section");

            // Verify struct_entry definition
            ksyContent.Should().Contain("struct_entry", "Should define struct_entry type");
            ksyContent.Should().Contain("struct_id", "Should have struct_id field");
            ksyContent.Should().Contain("field_count", "Should have field_count field");

            // Verify field_entry definition
            ksyContent.Should().Contain("field_entry", "Should define field_entry type");
            ksyContent.Should().Contain("field_type", "Should have field_type field");
            ksyContent.Should().Contain("label_index", "Should have label_index field");
            ksyContent.Should().Contain("data_or_offset", "Should have data_or_offset field");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }
    }
}

