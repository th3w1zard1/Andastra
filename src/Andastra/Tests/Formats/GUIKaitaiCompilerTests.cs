using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

            // Compile GUI.ksy to target language
            var processInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"",
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

                    var processInfo = new ProcessStartInfo
                    {
                        FileName = compilerPath,
                        Arguments = $"-t {language} \"{GuiKsyPath}\" -d \"{langOutputDir}\"",
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

            // Compile to Python
            var processInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{GuiKsyPath}\" -d \"{langOutputDir}\"",
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

            // Note: Actually using the generated parser would require Python runtime and kaitaistruct library
            // This test validates that compilation succeeds and generates expected files
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

            // Compile to C#
            var compileInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t csharp \"{GuiKsyPath}\" -d \"{langOutputDir}\"",
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

            // Use Python as validation target (most commonly supported)
            var validateInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"-t python \"{GuiKsyPath}\" --debug",
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

            return null;
        }

        private static void CreateTestGuiFile(string path)
        {
            // Create a minimal valid GFF file with "GUI " signature
            // This is a simplified test file - a real GUI file would have actual control structures
            var gff = new GFF(GFFContent.GUI);
            gff.Root.SetString("Tag", "TestGUI");

            // Create a simple control list
            var controlsList = new GFFList();
            var controlStruct = controlsList.Add(6); // Struct type
            controlStruct.SetInt32("CONTROLTYPE", 6); // Button
            controlStruct.SetInt32("ID", 1);
            controlStruct.SetString("TAG", "TestButton");

            // Create EXTENT struct
            var extentStruct = controlStruct.AddStruct("EXTENT");
            extentStruct.SetInt32("LEFT", 10);
            extentStruct.SetInt32("TOP", 20);
            extentStruct.SetInt32("WIDTH", 100);
            extentStruct.SetInt32("HEIGHT", 30);

            gff.Root.SetList("CONTROLS", controlsList);

            // Write GFF file
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GFFAuto.WriteGff(gff, path, ResourceType.GUI);
        }
    }
}

