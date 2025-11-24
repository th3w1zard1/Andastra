using System;
using System.IO;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.GFF;
using Xunit;
using TSLPatcher.Tests.Common;

namespace TSLPatcher.Tests.Formats;

/// <summary>
/// Tests for GFF binary I/O.
/// 1:1 port of Python test_gff.py from tests/resource/formats/test_gff.py
/// </summary>
public class GFFFormatTests
{
    private static readonly string TestGffFile = TestFileHelper.GetPath("test.gff");
    private static readonly string CorruptGffFile = TestFileHelper.GetPath("test_corrupted.gff");

    [Fact]
    public void TestBinaryIO()
    {
        // Read GFF file
        GFF gff = new GFFBinaryReader(TestGffFile).Load();
        ValidateIO(gff);

        // Write and re-read to validate round-trip
        byte[] data = new GFFBinaryWriter(gff).Write();
        gff = new GFFBinaryReader(data).Load();
        ValidateIO(gff);
    }

    private static void ValidateIO(GFF gff)
    {
        gff.Root.GetUInt8("uint8").Should().Be(255);
        gff.Root.GetInt8("int8").Should().Be(-127);
        gff.Root.GetUInt16("uint16").Should().Be(0xFFFF);
        gff.Root.GetInt16("int16").Should().Be(-32768);
        gff.Root.GetUInt32("uint32").Should().Be(0xFFFFFFFF);
        gff.Root.GetInt32("int32").Should().Be(-2147483648);
        gff.Root.GetUInt64("uint64").Should().Be(4294967296);

        gff.Root.GetSingle("single").Should().BeApproximately(12.34567f, 0.00001f);
        gff.Root.GetDouble("double").Should().BeApproximately(12.345678901234, 0.00000000001);

        gff.Root.GetValue("string").Should().Be("abcdefghij123456789");
        gff.Root.GetResRef("resref").Should().Be(new ResRef("resref01"));
        gff.Root.GetBinary("binary").Should().Equal(System.Text.Encoding.ASCII.GetBytes("binarydata"));

        gff.Root.GetVector4("orientation").Should().Be(new Vector4(1, 2, 3, 4));
        gff.Root.GetVector3("position").Should().Be(new Vector3(11, 22, 33));

        LocalizedString locstring = gff.Root.GetLocString("locstring");
        locstring.StringRef.Should().Be(-1);
        locstring.Count.Should().Be(2);
        locstring.Get(Language.English, Gender.Male).Should().Be("male_eng");
        locstring.Get(Language.German, Gender.Female).Should().Be("fem_german");

        gff.Root.GetStruct("child_struct").GetUInt8("child_uint8").Should().Be(4);
        gff.Root.GetList("list").At(0)!.StructId.Should().Be(1);
        gff.Root.GetList("list").At(1)!.StructId.Should().Be(2);
    }

    [Fact]
    public void TestReadRaises()
    {
        // Invalid file
        Action act = () => new GFFBinaryReader(CorruptGffFile).Load();
        act.Should().Throw<InvalidDataException>();
    }
}

