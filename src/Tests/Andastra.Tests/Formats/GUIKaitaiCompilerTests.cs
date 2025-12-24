using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for GUI.ksy Kaitai Struct compiler functionality.
    /// Tests validate the GUI format structure as defined in GUI.ksy Kaitai Struct definition.
    ///
    /// Supported languages tested:
    /// - Python, Java, JavaScript, C#, C++, Ruby, PHP, Go, Rust, Perl, Lua, Nim, VisualBasic
    ///
    /// GUI format is a GFF-based format with file type signature "GUI ".
    /// </summary>
    public class GUIKaitaiCompilerTests
    {
        private static readonly string GuiKsyPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "..", "..", "..", "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "Generics", "GUI", "GUI.ksy");

        private static readonly string TestGuiFile = TestFileHelper.GetPath("test.gui");
        private static readonly string CompilerOutputDir = Path.Combine(Path.GetTempPath(), "kaitai_gui_tests");

        // Supported Kaitai Struct target languages
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
            "visualbasic"
        };

        static GUIKaitaiCompilerTests()
        {
            // Normalize GUI.ksy path
            GuiKsyPath = Path.GetFullPath(GuiKsyPath);

            // Create output directory
            if (!Directory.Exists(CompilerOutputDir))
            {
                Directory.CreateDirectory(CompilerOutputDir);
            }
        }

        [Fact(Timeout = 300000)] // 5 minutes timeout for compilation
        public void TestKaitaiStructCompilerAvailable()
        {
            // Test that kaitai-struct-compiler is available
            string compilerPath = FindKaitaiCompiler();
            compilerPath.Should().NotBeNullOrEmpty("kaitai-struct-compiler should be available in PATH or common locations");

            // Handle node-based compiler path and .bat files
            string actualCompilerPath = compilerPath;
            string versionArgs = "--version";
            if (compilerPath.StartsWith("node:\""))
            {
                actualCompilerPath = "node";
                string jsPath = compilerPath.Substring(6).TrimEnd('"');
                versionArgs = $"\"{jsPath}\" --version";
            }
            else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                // Use cmd.exe to run .bat file on Windows
                actualCompilerPath = "cmd.exe";
                versionArgs = $"/c \"{compilerPath}\" --version";
            }

            // Test compiler version
            var processInfo = new ProcessStartInfo
            {
                FileName = actualCompilerPath,
                Arguments = versionArgs,
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
        public void TestGuiKsyFileExists()
        {
            File.Exists(GuiKsyPath).Should().BeTrue($"GUI.ksy should exist at {GuiKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(GuiKsyPath);
            content.Should().Contain("meta:", "GUI.ksy should contain meta section");
            content.Should().Contain("id: gui", "GUI.ksy should have id: gui");
            content.Should().Contain("file-extension: gui", "GUI.ksy should specify gui file extension");
            content.Should().Contain("GUI ", "GUI.ksy should include GUI file type signature");
        }

        [Theory(Timeout = 300000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestCompileGuiKsyToLanguage(string language)
        {
            // Skip if compiler not available
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip test if compiler not available
            }

            // Create output directory for this language
            string langOutputDir = Path.Combine(CompilerOutputDir, language);
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Handle node-based compiler path and .bat files
            string actualCompilerPath = compilerPath;
            string arguments = $"-t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            if (compilerPath.StartsWith("node:\""))
            {
                actualCompilerPath = "node";
                string jsPath = compilerPath.Substring(6).TrimEnd('"');
                arguments = $"\"{jsPath}\" -t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            }
            else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                // Use cmd.exe to run .bat file on Windows
                actualCompilerPath = "cmd.exe";
                arguments = $"/c \"{compilerPath}\" -t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            }

            // Compile GUI.ksy to target language
            var processInfo = new ProcessStartInfo
            {
                FileName = actualCompilerPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(GuiKsyPath)
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

            // Compilation should succeed
            exitCode.Should().Be(0,
                $"GUI.ksy should compile successfully to {language}. STDOUT: {stdout}, STDERR: {stderr}");
        }

        [Fact(Timeout = 300000)]
        public void TestCompileGuiKsyToAllLanguages()
        {
            // Test compilation to all supported languages
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            Dictionary<string, bool> results = new Dictionary<string, bool>();
            Dictionary<string, string> errors = new Dictionary<string, string>();

            foreach (string language in SupportedLanguages)
            {
                try
                {
                    string langOutputDir = Path.Combine(CompilerOutputDir, $"all_{language}");
                    if (Directory.Exists(langOutputDir))
                    {
                        Directory.Delete(langOutputDir, true);
                    }
                    Directory.CreateDirectory(langOutputDir);

                    // Handle node-based compiler path and .bat files
                    string actualCompilerPath = compilerPath;
                    string arguments = $"-t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
                    if (compilerPath.StartsWith("node:\""))
                    {
                        actualCompilerPath = "node";
                        string jsPath = compilerPath.Substring(6).TrimEnd('"');
                        arguments = $"\"{jsPath}\" -t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
                    }
                    else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                    {
                        // Use cmd.exe to run .bat file on Windows
                        actualCompilerPath = "cmd.exe";
                        arguments = $"/c \"{compilerPath}\" -t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
                    }

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = actualCompilerPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(GuiKsyPath)
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
        public void TestCompiledParserValidatesGuiFile()
        {
            // Create test GUI file if it doesn't exist
            if (!File.Exists(TestGuiFile))
            {
                CreateTestGuiFile(TestGuiFile);
            }

            // Test Python parser (most commonly available)
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "python");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Handle node-based compiler path and .bat files
            string actualCompilerPath = compilerPath;
            string arguments = $"-t python \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            if (compilerPath.StartsWith("node:\""))
            {
                actualCompilerPath = "node";
                string jsPath = compilerPath.Substring(6).TrimEnd('"');
                arguments = $"\"{jsPath}\" -t python \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            }
            else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                // Use cmd.exe to run .bat file on Windows
                actualCompilerPath = "cmd.exe";
                arguments = $"/c \"{compilerPath}\" -t python \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            }

            // Compile to Python
            var processInfo = new ProcessStartInfo
            {
                FileName = actualCompilerPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(GuiKsyPath)
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0, "Python compilation should succeed");
                }
            }

            // Verify Python parser file was generated
            string[] pythonFiles = Directory.GetFiles(langOutputDir, "*.py", SearchOption.AllDirectories);
            pythonFiles.Should().NotBeEmpty("Python parser files should be generated");

            // Actually use the generated parser to validate it works with Python runtime and kaitaistruct library
            string pythonCmd = FindPythonRuntime();
            if (string.IsNullOrEmpty(pythonCmd))
            {
                return; // Skip if Python not available
            }

            // Check if kaitaistruct library is available
            if (!IsKaitaiStructLibraryAvailable(pythonCmd))
            {
                return; // Skip if kaitaistruct library not available
            }

            // Create Python script to use the generated parser
            string pythonScript = CreatePythonParserScript(TestGuiFile, langOutputDir);
            string tempScriptFile = Path.GetTempFileName();
            File.WriteAllText(tempScriptFile, pythonScript);

            try
            {
                // Execute Python script
                var pythonInfo = new ProcessStartInfo
                {
                    FileName = pythonCmd,
                    Arguments = $"\"{tempScriptFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(TestGuiFile)
                };

                string pythonStdout = "";
                string pythonStderr = "";
                int pythonExitCode = -1;

                using (var process = Process.Start(pythonInfo))
                {
                    if (process != null)
                    {
                        pythonStdout = process.StandardOutput.ReadToEnd();
                        pythonStderr = process.StandardError.ReadToEnd();
                        process.WaitForExit(60000);
                        pythonExitCode = process.ExitCode;
                    }
                }

                // Python parser should execute successfully
                pythonExitCode.Should().Be(0,
                    $"Python parser should parse GUI file successfully. STDOUT: {pythonStdout}, STDERR: {pythonStderr}");

                // Validate that parsing succeeded (output should contain GUI structure info)
                pythonStdout.Should().NotBeNullOrEmpty("Python parser should produce output");
                pythonStdout.Should().NotContain("\"error\"", "Python parser should not report errors");
            }
            finally
            {
                if (File.Exists(tempScriptFile))
                {
                    try
                    {
                        File.Delete(tempScriptFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestCompiledCSharpParserStructure()
        {
            // Test C# parser compilation and basic structure validation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            string langOutputDir = Path.Combine(CompilerOutputDir, "csharp");
            if (Directory.Exists(langOutputDir))
            {
                Directory.Delete(langOutputDir, true);
            }
            Directory.CreateDirectory(langOutputDir);

            // Handle node-based compiler path and .bat files
            string actualCompilerPath = compilerPath;
            string arguments = $"-t csharp \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            if (compilerPath.StartsWith("node:\""))
            {
                actualCompilerPath = "node";
                string jsPath = compilerPath.Substring(6).TrimEnd('"');
                arguments = $"\"{jsPath}\" -t csharp \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            }
            else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                // Use cmd.exe to run .bat file on Windows
                actualCompilerPath = "cmd.exe";
                arguments = $"/c \"{compilerPath}\" -t csharp \"{GuiKsyPath}\" -d \"{langOutputDir}\"";
            }

            // Compile to C#
            var compileInfo = new ProcessStartInfo
            {
                FileName = actualCompilerPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(GuiKsyPath)
            };

            using (var process = Process.Start(compileInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(60000);
                    process.ExitCode.Should().Be(0,
                        $"C# compilation should succeed. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }

            // Verify C# parser file was generated
            string[] csFiles = Directory.GetFiles(langOutputDir, "*.cs", SearchOption.AllDirectories);
            csFiles.Should().NotBeEmpty("C# parser files should be generated");

            // Verify generated C# file contains expected structure
            string guiCsFile = csFiles.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains("gui"));
            if (guiCsFile != null)
            {
                string csContent = File.ReadAllText(guiCsFile);
                csContent.Should().Contain("class", "Generated C# file should contain class definition");
                csContent.Should().Contain("GffHeader", "Generated C# file should contain GffHeader structure");
            }
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsySyntaxValidation()
        {
            // Validate GUI.ksy syntax by attempting compilation
            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                return; // Skip if compiler not available
            }

            // Handle node-based compiler path and .bat files
            string actualCompilerPath = compilerPath;
            string arguments = $"-t python \"{GuiKsyPath}\" --debug";
            if (compilerPath.StartsWith("node:\""))
            {
                actualCompilerPath = "node";
                string jsPath = compilerPath.Substring(6).TrimEnd('"');
                arguments = $"\"{jsPath}\" -t python \"{GuiKsyPath}\" --debug";
            }
            else if (compilerPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                // Use cmd.exe to run .bat file on Windows
                actualCompilerPath = "cmd.exe";
                arguments = $"/c \"{compilerPath}\" -t python \"{GuiKsyPath}\" --debug";
            }

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = actualCompilerPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(GuiKsyPath)
            };

            using (var process = Process.Start(validateInfo))
            {
                if (process != null)
                {
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);

                    // Compiler should not report syntax errors
                    stderr.Should().NotContain("error", "GUI.ksy should not have syntax errors");
                    process.ExitCode.Should().Be(0,
                        $"GUI.ksy syntax should be valid. STDOUT: {stdout}, STDERR: {stderr}");
                }
            }
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyDefinitionCompleteness()
        {
            // Validate that GUI.ksy definition is complete and includes all GUI structures
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for required elements in Kaitai Struct definition
            ksyContent.Should().Contain("meta:", "Should have meta section");
            ksyContent.Should().Contain("id: gui", "Should have id: gui");
            ksyContent.Should().Contain("file-extension: gui", "Should define gui file extension");
            ksyContent.Should().Contain("GUI ", "Should include GUI file type signature");

            // Check for GFF structure
            ksyContent.Should().Contain("gff_header", "Should define gff_header type");
            ksyContent.Should().Contain("file_type", "Should define file_type field");
            ksyContent.Should().Contain("file_version", "Should define file_version field");
            ksyContent.Should().Contain("struct_array", "Should define struct_array");
            ksyContent.Should().Contain("field_array", "Should define field_array");
            ksyContent.Should().Contain("label_array", "Should define label_array");
            ksyContent.Should().Contain("field_data", "Should define field_data section");
            ksyContent.Should().Contain("field_indices", "Should define field_indices array");
            ksyContent.Should().Contain("list_indices", "Should define list_indices array");

            // Check for GUI-specific types
            ksyContent.Should().Contain("gui_root", "Should define gui_root type");
            ksyContent.Should().Contain("gui_control", "Should define gui_control type");
            ksyContent.Should().Contain("gui_extent", "Should define gui_extent type");
            ksyContent.Should().Contain("gui_border", "Should define gui_border type");
            ksyContent.Should().Contain("gui_text", "Should define gui_text type");
            ksyContent.Should().Contain("gui_moveto", "Should define gui_moveto type");
            ksyContent.Should().Contain("gui_scrollbar", "Should define gui_scrollbar type");
            ksyContent.Should().Contain("gui_progress", "Should define gui_progress type");
            ksyContent.Should().Contain("gui_listbox", "Should define gui_listbox type");
            ksyContent.Should().Contain("gui_slider", "Should define gui_slider type");
            ksyContent.Should().Contain("gui_checkbox", "Should define gui_checkbox type");
            ksyContent.Should().Contain("gui_button", "Should define gui_button type");
            ksyContent.Should().Contain("gui_label", "Should define gui_label type");
            ksyContent.Should().Contain("gui_panel", "Should define gui_panel type");
            ksyContent.Should().Contain("gui_protoitem", "Should define gui_protoitem type");

            // Check for field data types
            ksyContent.Should().Contain("resref_data", "Should define resref_data type");
            ksyContent.Should().Contain("string_data", "Should define string_data type");
            ksyContent.Should().Contain("localized_string_data", "Should define localized_string_data type");
            ksyContent.Should().Contain("vector3", "Should define vector3 type");
            ksyContent.Should().Contain("vector4", "Should define vector4 type");

            // Check for enums
            ksyContent.Should().Contain("gff_field_type", "Should define gff_field_type enum");
            ksyContent.Should().Contain("gui_control_type", "Should define gui_control_type enum");
            ksyContent.Should().Contain("gui_text_alignment", "Should define gui_text_alignment enum");
            ksyContent.Should().Contain("gui_fill_style", "Should define gui_fill_style enum");
            ksyContent.Should().Contain("gui_slider_direction", "Should define gui_slider_direction enum");
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyGffHeaderStructure()
        {
            // Validate that GUI.ksy correctly defines the GFF header structure (56 bytes)
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for all required GFF header fields
            ksyContent.Should().Contain("file_type", "Header should define file_type");
            ksyContent.Should().Contain("file_version", "Header should define file_version");
            ksyContent.Should().Contain("struct_array_offset", "Header should define struct_array_offset");
            ksyContent.Should().Contain("struct_count", "Header should define struct_count");
            ksyContent.Should().Contain("field_array_offset", "Header should define field_array_offset");
            ksyContent.Should().Contain("field_count", "Header should define field_count");
            ksyContent.Should().Contain("label_array_offset", "Header should define label_array_offset");
            ksyContent.Should().Contain("label_count", "Header should define label_count");
            ksyContent.Should().Contain("field_data_offset", "Header should define field_data_offset");
            ksyContent.Should().Contain("field_data_count", "Header should define field_data_count");
            ksyContent.Should().Contain("field_indices_offset", "Header should define field_indices_offset");
            ksyContent.Should().Contain("field_indices_count", "Header should define field_indices_count");
            ksyContent.Should().Contain("list_indices_offset", "Header should define list_indices_offset");
            ksyContent.Should().Contain("list_indices_count", "Header should define list_indices_count");

            // Check for file type validation
            ksyContent.Should().Contain("\"GUI \"", "Should validate GUI file type signature");
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyControlTypes()
        {
            // Validate that GUI.ksy correctly defines all control types
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for control type enum values
            ksyContent.Should().Contain("-1: invalid", "Should define invalid control type");
            ksyContent.Should().Contain("0: control", "Should define control type");
            ksyContent.Should().Contain("2: panel", "Should define panel type");
            ksyContent.Should().Contain("4: proto_item", "Should define proto_item type");
            ksyContent.Should().Contain("5: label", "Should define label type");
            ksyContent.Should().Contain("6: button", "Should define button type");
            ksyContent.Should().Contain("7: checkbox", "Should define checkbox type");
            ksyContent.Should().Contain("8: slider", "Should define slider type");
            ksyContent.Should().Contain("9: scrollbar", "Should define scrollbar type");
            ksyContent.Should().Contain("10: progress", "Should define progress type");
            ksyContent.Should().Contain("11: listbox", "Should define listbox type");
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyFieldDataTypes()
        {
            // Validate that GUI.ksy correctly defines field data types
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for ResRef format (1-byte length + 16-byte string)
            ksyContent.Should().Contain("resref_data", "Should define resref_data type");
            ksyContent.Should().Contain("length", "ResRef should define length field");
            ksyContent.Should().Contain("name", "ResRef should define name field");

            // Check for String format (4-byte length + string data)
            ksyContent.Should().Contain("string_data", "Should define string_data type");

            // Check for LocalizedString format
            ksyContent.Should().Contain("localized_string_data", "Should define localized_string_data type");
            ksyContent.Should().Contain("string_ref", "LocalizedString should define string_ref field");
            ksyContent.Should().Contain("string_count", "LocalizedString should define string_count field");
            ksyContent.Should().Contain("substrings", "LocalizedString should define substrings field");

            // Check for Vector3 format (3×float32)
            ksyContent.Should().Contain("vector3", "Should define vector3 type");
            ksyContent.Should().Contain("x", "Vector3 should define x component");
            ksyContent.Should().Contain("y", "Vector3 should define y component");
            ksyContent.Should().Contain("z", "Vector3 should define z component");
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyTextAlignmentEnum()
        {
            // Validate that GUI.ksy correctly defines text alignment enum
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for text alignment enum values
            ksyContent.Should().Contain("gui_text_alignment", "Should define gui_text_alignment enum");
            ksyContent.Should().Contain("1: top_left", "Should define top_left alignment");
            ksyContent.Should().Contain("2: top_center", "Should define top_center alignment");
            ksyContent.Should().Contain("3: top_right", "Should define top_right alignment");
            ksyContent.Should().Contain("17: center_left", "Should define center_left alignment");
            ksyContent.Should().Contain("18: center", "Should define center alignment");
            ksyContent.Should().Contain("19: center_right", "Should define center_right alignment");
            ksyContent.Should().Contain("33: bottom_left", "Should define bottom_left alignment");
            ksyContent.Should().Contain("34: bottom_center", "Should define bottom_center alignment");
            ksyContent.Should().Contain("35: bottom_right", "Should define bottom_right alignment");
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyFillStyleEnum()
        {
            // Validate that GUI.ksy correctly defines fill style enum
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for fill style enum values
            ksyContent.Should().Contain("gui_fill_style", "Should define gui_fill_style enum");
            ksyContent.Should().Contain("-1: none", "Should define none fill style");
            ksyContent.Should().Contain("0: empty", "Should define empty fill style");
            ksyContent.Should().Contain("1: solid", "Should define solid fill style");
            ksyContent.Should().Contain("2: texture", "Should define texture fill style");
        }

        [Fact(Timeout = 300000)]
        public void TestGuiKsyGffFieldTypeEnum()
        {
            // Validate that GUI.ksy correctly defines GFF field type enum
            if (!File.Exists(GuiKsyPath))
            {
                return; // Skip if file doesn't exist
            }

            string ksyContent = File.ReadAllText(GuiKsyPath);

            // Check for GFF field type enum values
            ksyContent.Should().Contain("gff_field_type", "Should define gff_field_type enum");
            ksyContent.Should().Contain("0: uint8", "Should define uint8 field type");
            ksyContent.Should().Contain("5: int32", "Should define int32 field type");
            ksyContent.Should().Contain("8: single", "Should define single field type");
            ksyContent.Should().Contain("10: string", "Should define string field type");
            ksyContent.Should().Contain("11: resref", "Should define resref field type");
            ksyContent.Should().Contain("12: localized_string", "Should define localized_string field type");
            ksyContent.Should().Contain("14: struct", "Should define struct field type");
            ksyContent.Should().Contain("15: list", "Should define list field type");
            ksyContent.Should().Contain("17: vector3", "Should define vector3 field type");
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        private static string FindKaitaiCompiler()
        {
            // Try common locations and PATH
            string npmGlobalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");
            string npmModulePath = Path.Combine(npmGlobalPath, "node_modules", "kaitai-struct-compiler");
            string npmCmdPath = Path.Combine(npmGlobalPath, "kaitai-struct-compiler.cmd");
            string npmJsPath = Path.Combine(npmModulePath, "kaitai-struct-compiler.js");

            string[] possiblePaths = new[]
            {
                "kaitai-struct-compiler",
                "ksc",
                @"C:\Program Files (x86)\kaitai-struct-compiler\bin\kaitai-struct-compiler.bat",
                npmCmdPath,
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
                        // Use cmd.exe to run .bat file on Windows
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

            // Try using node to run the npm-installed compiler
            if (File.Exists(npmJsPath))
            {
                try
                {
                    string nodePath = "node";
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = nodePath,
                        Arguments = $"\"{npmJsPath}\" --version",
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
                                // Return a special marker that we'll handle in the compilation calls
                                return $"node:\"{npmJsPath}\"";
                            }
                        }
                    }
                }
                catch
                {
                    // Continue
                }
            }

            return null;
        }

        private static void CreateTestGuiFile(string path)
        {
            // Create a comprehensive valid GFF file with "GUI " signature
            // This test file includes all control types with complete control structures
            // Based on PyKotor GUI format documentation: vendor/PyKotor/wiki/GFF-GUI.md
            // swkotor2.exe GUI loading: FUN_0070a2e0 @ 0x0070a2e0 - GUI file structure parsing
            var gff = new GFF(GFFContent.GUI);
            gff.Root.SetString("Tag", "TestGUI");
            gff.Root.SetString("Comment", "Comprehensive test GUI file with all control types");

            // Create controls list with multiple control types
            var controlsList = new GFFList();

            // 1. Panel control (type 2) - Root container panel
            var panelControl = CreatePanelControl(1, "ROOT_PANEL", 0, 0, 640, 480);
            controlsList.Add(panelControl);

            // 2. Button control (type 6)
            var buttonControl = CreateButtonControl(2, "TEST_BUTTON", 50, 50, 150, 40);
            controlsList.Add(buttonControl);

            // 3. Label control (type 5)
            var labelControl = CreateLabelControl(3, "TEST_LABEL", 50, 100, 200, 30);
            controlsList.Add(labelControl);

            // 4. CheckBox control (type 7)
            var checkboxControl = CreateCheckBoxControl(4, "TEST_CHECKBOX", 50, 140, 150, 30);
            controlsList.Add(checkboxControl);

            // 5. Slider control (type 8)
            var sliderControl = CreateSliderControl(5, "TEST_SLIDER", 50, 180, 200, 30);
            controlsList.Add(sliderControl);

            // 6. ScrollBar control (type 9)
            var scrollbarControl = CreateScrollBarControl(6, "TEST_SCROLLBAR", 300, 50, 20, 200);
            controlsList.Add(scrollbarControl);

            // 7. ProgressBar control (type 10)
            var progressControl = CreateProgressBarControl(7, "TEST_PROGRESS", 50, 220, 250, 25);
            controlsList.Add(progressControl);

            // 8. ListBox control (type 11) with ProtoItem template
            var listboxControl = CreateListBoxControl(8, "TEST_LISTBOX", 300, 300, 300, 150);
            controlsList.Add(listboxControl);

            gff.Root.SetList("CONTROLS", controlsList);

            // Write GFF file
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GFFAuto.WriteGff(gff, path, ResourceType.GUI);
        }

        /// <summary>
        /// Creates a Panel control with complete properties.
        /// Panel type: 2
        /// </summary>
        private static GFFStruct CreatePanelControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(2);
            control.SetInt32("CONTROLTYPE", 2); // Panel
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // BORDER struct with all properties
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uipnl_corner"));
            border.SetResRef("EDGE", new ResRef("uipnl_edge"));
            border.SetResRef("FILL", new ResRef("uipnl_fill"));
            border.SetInt32("FILLSTYLE", 2); // Texture fill
            border.SetInt32("DIMENSION", 4);
            border.SetInt32("INNEROFFSET", 2);
            border.SetInt32("INNEROFFSETY", 2);
            border.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f)); // White
            border.SetUInt8("PULSING", 0);
            control.SetStruct("BORDER", border);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(0.8f, 0.8f, 0.9f)); // Light blue tint
            control.SetSingle("ALPHA", 1.0f); // Fully opaque

            // Child controls list (Panel can contain children)
            var childControls = new GFFList();
            var childButton = CreateButtonControl(id * 100 + 1, "CHILD_BUTTON", 10, 10, 100, 30);
            childControls.Add(childButton);
            control.SetList("CONTROLS", childControls);

            return control;
        }

        /// <summary>
        /// Creates a Button control with complete properties.
        /// Button type: 6
        /// </summary>
        private static GFFStruct CreateButtonControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(6);
            control.SetInt32("CONTROLTYPE", 6); // Button
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // BORDER struct
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uibtn_corner"));
            border.SetResRef("EDGE", new ResRef("uibtn_edge"));
            border.SetResRef("FILL", new ResRef("uibtn_fill"));
            border.SetInt32("FILLSTYLE", 2); // Texture
            border.SetInt32("DIMENSION", 2);
            border.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetStruct("BORDER", border);

            // HILIGHT struct (hover state)
            var hilight = new GFFStruct();
            hilight.SetResRef("CORNER", new ResRef("uibtn_hcorner"));
            hilight.SetResRef("EDGE", new ResRef("uibtn_hedge"));
            hilight.SetResRef("FILL", new ResRef("uibtn_hfill"));
            hilight.SetInt32("FILLSTYLE", 2);
            hilight.SetInt32("DIMENSION", 2);
            hilight.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 0.5f)); // Yellow tint for highlight
            hilight.SetUInt8("PULSING", 0);
            control.SetStruct("HILIGHT", hilight);

            // TEXT struct
            var text = new GFFStruct();
            text.SetString("TEXT", "Test Button");
            text.SetUInt32("STRREF", 0xFFFFFFFF); // No string reference
            text.SetResRef("FONT", new ResRef("fnt_d16x16"));
            text.SetUInt32("ALIGNMENT", 18); // Center alignment
            text.SetVector3("COLOR", new Vector3(0.0f, 0.659f, 0.980f)); // KotOR cyan
            text.SetUInt8("PULSING", 0);
            control.SetStruct("TEXT", text);

            // MOVETO struct (navigation)
            var moveto = new GFFStruct();
            moveto.SetInt32("UP", -1); // No navigation up
            moveto.SetInt32("DOWN", id + 1); // Navigate to next control
            moveto.SetInt32("LEFT", -1);
            moveto.SetInt32("RIGHT", id + 1);
            control.SetStruct("MOVETO", moveto);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);
            control.SetUInt8("PULSING", 0); // Button-level pulsing

            return control;
        }

        /// <summary>
        /// Creates a Label control with complete properties.
        /// Label type: 5
        /// </summary>
        private static GFFStruct CreateLabelControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(5);
            control.SetInt32("CONTROLTYPE", 5); // Label
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // TEXT struct
            var text = new GFFStruct();
            text.SetString("TEXT", "Test Label Text");
            text.SetUInt32("STRREF", 0xFFFFFFFF);
            text.SetResRef("FONT", new ResRef("fnt_d16x16"));
            text.SetUInt32("ALIGNMENT", 1); // Top-Left alignment
            text.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f)); // White
            text.SetUInt8("PULSING", 0);
            control.SetStruct("TEXT", text);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);

            return control;
        }

        /// <summary>
        /// Creates a CheckBox control with complete properties.
        /// CheckBox type: 7
        /// </summary>
        private static GFFStruct CreateCheckBoxControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(7);
            control.SetInt32("CONTROLTYPE", 7); // CheckBox
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // BORDER struct
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uichk_corner"));
            border.SetResRef("EDGE", new ResRef("uichk_edge"));
            border.SetResRef("FILL", new ResRef("uichk_fill"));
            border.SetInt32("FILLSTYLE", 2);
            border.SetInt32("DIMENSION", 2);
            border.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetStruct("BORDER", border);

            // SELECTED struct (checked state)
            var selected = new GFFStruct();
            selected.SetResRef("CORNER", new ResRef("uichk_scorner"));
            selected.SetResRef("EDGE", new ResRef("uichk_sedge"));
            selected.SetResRef("FILL", new ResRef("uichk_sfill"));
            selected.SetInt32("FILLSTYLE", 2);
            selected.SetInt32("DIMENSION", 2);
            selected.SetVector3("COLOR", new Vector3(0.5f, 1.0f, 0.5f)); // Green tint when selected
            selected.SetUInt8("PULSING", 0);
            control.SetStruct("SELECTED", selected);

            // HILIGHTSELECTED struct (checked and hovered state)
            var hilightSelected = new GFFStruct();
            hilightSelected.SetResRef("CORNER", new ResRef("uichk_hscorner"));
            hilightSelected.SetResRef("EDGE", new ResRef("uichk_hsedge"));
            hilightSelected.SetResRef("FILL", new ResRef("uichk_hsfill"));
            hilightSelected.SetInt32("FILLSTYLE", 2);
            hilightSelected.SetInt32("DIMENSION", 2);
            hilightSelected.SetVector3("COLOR", new Vector3(0.7f, 1.0f, 0.7f)); // Brighter green
            hilightSelected.SetUInt8("PULSING", 0);
            control.SetStruct("HILIGHTSELECTED", hilightSelected);

            // ISSELECTED - initial state (0 = unchecked, 1 = checked)
            control.SetUInt8("ISSELECTED", 0);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);

            return control;
        }

        /// <summary>
        /// Creates a Slider control with complete properties.
        /// Slider type: 8
        /// </summary>
        private static GFFStruct CreateSliderControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(8);
            control.SetInt32("CONTROLTYPE", 8); // Slider
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // Slider-specific properties
            control.SetInt32("MAXVALUE", 100);
            control.SetInt32("CURVALUE", 50); // Current value (50%)
            control.SetInt32("DIRECTION", 0); // 0 = horizontal, 1 = vertical

            // THUMB struct
            var thumb = new GFFStruct();
            thumb.SetResRef("IMAGE", new ResRef("uislider_thumb"));
            thumb.SetInt32("ALIGNMENT", 18); // Center
            thumb.SetInt32("DRAWSTYLE", 0);
            thumb.SetInt32("FLIPSTYLE", 0);
            thumb.SetSingle("ROTATE", 0.0f);
            control.SetStruct("THUMB", thumb);

            // BORDER struct (track appearance)
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uislider_corner"));
            border.SetResRef("EDGE", new ResRef("uislider_edge"));
            border.SetResRef("FILL", new ResRef("uislider_fill"));
            border.SetInt32("FILLSTYLE", 2);
            border.SetInt32("DIMENSION", 2);
            border.SetVector3("COLOR", new Vector3(0.5f, 0.5f, 0.5f)); // Gray track
            control.SetStruct("BORDER", border);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);

            return control;
        }

        /// <summary>
        /// Creates a ScrollBar control with complete properties.
        /// ScrollBar type: 9
        /// </summary>
        private static GFFStruct CreateScrollBarControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(9);
            control.SetInt32("CONTROLTYPE", 9); // ScrollBar
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // ScrollBar-specific properties
            control.SetInt32("MAXVALUE", 100);
            control.SetInt32("VISIBLEVALUE", 10); // Number of visible items
            control.SetInt32("CURVALUE", 0); // Current scroll position
            control.SetUInt8("DRAWMODE", 0); // Normal draw mode

            // DIR struct (direction arrow buttons)
            var dir = new GFFStruct();
            dir.SetResRef("IMAGE", new ResRef("uiscroll_dir"));
            dir.SetInt32("ALIGNMENT", 18); // Center
            control.SetStruct("DIR", dir);

            // THUMB struct
            var thumb = new GFFStruct();
            thumb.SetResRef("IMAGE", new ResRef("uiscroll_thumb"));
            thumb.SetInt32("ALIGNMENT", 18);
            thumb.SetInt32("DRAWSTYLE", 0);
            thumb.SetInt32("FLIPSTYLE", 0);
            thumb.SetSingle("ROTATE", 0.0f);
            control.SetStruct("THUMB", thumb);

            // BORDER struct (track)
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uiscroll_corner"));
            border.SetResRef("EDGE", new ResRef("uiscroll_edge"));
            border.SetResRef("FILL", new ResRef("uiscroll_fill"));
            border.SetInt32("FILLSTYLE", 1); // Solid fill
            border.SetInt32("DIMENSION", 2);
            border.SetVector3("COLOR", new Vector3(0.3f, 0.3f, 0.3f));
            control.SetStruct("BORDER", border);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);

            return control;
        }

        /// <summary>
        /// Creates a ProgressBar control with complete properties.
        /// ProgressBar type: 10
        /// </summary>
        private static GFFStruct CreateProgressBarControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(10);
            control.SetInt32("CONTROLTYPE", 10); // ProgressBar
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // ProgressBar-specific properties
            control.SetInt32("MAXVALUE", 100);
            control.SetInt32("CURVALUE", 75); // 75% progress
            control.SetInt32("STARTFROMLEFT", 1); // Fill from left (1) or right (0)

            // PROGRESS struct (progress fill appearance)
            var progress = new GFFStruct();
            progress.SetResRef("CORNER", new ResRef("uiprog_corner"));
            progress.SetResRef("EDGE", new ResRef("uiprog_edge"));
            progress.SetResRef("FILL", new ResRef("uiprog_fill"));
            progress.SetInt32("FILLSTYLE", 2); // Texture
            progress.SetInt32("DIMENSION", 2);
            progress.SetInt32("INNEROFFSET", 0);
            progress.SetVector3("COLOR", new Vector3(0.0f, 1.0f, 0.0f)); // Green progress fill
            progress.SetUInt8("PULSING", 0);
            control.SetStruct("PROGRESS", progress);

            // BORDER struct (background/track)
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uiprog_bcorner"));
            border.SetResRef("EDGE", new ResRef("uiprog_bedge"));
            border.SetResRef("FILL", new ResRef("uiprog_bfill"));
            border.SetInt32("FILLSTYLE", 1); // Solid
            border.SetInt32("DIMENSION", 2);
            border.SetVector3("COLOR", new Vector3(0.2f, 0.2f, 0.2f)); // Dark background
            control.SetStruct("BORDER", border);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);

            return control;
        }

        /// <summary>
        /// Creates a ListBox control with complete properties including ProtoItem template.
        /// ListBox type: 11
        /// </summary>
        private static GFFStruct CreateListBoxControl(int id, string tag, int left, int top, int width, int height)
        {
            var control = new GFFStruct(11);
            control.SetInt32("CONTROLTYPE", 11); // ListBox
            control.SetInt32("ID", id);
            control.SetString("TAG", tag);

            // EXTENT struct
            var extent = new GFFStruct();
            extent.SetInt32("LEFT", left);
            extent.SetInt32("TOP", top);
            extent.SetInt32("WIDTH", width);
            extent.SetInt32("HEIGHT", height);
            control.SetStruct("EXTENT", extent);

            // ListBox-specific properties
            control.SetInt32("PADDING", 5); // Spacing between items
            control.SetUInt8("LOOPING", 1); // Enable looping scroll
            control.SetInt32("MAXVALUE", 20); // Maximum scroll value
            control.SetInt32("LEFTSCROLLBAR", 0); // Scrollbar on right (0) or left (1)

            // PROTOITEM struct (template for list items)
            var protoItem = new GFFStruct(4); // ProtoItem type
            protoItem.SetInt32("CONTROLTYPE", 4); // ProtoItem
            protoItem.SetInt32("ID", 0); // Template doesn't need ID
            protoItem.SetString("TAG", "LISTITEM");

            // ProtoItem EXTENT
            var protoExtent = new GFFStruct();
            protoExtent.SetInt32("LEFT", 0);
            protoExtent.SetInt32("TOP", 0);
            protoExtent.SetInt32("WIDTH", width - 25); // Account for scrollbar
            protoExtent.SetInt32("HEIGHT", 30); // Item height
            protoItem.SetStruct("EXTENT", protoExtent);

            // ProtoItem BORDER
            var protoBorder = new GFFStruct();
            protoBorder.SetResRef("CORNER", new ResRef("uilist_corner"));
            protoBorder.SetResRef("EDGE", new ResRef("uilist_edge"));
            protoBorder.SetResRef("FILL", new ResRef("uilist_fill"));
            protoBorder.SetInt32("FILLSTYLE", 0); // Empty fill
            protoBorder.SetInt32("DIMENSION", 1);
            protoBorder.SetVector3("COLOR", new Vector3(0.2f, 0.2f, 0.3f));
            protoItem.SetStruct("BORDER", protoBorder);

            // ProtoItem HILIGHT
            var protoHilight = new GFFStruct();
            protoHilight.SetResRef("CORNER", new ResRef("uilist_hcorner"));
            protoHilight.SetResRef("EDGE", new ResRef("uilist_hedge"));
            protoHilight.SetResRef("FILL", new ResRef("uilist_hfill"));
            protoHilight.SetInt32("FILLSTYLE", 1); // Solid fill for highlight
            protoHilight.SetInt32("DIMENSION", 1);
            protoHilight.SetVector3("COLOR", new Vector3(0.4f, 0.4f, 0.6f));
            protoItem.SetStruct("HILIGHT", protoHilight);

            // ProtoItem SELECTED
            var protoSelected = new GFFStruct();
            protoSelected.SetResRef("CORNER", new ResRef("uilist_scorner"));
            protoSelected.SetResRef("EDGE", new ResRef("uilist_sedge"));
            protoSelected.SetResRef("FILL", new ResRef("uilist_sfill"));
            protoSelected.SetInt32("FILLSTYLE", 1);
            protoSelected.SetInt32("DIMENSION", 1);
            protoSelected.SetVector3("COLOR", new Vector3(0.2f, 0.6f, 0.2f)); // Green when selected
            protoItem.SetStruct("SELECTED", protoSelected);

            // ProtoItem HILIGHTSELECTED
            var protoHilightSelected = new GFFStruct();
            protoHilightSelected.SetResRef("CORNER", new ResRef("uilist_hscorner"));
            protoHilightSelected.SetResRef("EDGE", new ResRef("uilist_hsedge"));
            protoHilightSelected.SetResRef("FILL", new ResRef("uilist_hsfill"));
            protoHilightSelected.SetInt32("FILLSTYLE", 1);
            protoHilightSelected.SetInt32("DIMENSION", 1);
            protoHilightSelected.SetVector3("COLOR", new Vector3(0.3f, 0.7f, 0.3f));
            protoItem.SetStruct("HILIGHTSELECTED", protoHilightSelected);

            // ProtoItem TEXT
            var protoText = new GFFStruct();
            protoText.SetString("TEXT", "List Item");
            protoText.SetUInt32("STRREF", 0xFFFFFFFF);
            protoText.SetResRef("FONT", new ResRef("fnt_d16x16"));
            protoText.SetUInt32("ALIGNMENT", 1); // Top-Left
            protoText.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            protoItem.SetStruct("TEXT", protoText);

            protoItem.SetUInt8("ISSELECTED", 0);
            protoItem.SetUInt8("PULSING", 0);
            control.SetStruct("PROTOITEM", protoItem);

            // SCROLLBAR struct (embedded scrollbar)
            var scrollbar = new GFFStruct();
            scrollbar.SetInt32("MAXVALUE", 20);
            scrollbar.SetInt32("VISIBLEVALUE", 5); // 5 visible items
            scrollbar.SetInt32("CURVALUE", 0);

            // Scrollbar DIR
            var scrollbarDir = new GFFStruct();
            scrollbarDir.SetResRef("IMAGE", new ResRef("uiscroll_dir"));
            scrollbarDir.SetInt32("ALIGNMENT", 18);
            scrollbar.SetStruct("DIR", scrollbarDir);

            // Scrollbar THUMB
            var scrollbarThumb = new GFFStruct();
            scrollbarThumb.SetResRef("IMAGE", new ResRef("uiscroll_thumb"));
            scrollbarThumb.SetInt32("ALIGNMENT", 18);
            scrollbarThumb.SetInt32("DRAWSTYLE", 0);
            scrollbarThumb.SetInt32("FLIPSTYLE", 0);
            scrollbarThumb.SetSingle("ROTATE", 0.0f);
            scrollbar.SetStruct("THUMB", scrollbarThumb);

            control.SetStruct("SCROLLBAR", scrollbar);

            // BORDER struct (ListBox background)
            var border = new GFFStruct();
            border.SetResRef("CORNER", new ResRef("uilistbox_corner"));
            border.SetResRef("EDGE", new ResRef("uilistbox_edge"));
            border.SetResRef("FILL", new ResRef("uilistbox_fill"));
            border.SetInt32("FILLSTYLE", 1); // Solid
            border.SetInt32("DIMENSION", 2);
            border.SetVector3("COLOR", new Vector3(0.1f, 0.1f, 0.1f)); // Very dark background
            control.SetStruct("BORDER", border);

            // COLOR and ALPHA
            control.SetVector3("COLOR", new Vector3(1.0f, 1.0f, 1.0f));
            control.SetSingle("ALPHA", 1.0f);

            return control;
        }

        private static string FindPythonRuntime()
        {
            // Try common Python executable names
            string[] pythonCommands = new[] { "python3", "python" };

            foreach (string pythonCmd in pythonCommands)
            {
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = pythonCmd,
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
                                return pythonCmd;
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

        private static bool IsKaitaiStructLibraryAvailable(string pythonCmd)
        {
            try
            {
                // Try to import kaitaistruct to check if it's installed
                var processInfo = new ProcessStartInfo
                {
                    FileName = pythonCmd,
                    Arguments = "-c \"import kaitai_struct; print('ok')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit(5000);
                        if (process.ExitCode == 0 && output.Trim() == "ok")
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Library not available
            }

            return false;
        }

        private static string CreatePythonParserScript(string guiFilePath, string parserOutputDir)
        {
            // Find the generated GUI.py file
            string[] pyFiles = Directory.GetFiles(parserOutputDir, "*.py", SearchOption.AllDirectories);
            if (pyFiles.Length == 0)
            {
                throw new InvalidOperationException("No Python parser files found in output directory");
            }

            // Determine module name from the first .py file found (without extension)
            // Kaitai Struct generates module names based on .ksy file id, typically lowercase
            string firstPyFile = Path.GetFileNameWithoutExtension(pyFiles[0]);
            string moduleName = firstPyFile.ToLowerInvariant();

            // Escape backslashes in paths for Python string literals
            string escapedParserDir = parserOutputDir.Replace("\\", "\\\\");
            string escapedGuiPath = guiFilePath.Replace("\\", "\\\\");

            // Class name is typically capitalized version of module name (e.g., "gui" -> "Gui")
            string className = char.ToUpperInvariant(moduleName[0]) + (moduleName.Length > 1 ? moduleName.Substring(1) : "");

            string script = $@"import sys
import json
import os

# Add parser directory to path
sys.path.insert(0, r'{escapedParserDir}')

try:
    from kaitai_struct import KaitaiStream, BytesIO
    # Import the generated parser (module name matches .ksy file id)
    parser_module = __import__('{moduleName}')
    parser_class = getattr(parser_module, '{className}')

    # Read GUI file
    with open(r'{escapedGuiPath}', 'rb') as f:
        data = f.read()

    # Parse using generated parser
    stream = KaitaiStream(BytesIO(data))
    parsed = parser_class(stream)

    # Extract basic GFF header information to validate parsing
    # GUI files are GFF-based, so the root structure should have a gff_header
    result = {{'success': True, 'parsed': True}}

    # Try to extract GFF header information if available
    if hasattr(parsed, 'gff_header'):
        gff_header = parsed.gff_header
        result['file_type'] = gff_header.file_type.decode('ascii') if isinstance(gff_header.file_type, bytes) else str(gff_header.file_type)
        result['file_version'] = gff_header.file_version.decode('ascii') if isinstance(gff_header.file_version, bytes) else str(gff_header.file_version)
        result['struct_count'] = int(gff_header.struct_count) if hasattr(gff_header, 'struct_count') else 0
        result['field_count'] = int(gff_header.field_count) if hasattr(gff_header, 'field_count') else 0
        result['label_count'] = int(gff_header.label_count) if hasattr(gff_header, 'label_count') else 0

        # Validate that file type is 'GUI '
        file_type = result.get('file_type', '')
        if file_type != 'GUI ':
            result['warning'] = 'Expected file type \'GUI \', got \'' + str(file_type) + '\''
    else:
        # If no gff_header, at least verify parsing succeeded
        result['parsed_structure'] = True

    # Output JSON
    print(json.dumps(result))

except ImportError as e:
    error_result = {{'error': 'Import error: ' + str(e), 'success': False}}
    print(json.dumps(error_result), file=sys.stderr)
    sys.exit(1)
except Exception as e:
    error_result = {{'error': 'Parse error: ' + str(e), 'success': False}}
    print(json.dumps(error_result), file=sys.stderr)
    import traceback
    traceback.print_exc(file=sys.stderr)
    sys.exit(1)
";

            return script;
        }
    }
}

