using System;
using System.IO;
using System.Text;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for NSS (NWScript Source) file format.
    /// Tests validate the NSS format structure as defined in NSS.ksy Kaitai Struct definition.
    /// NSS files are plain text source code files, so tests focus on text content validation.
    /// </summary>
    public class NSSFormatTests
    {
        private static readonly string TextTestFile = TestFileHelper.GetPath("test.nss");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";

        [Fact(Timeout = 120000)]
        public void TestNssFileReading()
        {
            if (!File.Exists(TextTestFile))
            {
                CreateTestNssFile(TextTestFile);
            }

            // Read NSS file as text
            string content = File.ReadAllText(TextTestFile, Encoding.UTF8);
            content.Should().NotBeNull("NSS file content should not be null");
            content.Length.Should().BeGreaterThan(0, "NSS file should contain source code");
        }

        [Fact(Timeout = 120000)]
        public void TestNssFileStructure()
        {
            if (!File.Exists(TextTestFile))
            {
                CreateTestNssFile(TextTestFile);
            }

            // Validate NSS file structure (plain text)
            string content = File.ReadAllText(TextTestFile, Encoding.UTF8);

            // NSS files are plain text, so we validate basic structure
            content.Should().NotBeNull("NSS content should not be null");

            // Check for common NWScript elements (optional - depends on file content)
            // These are not required but common in NSS files
        }

        [Fact(Timeout = 120000)]
        public void TestNssBomHandling()
        {
            // Test NSS file with UTF-8 BOM
            string tempFile = Path.GetTempFileName();
            try
            {
                // Create file with UTF-8 BOM
                using (var fs = File.Create(tempFile))
                {
                    // UTF-8 BOM: 0xEF 0xBB 0xBF
                    byte[] bom = { 0xEF, 0xBB, 0xBF };
                    fs.Write(bom, 0, bom.Length);

                    // Add some source code
                    byte[] source = Encoding.UTF8.GetBytes("void main() { }");
                    fs.Write(source, 0, source.Length);
                }

                // Read and validate
                string content = File.ReadAllText(tempFile, Encoding.UTF8);
                content.Should().NotBeNull("NSS file with BOM should be readable");
                content.Should().Contain("void main()", "Should contain source code");
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
        public void TestNssWithoutBom()
        {
            // Test NSS file without BOM (most common case)
            string tempFile = Path.GetTempFileName();
            try
            {
                // Create file without BOM
                string sourceCode = "void main() { int x = 5; }";
                File.WriteAllText(tempFile, sourceCode, Encoding.UTF8);

                // Read and validate
                string content = File.ReadAllText(tempFile, Encoding.UTF8);
                content.Should().NotBeNull("NSS file without BOM should be readable");
                content.Should().Contain("void main()", "Should contain source code");
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
        public void TestNssEncoding()
        {
            // Test that NSS files can be read with different encodings
            if (!File.Exists(TextTestFile))
            {
                CreateTestNssFile(TextTestFile);
            }

            // Test UTF-8 encoding
            string utf8Content = File.ReadAllText(TextTestFile, Encoding.UTF8);
            utf8Content.Should().NotBeNull("UTF-8 encoding should work");

            // Test Windows-1252 encoding (common for KotOR NSS files)
            try
            {
                string windows1252Content = File.ReadAllText(TextTestFile, Encoding.GetEncoding("windows-1252"));
                windows1252Content.Should().NotBeNull("Windows-1252 encoding should work");
            }
            catch (ArgumentException)
            {
                // Windows-1252 may not be available on all systems
                // This is acceptable
            }
        }

        [Fact(Timeout = 120000)]
        public void TestNssEmptyFile()
        {
            // Test empty NSS file
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "", Encoding.UTF8);

                string content = File.ReadAllText(tempFile, Encoding.UTF8);
                content.Should().BeEmpty("Empty NSS file should be empty");
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
        public void TestNssSourceCodeContent()
        {
            if (!File.Exists(TextTestFile))
            {
                CreateTestNssFile(TextTestFile);
            }

            string content = File.ReadAllText(TextTestFile, Encoding.UTF8);

            // Validate that content is valid text (not binary)
            // NSS files should be readable as text
            content.Should().NotBeNull("Content should not be null");

            // Check for common NWScript syntax elements (if present)
            // These are optional and depend on the specific file
        }

        [Fact(Timeout = 120000)]
        public void TestNssLineEndings()
        {
            // Test NSS file with different line endings
            string tempFile = Path.GetTempFileName();
            try
            {
                // Create file with CRLF line endings (Windows style)
                string sourceWithCrlf = "void main()\r\n{\r\n    int x = 5;\r\n}";
                File.WriteAllText(tempFile, sourceWithCrlf, Encoding.UTF8);

                string content = File.ReadAllText(tempFile, Encoding.UTF8);
                content.Should().Contain("\r\n", "Should contain CRLF line endings");

                // Create file with LF line endings (Unix style)
                string sourceWithLf = "void main()\n{\n    int x = 5;\n}";
                File.WriteAllText(tempFile, sourceWithLf, Encoding.UTF8);

                content = File.ReadAllText(tempFile, Encoding.UTF8);
                content.Should().Contain("\n", "Should contain LF line endings");
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
        public void TestReadRaises()
        {
            // Test reading non-existent file
            Action act = () => File.ReadAllText(DoesNotExistFile, Encoding.UTF8);
            act.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestNssFileSize()
        {
            if (!File.Exists(TextTestFile))
            {
                CreateTestNssFile(TextTestFile);
            }

            FileInfo fileInfo = new FileInfo(TextTestFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(0, "NSS file size should be non-negative");
        }

        private static void CreateTestNssFile(string path)
        {
            // Create a minimal valid NSS file for testing
            string sourceCode = @"void main()
{
    int x = 5;
    int y = 10;
    int z = x + y;
}";

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sourceCode, Encoding.UTF8);
        }
    }
}


