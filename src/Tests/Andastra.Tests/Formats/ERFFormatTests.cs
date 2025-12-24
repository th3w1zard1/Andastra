using System;
using System.IO;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.ERF.ERFAuto;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Tests for ERF binary I/O operations.
    /// 1:1 port from tests/resource/formats/test_erf.py
    /// </summary>
    public class ERFFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.erf");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.erf");

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBinaryIO()
        {
            // Python: test_binary_io
            if (!File.Exists(BinaryTestFile))
            {
                // Skip if test file doesn't exist
                return;
            }

            // Python: erf = ERFBinaryReader(BINARY_TEST_FILE).load()
            ERF erf = new ERFBinaryReader(BinaryTestFile).Load();
            ValidateIO(erf);

            // Python: data = bytearray()
            // Python: write_erf(erf, data)
            byte[] data = BytesErf(erf);

            // Python: erf = ERFBinaryReader(data).load()
            erf = new ERFBinaryReader(data).Load();
            ValidateIO(erf);
        }

        private static void ValidateIO(ERF erf)
        {
            // Python: validate_io
            // Python: assert len(erf) == 3
            erf.Count.Should().Be(3);
            // Python: assert erf.get("1", ResourceType.TXT) == b"abc"
            erf.Get("1", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("abc"));
            // Python: assert erf.get("2", ResourceType.TXT) == b"def"
            erf.Get("2", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("def"));
            // Python: assert erf.get("3", ResourceType.TXT) == b"ghi"
            erf.Get("3", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("ghi"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReadRaises()
        {
            // test_read_raises from Python
            // Python: read_erf(".") raises PermissionError on Windows, IsADirectoryError on Unix
            Action act1 = () => ReadErf(".");
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Python: read_erf(DOES_NOT_EXIST_FILE) raises FileNotFoundError
            Action act2 = () => ReadErf(DoesNotExistFile);
            act2.Should().Throw<FileNotFoundException>();

            // Python: read_erf(CORRUPT_BINARY_TEST_FILE) raises ValueError
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => ReadErf(CorruptBinaryTestFile);
                act3.Should().Throw<InvalidDataException>(); // ValueError equivalent
            }
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWriteRaises()
        {
            // test_write_raises from Python
            var erf = new ERF(ERFType.ERF);

            // Test writing to directory (should raise PermissionError on Windows, IsADirectoryError on Unix)
            // Python: write_erf(ERF(ERFType.ERF), ".", ResourceType.ERF)
            Action act1 = () => WriteErf(erf, ".", ResourceType.ERF);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Test invalid resource type (Python raises ValueError for ResourceType.INVALID)
            // Python: write_erf(ERF(ERFType.ERF), ".", ResourceType.INVALID)
            Action act2 = () => WriteErf(erf, ".", ResourceType.INVALID);
            act2.Should().Throw<ArgumentException>().WithMessage("*Unsupported format*");
        }

    }
}

