using System;
using System.IO;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.RIM.RIMAuto;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Tests for RIM binary I/O operations.
    /// 1:1 port from tests/resource/formats/test_rim.py
    /// </summary>
    public class RIMFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.rim");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.rim");

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBinaryIO()
        {
            // Python: test_binary_io
            if (!File.Exists(BinaryTestFile))
            {
                // Skip if test file doesn't exist
                return;
            }

            // Python: rim: RIM = RIMBinaryReader(BINARY_TEST_FILE).load()
            RIM rim = new RIMBinaryReader(BinaryTestFile).Load();
            ValidateIO(rim);

            // Python: data: bytearray = bytearray()
            // Python: write_rim(rim, data)
            byte[] data = BytesRim(rim);

            // Python: rim = read_rim(data)
            rim = ReadRim(data);
            ValidateIO(rim);
        }

        private static void ValidateIO(RIM rim)
        {
            // Python: validate_io
            // Python: assert len(rim) == 3
            rim.Count.Should().Be(3);
            // Python: assert rim.get("1", ResourceType.TXT) == b"abc"
            rim.Get("1", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("abc"));
            // Python: assert rim.get("2", ResourceType.TXT) == b"def"
            rim.Get("2", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("def"));
            // Python: assert rim.get("3", ResourceType.TXT) == b"ghi"
            rim.Get("3", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("ghi"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReadRaises()
        {
            // test_read_raises from Python
            // Python: read_rim(".") raises PermissionError on Windows, IsADirectoryError on Unix
            Action act1 = () => ReadRim(".");
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Python: read_rim(DOES_NOT_EXIST_FILE) raises FileNotFoundError
            Action act2 = () => ReadRim(DoesNotExistFile);
            act2.Should().Throw<FileNotFoundException>();

            // Python: read_rim(CORRUPT_BINARY_TEST_FILE) raises ValueError
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => ReadRim(CorruptBinaryTestFile);
                act3.Should().Throw<InvalidDataException>(); // ValueError equivalent
            }
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWriteRaises()
        {
            // test_write_raises from Python
            var rim = new RIM();

            // Test writing to directory (should raise PermissionError on Windows, IsADirectoryError on Unix)
            // Python: write_rim(RIM(), ".", ResourceType.RIM)
            Action act1 = () => WriteRim(rim, ".", ResourceType.RIM);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Test invalid resource type (Python raises ValueError for ResourceType.INVALID)
            // Python: write_rim(RIM(), ".", ResourceType.INVALID)
            Action act2 = () => WriteRim(rim, ".", ResourceType.INVALID);
            act2.Should().Throw<ArgumentException>().WithMessage("*Unsupported format*");
        }

    }
}

