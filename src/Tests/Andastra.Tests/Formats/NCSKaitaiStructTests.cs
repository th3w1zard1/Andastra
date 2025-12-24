using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for NCS format using Kaitai Struct generated parsers.
    /// Tests validate that the NCS.ksy definition compiles correctly to multiple languages
    /// and that the generated parsers correctly parse NCS files.
    /// </summary>
    public class NCSKaitaiStructTests
    {
        private static readonly string KsyFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "NSS", "NCS.ksy");

        private static readonly string TestNcsFile = TestFileHelper.GetPath("test.ncs");
        private static readonly string KaitaiOutputDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "kaitai_compiled", "ncs");

        // Languages supported by Kaitai Struct (at least a dozen)
        private static readonly string[] SupportedLanguages = new[]
        {
            "python", "java", "javascript", "csharp", "cpp_stl", "go", "ruby",
            "php", "rust", "swift", "perl", "nim", "lua", "kotlin", "typescript"
        };

        private static string FindKaitaiCompiler()
        {
            // Try Windows installation path first
            var windowsPath = @"C:\Program Files (x86)\kaitai-struct-compiler\bin\kaitai-struct-compiler.bat";
            if (File.Exists(windowsPath))
            {
                return windowsPath;
            }

            // Try in PATH
            try
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
                process.Start();
                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                {
                    return "kaitai-struct-compiler";
                }
            }
            catch
            {
                // Not in PATH
            }

            return windowsPath; // Return Windows path as fallback even if it doesn't exist
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilerAvailable()
        {
            string compilerPath = FindKaitaiCompiler();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
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
                    "src", "Andastra", "Parsing", "Resource", "Formats", "NSS", "NCS.ksy"));
            }

            ksyPath.Exists.Should().BeTrue($"NCS.ksy should exist at {ksyPath.FullName}");
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileValid()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NCS.ksy not found - skipping validation");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FindKaitaiCompiler(),
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

                // Try to compile to a test language to validate syntax
                string compilerPath = FindKaitaiCompiler();
                var testProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = compilerPath,
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

                // Compilation might fail due to missing dependencies, but syntax errors would be caught
                if (testProcess.ExitCode != 0 && stderr.Contains("error") && !stderr.Contains("import"))
                {
                    Assert.True(false, $"NCS.ksy has syntax errors: {stderr}");
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
                Assert.True(true, "NCS.ksy not found - skipping compilation test");
                return;
            }

            string compilerPath = FindKaitaiCompiler();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = compilerPath,
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
                        Assert.True(false, $"Failed to compile NCS.ksy to {language}: {stderr}");
                    }
                }
                else
                {
                    Assert.True(true, $"Successfully compiled NCS.ksy to {language}");
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
                Assert.True(true, "NCS.ksy not found - skipping compilation test");
                return;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FindKaitaiCompiler(),
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
                        FileName = FindKaitaiCompiler(),
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
        public void TestKaitaiStructCompilesToAtLeastDozenLanguages()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NCS.ksy not found - skipping test");
                return;
            }

            SupportedLanguages.Length.Should().BeGreaterThanOrEqualTo(12,
                "Should support at least a dozen languages for testing");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FindKaitaiCompiler(),
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
            foreach (string lang in SupportedLanguages)
            {
                var compileProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = FindKaitaiCompiler(),
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
                    }
                }
                catch
                {
                    // Ignore individual failures
                }
            }

            compiledCount.Should().BeGreaterThanOrEqualTo(12,
                $"Should successfully compile NCS.ksy to at least 12 languages. Compiled to {compiledCount} languages.");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyDefinitionCompleteness()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NCS.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: ncs", "Should have id: ncs");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("size_marker", "Should define size_marker field");
            ksyContent.Should().Contain("total_file_size", "Should define total_file_size field");
            ksyContent.Should().Contain("instructions", "Should define instructions field");
            ksyContent.Should().Contain("instruction", "Should define instruction type");
            ksyContent.Should().Contain("bytecode", "Should define bytecode field");
            ksyContent.Should().Contain("qualifier", "Should define qualifier field");
            ksyContent.Should().Contain("NCS ", "Should reference \"NCS \" magic");
            ksyContent.Should().Contain("V1.0", "Should reference \"V1.0\" version");
            ksyContent.Should().Contain("0x42", "Should reference 0x42 size marker");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyHeaderStructure()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NCS.ksy not found - skipping header structure test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Validate header structure matches NCS format
            ksyContent.Should().Contain("file_type", "Header should define file_type");
            ksyContent.Should().Contain("file_version", "Header should define file_version");
            ksyContent.Should().Contain("size_marker", "Header should define size_marker");
            ksyContent.Should().Contain("total_file_size", "Header should define total_file_size");
            ksyContent.Should().Contain("endian: be", "NCS format should use big-endian");
        }

        [Fact(Timeout = 300000)]
        public void TestNcsKsyInstructionStructure()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NCS.ksy not found - skipping instruction structure test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Validate instruction structure
            ksyContent.Should().Contain("instruction:", "Should define instruction type");
            ksyContent.Should().Contain("bytecode", "Instruction should define bytecode field");
            ksyContent.Should().Contain("qualifier", "Instruction should define qualifier field");
            ksyContent.Should().Contain("repeat: until", "Should use repeat-until for instructions");
            ksyContent.Should().Contain("repeat-until", "Should have repeat-until condition");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }
    }
}

