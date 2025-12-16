using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using AuroraEngine.Common.Common;

namespace AuroraEngine.Common.Tests
{
    // Matching PyKotor implementation at Libraries/PyKotor/tests/common/test_stream.py:28
    // Original: class TestBinaryReader(TestCase):
    [TestFixture]
    public class StreamTests
    {
        private byte[] _data1;
        private byte[] _data2;
        private byte[] _data3;
        private byte[] _data4;
        private RawBinaryReader _reader1;
        private RawBinaryReader _reader1b;
        private RawBinaryReader _reader1c;
        private RawBinaryReader _reader2;
        private RawBinaryReader _reader3;
        private RawBinaryReader _reader4;

        [SetUp]
        public void SetUp()
        {
            // Register CodePages encoding provider for Windows encodings (required for .NET Core/5+)
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // Already registered, ignore
            }

            _data1 = new byte[] { 0x01, 0x02, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            _data2 = Encoding.ASCII.GetBytes("helloworld\x00");
            _data3 = new byte[] { 0xFF, 0xFE, 0xFF, 0xFD, 0xFF, 0xFF, 0xFF, 0xFC, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            _data4 = new byte[] { 0x79, 0xE9, 0xF6, 0xC2, 0x68, 0x91, 0xED, 0x7C, 0x3F, 0xDD, 0x5E, 0x40 };

            _reader1 = RawBinaryReader.FromBytes(_data1);
            _reader1b = RawBinaryReader.FromBytes(_data1, 3);
            _reader1c = RawBinaryReader.FromBytes(_data1, 3, 4);
            _reader2 = RawBinaryReader.FromBytes(_data2);
            _reader3 = RawBinaryReader.FromBytes(_data3);
            _reader4 = RawBinaryReader.FromBytes(_data4);
        }

        [TearDown]
        public void TearDown()
        {
            _reader1?.Dispose();
            _reader1b?.Dispose();
            _reader1c?.Dispose();
            _reader2?.Dispose();
            _reader3?.Dispose();
            _reader4?.Dispose();
        }

        [Test]
        public void TestRead()
        {
            Assert.AreEqual(1, _reader1.ReadUInt8());
            Assert.AreEqual(2, _reader1.ReadUInt16());
            Assert.AreEqual(3U, _reader1.ReadUInt32());
            Assert.AreEqual(4UL, _reader1.ReadUInt64());

            Assert.AreEqual(3U, _reader1b.ReadUInt32());
            Assert.AreEqual(4UL, _reader1b.ReadUInt64());

            var reader2 = RawBinaryReader.FromBytes(_data2);
            Assert.AreEqual("helloworld", reader2.ReadString(10));
            reader2.Dispose();

            var reader3 = RawBinaryReader.FromBytes(_data3);
            Assert.AreEqual(-1, reader3.ReadInt8());
            Assert.AreEqual(-2, reader3.ReadInt16());
            Assert.AreEqual(-3, reader3.ReadInt32());
            Assert.AreEqual(-4, reader3.ReadInt64());
            reader3.Dispose();

            var reader4 = RawBinaryReader.FromBytes(_data4);
            Assert.AreEqual(-123.456f, reader4.ReadSingle(), 0.001f);
            Assert.AreEqual(123.457, reader4.ReadDouble(), 0.000001);
            reader4.Dispose();
        }

        [Test]
        public void TestSize()
        {
            _reader1.ReadBytes(4);
            Assert.AreEqual(15, _reader1.Size);

            _reader1b.ReadBytes(4);
            Assert.AreEqual(12, _reader1b.Size);

            _reader1c.ReadBytes(1);
            Assert.AreEqual(4, _reader1c.Size);
        }

        [Test]
        public void TestTrueSize()
        {
            _reader1.ReadBytes(4);
            Assert.AreEqual(15, _reader1.TrueSize());

            _reader1b.ReadBytes(4);
            Assert.AreEqual(15, _reader1b.TrueSize());

            _reader1c.ReadBytes(4);
            Assert.AreEqual(15, _reader1c.TrueSize());
        }

        [Test]
        public void TestPosition()
        {
            _reader1.ReadBytes(3);
            _reader1.ReadBytes(3);
            Assert.AreEqual(6, _reader1.Position);

            _reader1b.ReadBytes(1);
            _reader1b.ReadBytes(2);
            Assert.AreEqual(3, _reader1b.Position);

            _reader1c.ReadBytes(1);
            _reader1c.ReadBytes(2);
            Assert.AreEqual(3, _reader1c.Position);
        }

        [Test]
        public void TestSeek()
        {
            _reader1.ReadBytes(4);
            _reader1.Seek(7);
            Assert.AreEqual(7, _reader1.Position);
            Assert.AreEqual(4UL, _reader1.ReadUInt64());

            _reader1b.ReadBytes(3);
            _reader1b.Seek(4);
            Assert.AreEqual(4, _reader1b.Position);
            Assert.AreEqual(4U, _reader1b.ReadUInt32());

            _reader1c.ReadBytes(3);
            _reader1c.Seek(2);
            Assert.AreEqual(2, _reader1c.Position);
            Assert.AreEqual(0, _reader1c.ReadUInt16());
        }

        [Test]
        public void TestSkip()
        {
            _reader1.ReadUInt32();
            _reader1.Skip(2);
            _reader1.Skip(1);
            Assert.AreEqual(4UL, _reader1.ReadUInt64());

            _reader1b.Skip(4);
            Assert.AreEqual(4UL, _reader1b.ReadUInt64());

            _reader1c.Skip(2);
            Assert.AreEqual(0, _reader1c.ReadUInt16());
        }

        [Test]
        public void TestRemaining()
        {
            _reader1.ReadUInt32();
            _reader1.Skip(2);
            _reader1.Skip(1);
            Assert.AreEqual(8, _reader1.Remaining);

            _reader1b.ReadUInt32();
            Assert.AreEqual(8, _reader1b.Remaining);

            _reader1c.ReadUInt16();
            Assert.AreEqual(2, _reader1c.Remaining);
        }

        [Test]
        public void TestPeek()
        {
            _reader1.Skip(3);
            byte[] peeked = _reader1.Peek(1);
            Assert.AreEqual(new byte[] { 0x03 }, peeked);

            _reader1b.Skip(4);
            byte[] peeked2 = _reader1b.Peek(1);
            Assert.AreEqual(new byte[] { 0x04 }, peeked2);

            byte[] peeked3 = _reader1c.Peek(1);
            Assert.AreEqual(new byte[] { 0x03 }, peeked3);
        }

        [Test]
        public void TestSeekIgnoreAndTellInLittleEndianStream()
        {
            byte[] inputData = Encoding.ASCII.GetBytes("Hello, world!\x00");
            using (var stream = new MemoryStream(inputData))
            using (var reader = RawBinaryReader.FromStream(stream))
            {
                int expectedPos = 7;
                reader.Seek(5);
                reader.Skip(2);
                int actualPos = reader.Position;
                Assert.AreEqual(expectedPos, actualPos);
            }
        }

        [Test]
        public void TestReadFromLittleEndianStream()
        {
            byte[] inputData = new byte[]
            {
                0xFF, // byte
                0x01, 0xFF, // uint16
                0x02, 0xFF, 0xFF, 0xFF, // uint32
                0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // uint64
                0x01, 0xFF, // int16
                0x02, 0xFF, 0xFF, 0xFF, // int32
                0x03, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // int64
                0x00, 0x00, 0x80, 0x3F, // float
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F, // double
                0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x2C, 0x20, 0x77, 0x6F, 0x72, 0x6C, 0x64, 0x21, // string
                0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x2C, 0x20, 0x77, 0x6F, 0x72, 0x6C, 0x64, 0x21, 0x00, // cstring
                0x01, 0x02, 0x03, 0x04 // bytes
            };

            using (var stream = new MemoryStream(inputData))
            using (var reader = RawBinaryReader.FromStream(stream))
            {
                Assert.AreEqual(255, reader.ReadUInt8());
                Assert.AreEqual(65281, reader.ReadUInt16());
                Assert.AreEqual(4294967042U, reader.ReadUInt32());
                Assert.AreEqual(18446744073709551363UL, reader.ReadUInt64());
                Assert.AreEqual(-255, reader.ReadInt16());
                Assert.AreEqual(-254, reader.ReadInt32());
                Assert.AreEqual(-253, reader.ReadInt64());
                Assert.AreEqual(1.0f, reader.ReadSingle(), 0.00001f);
                Assert.AreEqual(1.0, reader.ReadDouble(), 0.0000001);
                Assert.AreEqual("Hello, world!", reader.ReadString(13));
                Assert.AreEqual("Hello, world!", reader.ReadTerminatedString('\0'));
                byte[] bytes = reader.ReadBytes(4);
                Assert.AreEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }, bytes);
            }
        }

        [Test]
        public void TestReadFromBigEndianStream()
        {
            byte[] inputData = new byte[]
            {
                0xFF, 0x01, // uint16
                0xFF, 0xFF, 0xFF, 0x02, // uint32
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, // uint64
                0xFF, 0x01, // int16
                0xFF, 0xFF, 0xFF, 0x02, // int32
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x03, // int64
                0x3F, 0x80, 0x00, 0x00, // float
                0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 // double
            };

            using (var stream = new MemoryStream(inputData))
            using (var reader = RawBinaryReader.FromStream(stream))
            {
                Assert.AreEqual(65281, reader.ReadUInt16(true));
                Assert.AreEqual(4294967042U, reader.ReadUInt32(true));
                Assert.AreEqual(18446744073709551363UL, reader.ReadUInt64(true));
                Assert.AreEqual(-255, reader.ReadInt16(true));
                Assert.AreEqual(-254, reader.ReadInt32(true));
                Assert.AreEqual(-253, reader.ReadInt64(true));
                Assert.AreEqual(1.0f, reader.ReadSingle(true), 0.00001f);
                Assert.AreEqual(1.0, reader.ReadDouble(true), 0.0000001);
            }
        }

        [Test]
        public void TestLocalizedStringReadWrite()
        {
            var originalLocString = new LocalizedString(12345);
            originalLocString.SetData(Language.English, Gender.Male, "Hello World");
            originalLocString.SetData(Language.French, Gender.Female, "Bonjour le monde");

            using (var stream = new MemoryStream())
            using (var writer = RawBinaryWriter.ToStream(stream))
            {
                writer.WriteLocalizedString(originalLocString);

                byte[] data = stream.ToArray();

                using (var reader = RawBinaryReader.FromBytes(data))
                {
                    LocalizedString readLocString = reader.ReadLocalizedString();

                    Assert.AreEqual(originalLocString.StringRef, readLocString.StringRef);
                    Assert.AreEqual("Hello World", readLocString.Get(Language.English, Gender.Male));
                    Assert.AreEqual("Bonjour le monde", readLocString.Get(Language.French, Gender.Female));
                }
            }
        }
    }
}
