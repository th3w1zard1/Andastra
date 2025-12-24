using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.TwoDA.TwoDAAuto;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Tests for 2DA binary I/O.
    /// 1:1 port of Python test_twoda.py from tests/resource/formats/test_twoda.py
    /// </summary>
    public class TwoDAFormatTests
    {
        private static readonly string TestTwoDAFile = TestFileHelper.GetPath("test.2da");
        private static readonly string CorruptTwoDAFile = TestFileHelper.GetPath("test_corrupted.2da");

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBinaryIO()
        {
            // Read 2DA file
            TwoDA twoda = new TwoDABinaryReader(TestTwoDAFile).Load();
            ValidateIO(twoda);

            // Write and re-read to validate round-trip
            byte[] data = new TwoDABinaryWriter(twoda).Write();
            twoda = new TwoDABinaryReader(data).Load();
            ValidateIO(twoda);
        }

        private static void ValidateIO(TwoDA twoda)
        {
            twoda.GetCellString(0, "col1").Should().Be("abc");
            twoda.GetCellString(0, "col2").Should().Be("def");
            twoda.GetCellString(0, "col3").Should().Be("ghi");

            twoda.GetCellString(1, "col1").Should().Be("def");
            twoda.GetCellString(1, "col2").Should().Be("ghi");
            twoda.GetCellString(1, "col3").Should().Be("123");

            twoda.GetCellString(2, "col1").Should().Be("123");
            twoda.GetCellString(2, "col2").Should().Be("");
            twoda.GetCellString(2, "col3").Should().Be("abc");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReadRaises()
        {
            // test_read_raises from Python
            // Test directory access
            Action act1 = () => new TwoDABinaryReader(".").Load();
            act1.Should().Throw<Exception>(); // UnauthorizedAccessException or IOException

            // Test file not found
            Action act2 = () => new TwoDABinaryReader("./thisfiledoesnotexist").Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test corrupted file
            Action act3 = () => new TwoDABinaryReader(CorruptTwoDAFile).Load();
            act3.Should().Throw<System.IO.InvalidDataException>();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWriteRaises()
        {
            // test_write_raises from Python
            var twoda = new TwoDA(new List<string> { "col1", "col2" });

            // Test writing to directory (should raise PermissionError on Windows, IsADirectoryError on Unix)
            // Python: write_2da(TwoDA(), ".", ResourceType.TwoDA)
            Action act1 = () => Write2DA(twoda, ".", ResourceType.TwoDA);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Test invalid resource type (Python raises ValueError for ResourceType.INVALID)
            // Python: write_2da(2DA(), ".", ResourceType.INVALID)
            Action act2 = () => Write2DA(twoda, ".", ResourceType.INVALID);
            act2.Should().Throw<ArgumentException>().WithMessage("*Unsupported format*");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRowMax()
        {
            // test_row_max from Python
            var twoda = new TwoDA();
            twoda.AddRow("0");
            twoda.AddRow("1");
            twoda.AddRow("2");

            twoda.LabelMax().Should().Be(3);
        }

    }
}

