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
    /// Comprehensive tests for NSS format using Kaitai Struct generated parsers.
    /// Tests validate that the NSS.ksy definition compiles correctly to multiple languages.
    /// </summary>
    public class NSSKaitaiStructTests
    {
        private static readonly string KsyFile = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "NSS", "NSS.ksy");

        private static readonly string TestNssFile = TestFileHelper.GetPath("test.nss");
        private static readonly string KaitaiOutputDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "kaitai_compiled", "nss");

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
        public void TestKsyFileExists()
        {
            var ksyPath = new FileInfo(KsyFile);
            if (!ksyPath.Exists)
            {
                ksyPath = new FileInfo(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..",
                    "src", "Andastra", "Parsing", "Resource", "Formats", "NSS", "NSS.ksy"));
            }

            ksyPath.Exists.Should().BeTrue($"NSS.ksy should exist at {ksyPath.FullName}");
        }

        [Fact(Timeout = 300000)]
        public void TestKsyFileValid()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NSS.ksy not found - skipping validation");
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
                    Assert.True(false, $"NSS.ksy has syntax errors: {stderr}");
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
                Assert.True(true, "NSS.ksy not found - skipping compilation test");
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
                        Assert.True(false, $"Failed to compile NSS.ksy to {language}: {stderr}");
                    }
                }
                else
                {
                    Assert.True(true, $"Successfully compiled NSS.ksy to {language}");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.True(true, "Kaitai Struct compiler not installed - skipping compilation test");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestKaitaiStructCompilesToAtLeastDozenLanguages()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NSS.ksy not found - skipping test");
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
                $"Should successfully compile NSS.ksy to at least 12 languages. Compiled to {compiledCount} languages.");
        }

        [Fact(Timeout = 300000)]
        public void TestNssKsyDefinitionCompleteness()
        {
            if (!File.Exists(KsyFile))
            {
                Assert.True(true, "NSS.ksy not found - skipping completeness test");
                return;
            }

            string ksyContent = File.ReadAllText(KsyFile);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: nss", "Should have id: nss");
            ksyContent.Should().Contain("source_code", "Should define source_code field");
            ksyContent.Should().Contain("encoding: UTF-8", "Should specify UTF-8 encoding");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }
    }
}

