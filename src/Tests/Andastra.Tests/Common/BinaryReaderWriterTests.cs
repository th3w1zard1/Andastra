using System;
using System.IO;
using System.Text;
using Andastra.Parsing;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Common
{

    /// <summary>
    /// Tests for BinaryReader and BinaryWriter.
    /// 1:1 port of Python test_stream.py from tests/common/test_stream.py
    /// </summary>
    public class BinaryReaderTests
    {
        private readonly byte[] _data1;
        private readonly byte[] _data2;
        private readonly byte[] _data3;
        private readonly byte[] _data4;

        public BinaryReaderTests()
        {
            // Register code pages encoding provider for windows-1252 support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            _data1 = new byte[] { 0x01, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            _data2 = Encoding.ASCII.GetBytes("helloworld\0");
            _data3 = new byte[] { 0xff, 0xfe, 0xff, 0xfd, 0xff, 0xff, 0xff, 0xfc, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
            _data4 = new byte[] { 0x79, 0xe9, 0xf6, 0xc2, 0x68, 0x91, 0xed, 0x7c, 0x3f, 0xdd, 0x5e, 0x40 };
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRead()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.ReadUInt8().Should().Be(1);
            reader1.ReadUInt16().Should().Be(2);
            reader1.ReadUInt32().Should().Be(3u);
            reader1.ReadUInt64().Should().Be(4ul);

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.ReadUInt32().Should().Be(3u);
            reader1b.ReadUInt64().Should().Be(4ul);

            var reader2 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data2);
            reader2.ReadString(10).Should().Be("helloworld");

            var reader3 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data3);
            reader3.ReadInt8().Should().Be(-1);
            reader3.ReadInt16().Should().Be(-2);
            reader3.ReadInt32().Should().Be(-3);
            reader3.ReadInt64().Should().Be(-4);

            var reader4 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data4);
            reader4.ReadSingle().Should().BeApproximately(-123.456f, 0.001f);
            reader4.ReadDouble().Should().BeApproximately(123.457, 0.001);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSize()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.ReadBytes(4);
            reader1.Size.Should().Be(15);

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.ReadBytes(4);
            reader1b.Size.Should().Be(12);  // Size returns size from offset

            var reader1c = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3, size: 4);
            reader1c.ReadBytes(1);
            reader1c.Size.Should().Be(4);  // Size returns specified size
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPosition()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.ReadBytes(3);
            reader1.ReadBytes(3);
            reader1.Position.Should().Be(6);

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.ReadBytes(1);
            reader1b.ReadBytes(2);
            reader1b.Position.Should().Be(3);  // Position is relative to offset

            var reader1c = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1c.ReadBytes(1);
            reader1c.ReadBytes(2);
            reader1c.Position.Should().Be(3);  // Position is relative to offset
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSeek()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.ReadBytes(4);
            reader1.Seek(7);
            reader1.Position.Should().Be(7);
            reader1.ReadUInt64().Should().Be(4ul);

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.ReadBytes(3);
            reader1b.Seek(4);
            reader1b.Position.Should().Be(4);
            reader1b.ReadUInt32().Should().Be(4u);

            var reader1c = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1c.ReadBytes(3);
            reader1c.Seek(2);
            reader1c.Position.Should().Be(2);
            reader1c.ReadUInt16().Should().Be(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSkip()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.ReadUInt32();
            reader1.Skip(2);
            reader1.Skip(1);
            reader1.ReadUInt64().Should().Be(4ul);

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.Skip(4);
            reader1b.ReadUInt64().Should().Be(4ul);

            var reader1c = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1c.Skip(2);
            reader1c.ReadUInt16().Should().Be(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRemaining()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.ReadUInt32();
            reader1.Skip(2);
            reader1.Skip(1);
            reader1.Remaining.Should().Be(8);

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.ReadUInt32();
            reader1b.Remaining.Should().Be(8);

            var reader1c = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3, size: 12);
            reader1c.ReadUInt16();
            reader1c.Remaining.Should().Be(10);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPeek()
        {
            var reader1 = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1);
            reader1.Skip(3);
            reader1.Peek(1).Should().Equal(new byte[] { 0x03 });

            var reader1b = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1b.Skip(4);
            reader1b.Peek(1).Should().Equal(new byte[] { 0x04 });

            var reader1c = Andastra.Parsing.Common.RawBinaryReader.FromBytes(_data1, offset: 3);
            reader1c.Peek(1).Should().Equal(new byte[] { 0x03 });
        }
    }

    public class BinaryReaderPortedTests
    {
        public BinaryReaderPortedTests()
        {
            // Register code pages encoding provider for windows-1252 support
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSeekIgnoreAndTellInLittleEndianStream()
        {
            byte[] inputData = Encoding.ASCII.GetBytes("Hello, world!\0");
            var reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(inputData);
            int expectedPos = 7;

            reader.Seek(5);
            reader.Skip(2);
            int actualPos = reader.Position;

            actualPos.Should().Be(expectedPos);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReadFromLittleEndianStream()
        {
            var ms = new MemoryStream();
            ms.WriteByte(0xff);  // byte
            ms.Write(new byte[] { 0x01, 0xff }, 0, 2);  // uint16
            ms.Write(new byte[] { 0x02, 0xff, 0xff, 0xff }, 0, 4);  // uint32
            ms.Write(new byte[] { 0x03, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }, 0, 8);  // uint64
            ms.Write(new byte[] { 0x01, 0xff }, 0, 2);  // int16
            ms.Write(new byte[] { 0x02, 0xff, 0xff, 0xff }, 0, 4);  // int32
            ms.Write(new byte[] { 0x03, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }, 0, 8);  // int64
            ms.Write(new byte[] { 0x00, 0x00, 0x80, 0x3f }, 0, 4);  // float
            ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf0, 0x3f }, 0, 8);  // double
            ms.Write(Encoding.ASCII.GetBytes("Hello, world!"), 0, 13);  // string
            ms.Write(Encoding.ASCII.GetBytes("Hello, world!\0"), 0, 14);  // cstring
            ms.Write(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0, 4);  // bytes

            byte[] inputData = ms.ToArray();
            var reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(inputData);

            byte expectedByte = 255;
            ushort expectedUint16 = 65281;
            uint expectedUint32 = 4294967042;
            ulong expectedUint64 = 18446744073709551363;
            short expectedInt16 = -255;
            int expectedInt32 = -254;
            long expectedInt64 = -253;
            float expectedFloat = 1.0f;
            double expectedDouble = 1.0;
            string expectedStr = "Hello, world!";
            string expectedCstr = "Hello, world!";
            byte[] expectedBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            byte actualByte = reader.ReadUInt8();
            ushort actualUint16 = reader.ReadUInt16();
            uint actualUint32 = reader.ReadUInt32();
            ulong actualUint64 = reader.ReadUInt64();
            short actualInt16 = reader.ReadInt16();
            int actualInt32 = reader.ReadInt32();
            long actualInt64 = reader.ReadInt64();
            float actualFloat = reader.ReadSingle();
            double actualDouble = reader.ReadDouble();
            string actualStr = reader.ReadString(13);
            string actualCstr = reader.ReadTerminatedString();
            byte[] actualBytes = reader.ReadBytes(4);

            actualByte.Should().Be(expectedByte);
            actualUint16.Should().Be(expectedUint16);
            actualUint32.Should().Be(expectedUint32);
            actualUint64.Should().Be(expectedUint64);
            actualInt16.Should().Be(expectedInt16);
            actualInt32.Should().Be(expectedInt32);
            actualInt64.Should().Be(expectedInt64);
            actualFloat.Should().BeApproximately(expectedFloat, 0.00001f);
            actualDouble.Should().BeApproximately(expectedDouble, 0.00001);
            actualStr.Should().Be(expectedStr);
            actualCstr.Should().Be(expectedCstr);
            actualBytes.Should().Equal(expectedBytes);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReadFromBigEndianStream()
        {
            var ms = new MemoryStream();
            ms.Write(new byte[] { 0xff, 0x01 }, 0, 2);  // uint16
            ms.Write(new byte[] { 0xff, 0xff, 0xff, 0x02 }, 0, 4);  // uint32
            ms.Write(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x03 }, 0, 8);  // uint64
            ms.Write(new byte[] { 0xff, 0x01 }, 0, 2);  // int16
            ms.Write(new byte[] { 0xff, 0xff, 0xff, 0x02 }, 0, 4);  // int32
            ms.Write(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x03 }, 0, 8);  // int64
            ms.Write(new byte[] { 0x3f, 0x80, 0x00, 0x00 }, 0, 4);  // float
            ms.Write(new byte[] { 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 8);  // double

            byte[] inputData = ms.ToArray();
            var reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(inputData);

            ushort expectedUint16 = 65281;
            uint expectedUint32 = 4294967042;
            ulong expectedUint64 = 18446744073709551363;
            short expectedInt16 = -255;
            int expectedInt32 = -254;
            long expectedInt64 = -253;
            float expectedFloat = 1.0f;
            double expectedDouble = 1.0;

            ushort actualUint16 = reader.ReadUInt16(bigEndian: true);
            uint actualUint32 = reader.ReadUInt32(bigEndian: true);
            ulong actualUint64 = reader.ReadUInt64(bigEndian: true);
            short actualInt16 = reader.ReadInt16(bigEndian: true);
            int actualInt32 = reader.ReadInt32(bigEndian: true);
            long actualInt64 = reader.ReadInt64(bigEndian: true);
            float actualFloat = reader.ReadSingle(bigEndian: true);
            double actualDouble = reader.ReadDouble(bigEndian: true);

            actualUint16.Should().Be(expectedUint16);
            actualUint32.Should().Be(expectedUint32);
            actualUint64.Should().Be(expectedUint64);
            actualInt16.Should().Be(expectedInt16);
            actualInt32.Should().Be(expectedInt32);
            actualInt64.Should().Be(expectedInt64);
            actualFloat.Should().BeApproximately(expectedFloat, 0.00001f);
            actualDouble.Should().BeApproximately(expectedDouble, 0.00001);
        }

    }
}

