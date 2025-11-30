using System;
using System.IO;
using FluentAssertions;
using TSLPatcher.Core.Formats.NCS;
using Xunit;
using TSLPatcher.Tests.Common;

namespace TSLPatcher.Tests.Formats
{

/// <summary>
/// Tests for NCS binary I/O operations.
/// 1:1 port of test_ncs.py from tests/resource/formats/test_ncs.py
/// </summary>
public class NCSFormatTests
{
    private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.ncs");
    private const int ExpectedInstructionCount = 1541;

    /// <summary>
    /// Python: test_binary_io
    /// Ensure binary NCS IO produces byte-identical output.
    /// </summary>
    [Fact]
    public void TestBinaryIO()
    {
        if (!File.Exists(BinaryTestFile))
        {
            // Skip if test file doesn't exist
            return;
        }

        // Python: ncs = NCSBinaryReader(BINARY_TEST_FILE).load()
        using var reader = new NCSBinaryReader(BinaryTestFile);
        NCS ncs = reader.Load();
        ValidateIO(ncs);

        // Python: write_ncs(ncs, file_path)
        string tempPath = Path.Combine(Path.GetTempPath(), $"output_{Guid.NewGuid()}.ncs");
        try
        {
            NCSAuto.WriteNcs(ncs, tempPath);
            
            // Python: data = bytes_ncs(ncs)
            byte[] data = NCSAuto.BytesNcs(ncs);
            
            // Python: ncs = read_ncs(data)
            NCS ncs2 = NCSAuto.ReadNcs(data);
            ValidateIO(ncs2);
            
            // Validate byte-identical output
            byte[] originalData = File.ReadAllBytes(BinaryTestFile);
            data.Should().Equal(originalData, "NCS binary I/O should produce byte-identical output");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void ValidateIO(NCS ncs)
    {
        // Python: self.assertEqual(EXPECTED_INSTRUCTION_COUNT, len(ncs.instructions))
        ncs.Instructions.Count.Should().Be(ExpectedInstructionCount);

        // Python: self.assertEqual(BinaryReader.load_file(BINARY_TEST_FILE), bytes_ncs(ncs))
        // This validates byte-identical output - now implemented in TestBinaryIO
    }
}
}



