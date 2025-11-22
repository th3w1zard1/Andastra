using System;
using System.IO;
using FluentAssertions;
using TSLPatcher.Core.Formats.SSF;
using Xunit;

namespace TSLPatcher.Tests.Formats;

/// <summary>
/// Tests for SSF binary I/O operations.
/// 1:1 port of tests/resource/formats/test_ssf.py
/// </summary>
public class SSFFormatTests
{
    private const string BinaryTestFile = "../../../test_data/test.ssf";
    private const string CorruptBinaryTestFile = "../../../test_data/test_corrupted.ssf";
    private const string DoesNotExistFile = "./thisfiledoesnotexist";

    /// <summary>
    /// Python: test_binary_io
    /// Tests reading from actual test.ssf file and round-trip through bytes
    /// </summary>
    [Fact]
    public void TestBinaryIO()
    {
        // Read from actual test file
        var reader = new SSFBinaryReader(BinaryTestFile);
        var ssf = reader.Load();
        ValidateIO(ssf);

        // Write to bytes and read back
        var writer = new SSFBinaryWriter(ssf);
        byte[] data = writer.Write();

        var newReader = new SSFBinaryReader(data);
        var newSsf = newReader.Load();
        ValidateIO(newSsf);
    }

    /// <summary>
    /// Python: test_read_raises
    /// Tests various error conditions when reading SSF files
    /// </summary>
    [Fact]
    public void TestReadRaises()
    {
        // Test directory access
        Action act1 = () => new SSFBinaryReader(".").Load();
        act1.Should().Throw<UnauthorizedAccessException>(); // Or IOException depending on OS

        // Test file not found
        Action act2 = () => new SSFBinaryReader(DoesNotExistFile).Load();
        act2.Should().Throw<FileNotFoundException>();

        // Test corrupted file (invalid version)
        Action act3 = () => new SSFBinaryReader(CorruptBinaryTestFile).Load();
        act3.Should().Throw<InvalidDataException>()
            .WithMessage("*version*not supported*");
    }

    private void ValidateIO(SSF ssf)
    {
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(123075);
        ssf.Get(SSFSound.BATTLE_CRY_2).Should().Be(123074);
        ssf.Get(SSFSound.BATTLE_CRY_3).Should().Be(123073);
        ssf.Get(SSFSound.BATTLE_CRY_4).Should().Be(123072);
        ssf.Get(SSFSound.BATTLE_CRY_5).Should().Be(123071);
        ssf.Get(SSFSound.BATTLE_CRY_6).Should().Be(123070);
        ssf.Get(SSFSound.SELECT_1).Should().Be(123069);
        ssf.Get(SSFSound.SELECT_2).Should().Be(123068);
        ssf.Get(SSFSound.SELECT_3).Should().Be(123067);
        ssf.Get(SSFSound.ATTACK_GRUNT_1).Should().Be(123066);
        ssf.Get(SSFSound.ATTACK_GRUNT_2).Should().Be(123065);
        ssf.Get(SSFSound.ATTACK_GRUNT_3).Should().Be(123064);
        ssf.Get(SSFSound.PAIN_GRUNT_1).Should().Be(123063);
        ssf.Get(SSFSound.PAIN_GRUNT_2).Should().Be(123062);
        ssf.Get(SSFSound.LOW_HEALTH).Should().Be(123061);
        ssf.Get(SSFSound.DEAD).Should().Be(123060);
        ssf.Get(SSFSound.CRITICAL_HIT).Should().Be(123059);
        ssf.Get(SSFSound.TARGET_IMMUNE).Should().Be(123058);
        ssf.Get(SSFSound.LAY_MINE).Should().Be(123057);
        ssf.Get(SSFSound.DISARM_MINE).Should().Be(123056);
        ssf.Get(SSFSound.BEGIN_STEALTH).Should().Be(123055);
        ssf.Get(SSFSound.BEGIN_SEARCH).Should().Be(123054);
        ssf.Get(SSFSound.BEGIN_UNLOCK).Should().Be(123053);
        ssf.Get(SSFSound.UNLOCK_FAILED).Should().Be(123052);
        ssf.Get(SSFSound.UNLOCK_SUCCESS).Should().Be(123051);
        ssf.Get(SSFSound.SEPARATED_FROM_PARTY).Should().Be(123050);
        ssf.Get(SSFSound.REJOINED_PARTY).Should().Be(123049);
        ssf.Get(SSFSound.POISONED).Should().Be(123048);
    }
}

