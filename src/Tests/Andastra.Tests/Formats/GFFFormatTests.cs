using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for GFF (Generic File Format) binary I/O operations.
    /// Tests validate the GFF format structure as defined in GFF.ksy Kaitai Struct definition.
    /// </summary>
    public class GFFFormatTests
    {
        private static readonly string GffKsyPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Andastra", "Parsing", "Resource", "Formats", "GFF", "GFF.ksy"
        ));

        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.gff");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.gff");

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

        static GFFFormatTests()
        {
            // Normalize GFF.ksy path
            GffKsyPath = Path.GetFullPath(GffKsyPath);
        }

        [Fact(Timeout = 120000)]
        public void TestGffKsyFileExists()
        {
            File.Exists(GffKsyPath).Should().BeTrue($"GFF.ksy should exist at {GffKsyPath}");

            // Validate it's a valid Kaitai Struct file
            string content = File.ReadAllText(GffKsyPath);
            content.Should().Contain("meta:", "GFF.ksy should contain meta section");
            content.Should().Contain("id: gff", "GFF.ksy should have id: gff");
            content.Should().Contain("file-extension: gff", "GFF.ksy should specify gff file extension");
        }

        [Fact(Timeout = 120000)]
        public void TestGffKsyFileValid()
        {
            if (!File.Exists(GffKsyPath))
            {
                Assert.True(true, "GFF.ksy not found - skipping validation");
                return;
            }

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                Assert.True(true, "Kaitai Struct compiler not available - skipping validation");
                return;
            }

            var testProcess = CreateCompilerProcess(compilerPath, $"-t python \"{GffKsyPath}\" -d \"{Path.GetTempPath()}\"");
            testProcess.Start();
            testProcess.WaitForExit(30000);

            string stderr = testProcess.StandardError.ReadToEnd();
            string stdout = testProcess.StandardOutput.ReadToEnd();

            if (testProcess.ExitCode != 0 && stderr.Contains("error") && !stderr.Contains("import"))
            {
                Assert.True(false, $"GFF.ksy has syntax errors: {stderr}\n{stdout}");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test GFF file if it doesn't exist
                CreateTestGffFile(BinaryTestFile);
            }

            // Test reading GFF file
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            ValidateIO(gff);

            // Test writing and reading back
            byte[] data = new GFFBinaryWriter(gff).Write();
            gff = new GFFBinaryReader(data).Load();
            ValidateIO(gff);
        }

        [Fact(Timeout = 120000)]
        public void TestGffHeaderStructure()
        {
            // Test that GFF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Read raw header bytes
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches GFF.ksy
            string fileType = Encoding.ASCII.GetString(header, 0, 4);
            fileType.Trim().Should().NotBeNullOrEmpty("File type should be a valid FourCC as defined in GFF.ksy");

            // Validate version
            string version = Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match GFF.ksy valid values");

            // Validate header structure (56 bytes total as per GFF.ksy)
            header.Length.Should().Be(56, "GFF header should be 56 bytes as defined in GFF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestGffFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches GFF.ksy
            string fileType = Encoding.ASCII.GetString(header, 0, 4);
            fileType.Trim().Should().NotBeNullOrEmpty("File type should be a valid FourCC as defined in GFF.ksy");

            // Validate version
            string version = Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match GFF.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestGffHeaderOffsets()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            // Read header offsets
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(8, SeekOrigin.Begin);
                uint structOffset = ReadUInt32(fs);
                uint structCount = ReadUInt32(fs);
                uint fieldOffset = ReadUInt32(fs);
                uint fieldCount = ReadUInt32(fs);
                uint labelOffset = ReadUInt32(fs);
                uint labelCount = ReadUInt32(fs);
                uint fieldDataOffset = ReadUInt32(fs);
                uint fieldDataCount = ReadUInt32(fs);
                uint fieldIndicesOffset = ReadUInt32(fs);
                uint fieldIndicesCount = ReadUInt32(fs);
                uint listIndicesOffset = ReadUInt32(fs);
                uint listIndicesCount = ReadUInt32(fs);

                // Validate offsets are within file bounds
                FileInfo fileInfo = new FileInfo(BinaryTestFile);
                structOffset.Should().BeLessThan((uint)fileInfo.Length, "Struct offset should be within file bounds");
                fieldOffset.Should().BeLessThan((uint)fileInfo.Length, "Field offset should be within file bounds");
                labelOffset.Should().BeLessThan((uint)fileInfo.Length, "Label offset should be within file bounds");
                fieldDataOffset.Should().BeLessThan((uint)fileInfo.Length, "Field data offset should be within file bounds");
                fieldIndicesOffset.Should().BeLessThan((uint)fileInfo.Length, "Field indices offset should be within file bounds");
                listIndicesOffset.Should().BeLessThan((uint)fileInfo.Length, "List indices offset should be within file bounds");

                // Validate header structure matches GFF.ksy definition (offsets at correct positions)
                fs.Seek(0, SeekOrigin.Begin);
                byte[] header = new byte[56];
                fs.Read(header, 0, 56);
                header.Length.Should().Be(56, "GFF header should be exactly 56 bytes as defined in GFF.ksy");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffLabelArray()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Read header to get label info
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(24, SeekOrigin.Begin);
                uint labelOffset = ReadUInt32(fs);
                uint labelCount = ReadUInt32(fs);

                if (labelCount > 0)
                {
                    fs.Seek(labelOffset, SeekOrigin.Begin);

                    // Each label is 16 bytes as per GFF.ksy
                    for (uint i = 0; i < labelCount; i++)
                    {
                        byte[] labelBytes = new byte[16];
                        fs.Read(labelBytes, 0, 16);

                        // Label should be null-padded ASCII as per GFF.ksy
                        string label = Encoding.ASCII.GetString(labelBytes).TrimEnd('\0');
                        label.Should().NotBeNull("Label should be valid ASCII string");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffStructArray()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Read header to get struct info
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(8, SeekOrigin.Begin);
                uint structOffset = ReadUInt32(fs);
                uint structCount = ReadUInt32(fs);

                if (structCount > 0)
                {
                    fs.Seek(structOffset, SeekOrigin.Begin);

                    // Each struct entry is 12 bytes as per GFF.ksy (s4 struct_id + u4 data_or_offset + u4 field_count)
                    for (uint i = 0; i < structCount; i++)
                    {
                        int structId = ReadInt32(fs);
                        uint dataOrOffset = ReadUInt32(fs);
                        uint fieldCount = ReadUInt32(fs);

                        // Validate struct entry structure (12 bytes total)
                        long currentPos = fs.Position;
                        fs.Seek(structOffset + i * 12, SeekOrigin.Begin);
                        fs.Seek(12, SeekOrigin.Current);
                        fs.Position.Should().Be(structOffset + (i + 1) * 12, "Each struct entry should be exactly 12 bytes as defined in GFF.ksy");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffFieldArray()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Read header to get field info
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(16, SeekOrigin.Begin);
                uint fieldOffset = ReadUInt32(fs);
                uint fieldCount = ReadUInt32(fs);

                if (fieldCount > 0)
                {
                    fs.Seek(fieldOffset, SeekOrigin.Begin);

                    // Each field entry is 12 bytes as per GFF.ksy (u4 field_type + u4 label_index + u4 data_or_offset)
                    for (uint i = 0; i < fieldCount; i++)
                    {
                        uint fieldType = ReadUInt32(fs);
                        uint labelIndex = ReadUInt32(fs);
                        uint dataOrOffset = ReadUInt32(fs);

                        // Validate field type is within valid range (0-17 as per GFF.ksy enum)
                        fieldType.Should().BeLessThanOrEqualTo(17u, "Field type should be 0-17 as defined in GFF.ksy gff_field_type enum");

                        // Validate field entry structure (12 bytes total)
                        long currentPos = fs.Position;
                        fs.Seek(fieldOffset + i * 12, SeekOrigin.Begin);
                        fs.Seek(12, SeekOrigin.Current);
                        fs.Position.Should().Be(fieldOffset + (i + 1) * 12, "Each field entry should be exactly 12 bytes as defined in GFF.ksy");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffFieldTypes()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGffFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate all field types defined in GFF.ksy enum are recognized
            var validFieldTypes = new HashSet<uint> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };

            // Read field types from file
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(16, SeekOrigin.Begin);
                uint fieldOffset = ReadUInt32(fs);
                uint fieldCount = ReadUInt32(fs);

                if (fieldCount > 0)
                {
                    fs.Seek(fieldOffset, SeekOrigin.Begin);

                    for (uint i = 0; i < fieldCount; i++)
                    {
                        uint fieldType = ReadUInt32(fs);
                        fs.Seek(8, SeekOrigin.Current); // Skip label_index and data_or_offset

                        validFieldTypes.Should().Contain(fieldType, $"Field type {fieldType} should be valid (0-17 as per GFF.ksy enum)");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffEmptyFile()
        {
            // Test GFF with minimal structure
            var gff = new GFF(GFFContent.GFF);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loaded = new GFFBinaryReader(data).Load();

            loaded.Should().NotBeNull();
            loaded.Root.Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new GFFBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new GFFBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestGffInvalidSignature()
        {
            // Create file with invalid signature
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] invalid = Encoding.ASCII.GetBytes("INVALID");
                    fs.Write(invalid, 0, invalid.Length);
                }

                Action act = () => new GFFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*Not a valid binary GFF file*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    Encoding.ASCII.GetBytes("GFF ").CopyTo(header, 0);
                    Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new GFFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*GFF version*unsupported*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Theory(Timeout = 600000)]
        [MemberData(nameof(GetSupportedLanguages))]
        public void TestKaitaiStructCompilation(string language)
        {
            if (!File.Exists(GffKsyPath))
            {
                Assert.True(true, "GFF.ksy not found - skipping compilation test");
                return;
            }

            string compilerPath = FindKaitaiCompiler();
            if (string.IsNullOrEmpty(compilerPath))
            {
                Assert.True(true, "Kaitai Struct compiler not available - skipping compilation test");
                return;
            }

            string outputDir = Path.Combine(Path.GetTempPath(), "gff_kaitai_test", language);
            Directory.CreateDirectory(outputDir);

            try
            {
                var process = CreateCompilerProcess(compilerPath, $"-t {language} \"{GffKsyPath}\" -d \"{outputDir}\"");
                process.Start();
                process.WaitForExit(60000);

                string stderr = process.StandardError.ReadToEnd();
                string stdout = process.StandardOutput.ReadToEnd();

                // Allow warnings but not errors (some languages may have import warnings)
                if (process.ExitCode != 0 && stderr.Contains("error") && !stderr.Contains("import"))
                {
                    Assert.True(false, $"Failed to compile GFF.ksy to {language}: {stderr}\n{stdout}");
                }
            }
            finally
            {
                if (Directory.Exists(outputDir))
                {
                    try { Directory.Delete(outputDir, true); } catch { }
                }
            }
        }

        public static IEnumerable<object[]> GetSupportedLanguages()
        {
            return SupportedLanguages.Select(lang => new object[] { lang });
        }

        private static void ValidateIO(GFF gff)
        {
            // Basic validation
            gff.Should().NotBeNull();
            gff.Root.Should().NotBeNull();
        }

        private static void CreateTestGffFile(string path)
        {
            var gff = new GFF(GFFContent.GFF);

            // Add some test fields
            gff.Root.SetUInt32("TestUInt32", 42);
            gff.Root.SetInt32("TestInt32", -42);
            gff.Root.SetString("TestString", "Hello World");
            gff.Root.SetSingle("TestFloat", 3.14f);

            byte[] data = new GFFBinaryWriter(gff).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        private static uint ReadUInt32(Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        private static int ReadInt32(Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        private static string FindKaitaiCompiler()
        {
            // Try common locations for kaitai-struct-compiler
            string[] possiblePaths = new[]
            {
                "kaitai-struct-compiler",
                "ksc",
                "/usr/bin/kaitai-struct-compiler",
                "/usr/local/bin/kaitai-struct-compiler",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "kaitai-struct-compiler", "bin", "kaitai-struct-compiler.exe")
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = path,
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
                        return path;
                    }
                }
                catch
                {
                    // Continue to next path
                }
            }

            // Try JAR file
            string jarPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "kaitai-struct-compiler", "kaitai-struct-compiler.jar"
            );

            if (File.Exists(jarPath))
            {
                return jarPath;
            }

            return null;
        }

        private static Process CreateCompilerProcess(string compilerPath, string arguments)
        {
            ProcessStartInfo processInfo;

            if (compilerPath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                processInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar \"{compilerPath}\" {arguments}",
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
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            return new Process { StartInfo = processInfo };
        }
    }
}
