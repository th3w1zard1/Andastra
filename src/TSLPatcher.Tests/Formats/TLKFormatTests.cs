using System;
using System.IO;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TLK;
using Xunit;

namespace TSLPatcher.Tests.Formats;

/// <summary>
/// Tests for TLK binary I/O operations.
/// 1:1 port from tests/resource/formats/test_tlk.py
/// </summary>
public class TLKFormatTests
{
    private static readonly string BinaryTestFile = Path.Combine("..", "..", "..", "test_data", "test.tlk");
    private static readonly string CorruptBinaryTestFile = Path.Combine("..", "..", "..", "test_data", "test_corrupted.tlk");

    [Fact]
    public void TestBinaryIO()
    {
        // test_binary_io from Python
        if (!File.Exists(BinaryTestFile))
        {
            // Create test data matching Python BASE_TLK
            var baseTlk = new TLK(Language.English);
            baseTlk.Add("abcdef", "resref01");
            baseTlk.Add("ghijklmnop", "resref02");
            baseTlk.Add("qrstuvwxyz", "");

            var writer = new TLKBinaryWriter(baseTlk);
            byte[] binaryTestData = writer.Write();

            var reader = new TLKBinaryReader(binaryTestData);
            TLK tlk = reader.Load();
            ValidateIO(tlk);

            // Round-trip test
            var writer2 = new TLKBinaryWriter(tlk);
            byte[] data = writer2.Write();

            var newReader = new TLKBinaryReader(data);
            TLK newTlk = newReader.Load();
            ValidateIO(newTlk);
        }
        else
        {
            var reader = new TLKBinaryReader(BinaryTestFile);
            TLK tlk = reader.Load();
            ValidateIO(tlk);

            var writer = new TLKBinaryWriter(tlk);
            byte[] data = writer.Write();

            var newReader = new TLKBinaryReader(data);
            TLK newTlk = newReader.Load();
            ValidateIO(newTlk);
        }
    }

    private static void ValidateIO(TLK tlk)
    {
        // Python: validate_io - matches BASE_TLK from test_tlk.py
        tlk.Language.Should().Be(Language.English);
        tlk.Count.Should().Be(3);

        // Python: assert TLKEntry("abcdef", ResRef("resref01")) == tlk[0]
        tlk[0].Text.Should().Be("abcdef");
        tlk[0].Voiceover.ToString().Should().Be("resref01");

        // Python: assert TLKEntry("ghijklmnop", ResRef("resref02")) == tlk[1]
        tlk[1].Text.Should().Be("ghijklmnop");
        tlk[1].Voiceover.ToString().Should().Be("resref02");

        // Python: assert TLKEntry("qrstuvwxyz", ResRef("")) == tlk[2]
        tlk[2].Text.Should().Be("qrstuvwxyz");
        tlk[2].Voiceover.ToString().Should().Be("");
    }

    [Fact]
    public void TestResize()
    {
        // Python: test_resize
        var baseTlk = new TLK(Language.English);
        baseTlk.Add("abcdef", "resref01");
        baseTlk.Add("ghijklmnop", "resref02");
        baseTlk.Add("qrstuvwxyz", "");

        var writer = new TLKBinaryWriter(baseTlk);
        byte[] binaryTestData = writer.Write();

        var reader = new TLKBinaryReader(binaryTestData);
        TLK tlk = reader.Load();
        tlk.Count.Should().Be(3);
        tlk.Resize(4);
        tlk.Count.Should().Be(4);
    }

    [Fact]
    public void TestReadRaises()
    {
        // test_read_raises from Python
        Action act1 = () => new TLKBinaryReader(".").Load();
        act1.Should().Throw<UnauthorizedAccessException>();

        Action act2 = () => new TLKBinaryReader("./thisfiledoesnotexist").Load();
        act2.Should().Throw<FileNotFoundException>();

        Action act3 = () => new TLKBinaryReader(CorruptBinaryTestFile).Load();
        act3.Should().Throw<InvalidDataException>().WithMessage("Attempted to load an invalid TLK file.");
    }
}

