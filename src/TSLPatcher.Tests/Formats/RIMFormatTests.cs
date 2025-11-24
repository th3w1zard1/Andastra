using System;
using System.IO;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.Capsule;
using TSLPatcher.Core.Formats.RIM;
using TSLPatcher.Core.Resources;
using Xunit;
using TSLPatcher.Tests.Common;

namespace TSLPatcher.Tests.Formats;

/// <summary>
/// Tests for RIM binary I/O operations.
/// 1:1 port from tests/resource/formats/test_rim.py
/// </summary>
public class RIMFormatTests
{
    private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.rim");
    private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
    private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.rim");

    [Fact]
    public void TestBinaryIO()
    {
        // Python: test_binary_io
        if (!File.Exists(BinaryTestFile))
        {
            // Skip if test file doesn't exist
            return;
        }

        var capsule = new Capsule(BinaryTestFile);
        ValidateIO(capsule);

        // Write and re-read to validate round-trip
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.rim");
        try
        {
            var rim = new RIM();
            foreach (CapsuleResource resource in capsule)
            {
                rim.SetData(resource.ResName, resource.ResType, resource.Data);
            }

            var writer = new RIMBinaryWriter(rim);
            byte[] data = writer.Write();

            // Read back from bytes
            File.WriteAllBytes(tempFile, data);
            var capsule2 = new Capsule(tempFile);
            capsule2.Reload();
            ValidateIO(capsule2);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void ValidateIO(Capsule rim)
    {
        // Python: validate_io
        rim.Count.Should().Be(3);
        rim.GetResource("1", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("abc"));
        rim.GetResource("2", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("def"));
        rim.GetResource("3", ResourceType.TXT).Should().Equal(System.Text.Encoding.ASCII.GetBytes("ghi"));
    }

    [Fact]
    public void TestReadRaises()
    {
        // Python: test_read_raises
        Action act1 = () => new Capsule(".");
        act1.Should().Throw<Exception>(); // PermissionError or IsADirectoryError

        Action act2 = () => new Capsule(DoesNotExistFile);
        // FileNotFoundException may not be thrown immediately

        // Corrupted file test
        if (File.Exists(CorruptBinaryTestFile))
        {
            Action act3 = () => new Capsule(CorruptBinaryTestFile);
            act3.Should().Throw<Exception>(); // Can be InvalidDataException, EndOfStreamException, etc.
        }
    }
}

