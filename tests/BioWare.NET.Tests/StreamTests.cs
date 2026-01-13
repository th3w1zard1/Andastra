using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Tests
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
            Assert.That(_reader1.ReadUInt8(), Is.EqualTo(1));
            Assert.That(_reader1.ReadUInt16(), Is.EqualTo(2));
            Assert.That(_reader1.ReadUInt32(), Is.EqualTo(3U));
            Assert.That(_reader1.ReadUInt64(), Is.EqualTo(4UL));

            Assert.That(_reader1b.ReadUInt32(), Is.EqualTo(3U));
            Assert.That(_reader1b.ReadUInt64(), Is.EqualTo(4UL));

            var reader2 = RawBinaryReader.FromBytes(_data2);
            Assert.That(reader2.ReadString(10), Is.EqualTo("helloworld"));
            reader2.Dispose();

            var reader3 = RawBinaryReader.FromBytes(_data3);
            Assert.That(reader3.ReadInt8(), Is.EqualTo(-1));
            Assert.That(reader3.ReadInt16(), Is.EqualTo(-2));
            Assert.That(reader3.ReadInt32(), Is.EqualTo(-3));
            Assert.That(reader3.ReadInt64(), Is.EqualTo(-4));
            reader3.Dispose();

            var reader4 = RawBinaryReader.FromBytes(_data4);
            Assert.That(reader4.ReadSingle(), Is.EqualTo(-123.456f).Within(0.001f));
            Assert.That(reader4.ReadDouble(), Is.EqualTo(123.457).Within(0.000001));
            reader4.Dispose();
        }

        [Test]
        public void TestSize()
        {
            _reader1.ReadBytes(4);
            Assert.That(_reader1.Size, Is.EqualTo(15));

            _reader1b.ReadBytes(4);
            Assert.That(_reader1b.Size, Is.EqualTo(12));

            _reader1c.ReadBytes(1);
            Assert.That(_reader1c.Size, Is.EqualTo(4));
        }

        [Test]
        public void TestTrueSize()
        {
            _reader1.ReadBytes(4);
            Assert.That(_reader1.TrueSize(), Is.EqualTo(15));

            _reader1b.ReadBytes(4);
            Assert.That(_reader1b.TrueSize(), Is.EqualTo(15));

            _reader1c.ReadBytes(4);
            Assert.That(_reader1c.TrueSize(), Is.EqualTo(15));
        }

        [Test]
        public void TestPosition()
        {
            _reader1.ReadBytes(3);
            _reader1.ReadBytes(3);
            Assert.That(_reader1.Position, Is.EqualTo(6));

            _reader1b.ReadBytes(1);
            _reader1b.ReadBytes(2);
            Assert.That(_reader1b.Position, Is.EqualTo(3));

            _reader1c.ReadBytes(1);
            _reader1c.ReadBytes(2);
            Assert.That(_reader1c.Position, Is.EqualTo(3));
        }

        [Test]
        public void TestSeek()
        {
            _reader1.ReadBytes(4);
            _reader1.Seek(7);
            Assert.That(_reader1.Position, Is.EqualTo(7));
            Assert.That(_reader1.ReadUInt64(), Is.EqualTo(4UL));

            _reader1b.ReadBytes(3);
            _reader1b.Seek(4);
            Assert.That(_reader1b.Position, Is.EqualTo(4));
            Assert.That(_reader1b.ReadUInt32(), Is.EqualTo(4U));

            _reader1c.ReadBytes(3);
            _reader1c.Seek(2);
            Assert.That(_reader1c.Position, Is.EqualTo(2));
            Assert.That(_reader1c.ReadUInt16(), Is.EqualTo(0));
        }

        [Test]
        public void TestSkip()
        {
            _reader1.ReadUInt32();
            _reader1.Skip(2);
            _reader1.Skip(1);
            Assert.That(_reader1.ReadUInt64(), Is.EqualTo(4UL));

            _reader1b.Skip(4);
            Assert.That(_reader1b.ReadUInt64(), Is.EqualTo(4UL));

            _reader1c.Skip(2);
            Assert.That(_reader1c.ReadUInt16(), Is.EqualTo(0));
        }

        [Test]
        public void TestRemaining()
        {
            _reader1.ReadUInt32();
            _reader1.Skip(2);
            _reader1.Skip(1);
            Assert.That(_reader1.Remaining, Is.EqualTo(8));

            _reader1b.ReadUInt32();
            Assert.That(_reader1b.Remaining, Is.EqualTo(8));

            _reader1c.ReadUInt16();
            Assert.That(_reader1c.Remaining, Is.EqualTo(2));
        }

        [Test]
        public void TestPeek()
        {
            _reader1.Skip(3);
            byte[] peeked = _reader1.Peek(1);
            Assert.That(peeked, Is.EqualTo(new byte[] { 0x03 }));

            _reader1b.Skip(4);
            byte[] peeked2 = _reader1b.Peek(1);
            Assert.That(peeked2, Is.EqualTo(new byte[] { 0x04 }));

            byte[] peeked3 = _reader1c.Peek(1);
            Assert.That(peeked3, Is.EqualTo(new byte[] { 0x03 }));
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
                Assert.That(actualPos, Is.EqualTo(expectedPos));
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
                Assert.That(reader.ReadUInt8(), Is.EqualTo(255));
                Assert.That(reader.ReadUInt16(), Is.EqualTo(65281));
                Assert.That(reader.ReadUInt32(), Is.EqualTo(4294967042U));
                Assert.That(reader.ReadUInt64(), Is.EqualTo(18446744073709551363UL));
                Assert.That(reader.ReadInt16(), Is.EqualTo(-255));
                Assert.That(reader.ReadInt32(), Is.EqualTo(-254));
                Assert.That(reader.ReadInt64(), Is.EqualTo(-253));
                Assert.That(reader.ReadSingle(), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(reader.ReadDouble(), Is.EqualTo(1.0).Within(0.0000001));
                Assert.That(reader.ReadString(13), Is.EqualTo("Hello, world!"));
                // ReadTerminatedString removes the first character and includes the terminator
                string terminated = reader.ReadTerminatedString('\0');
                Assert.That(terminated, Is.EqualTo("ello, world!" + "\0"));
                byte[] bytes = reader.ReadBytes(4);
                Assert.That(bytes, Is.EqualTo(new byte[] { 0x01, 0x02, 0x03, 0x04 }));
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
                Assert.That(reader.ReadUInt16(true), Is.EqualTo(65281));
                Assert.That(reader.ReadUInt32(true), Is.EqualTo(4294967042U));
                Assert.That(reader.ReadUInt64(true), Is.EqualTo(18446744073709551363UL));
                Assert.That(reader.ReadInt16(true), Is.EqualTo(-255));
                Assert.That(reader.ReadInt32(true), Is.EqualTo(-254));
                Assert.That(reader.ReadInt64(true), Is.EqualTo(-253));
                Assert.That(reader.ReadSingle(true), Is.EqualTo(1.0f).Within(0.00001f));
                Assert.That(reader.ReadDouble(true), Is.EqualTo(1.0).Within(0.0000001));
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

                    Assert.That(originalLocString.StringRef, Is.EqualTo(readLocString.StringRef));
                    Assert.That(readLocString.Get(Language.English, Gender.Male), Is.EqualTo("Hello World"));
                    Assert.That(readLocString.Get(Language.French, Gender.Female), Is.EqualTo("Bonjour le monde"));
                }
            }
        }
    }
}
