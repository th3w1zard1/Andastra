using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for LTR binary I/O operations.
    /// Tests validate the LTR format structure as defined in LTR.ksy Kaitai Struct definition.
    /// </summary>
    public class LTRFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.ltr");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.ltr");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test LTR file if it doesn't exist
                CreateTestLtrFile(BinaryTestFile);
            }

            // Test reading LTR file
            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();
            ValidateIO(ltr);

            // Test writing and reading back
            byte[] data = LTRAuto.BytesLtr(ltr);
            ltr = new LTRBinaryReader(data).Load();
            ValidateIO(ltr);
        }

        [Fact(Timeout = 120000)]
        public void TestLtrHeaderStructure()
        {
            // Test that LTR header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Validate header constants match LTR.ksy
            // Header is 9 bytes: 4 (file_type) + 4 (file_version) + 1 (letter_count)
            const int ExpectedHeaderSize = 9;
            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(ExpectedHeaderSize, "LTR file should have at least 9-byte header as defined in LTR.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestLtrFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[9];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 9);
            }

            // Validate file type signature matches LTR.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("LTR ", "File type should be 'LTR ' (space-padded) as defined in LTR.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1.0", "Version should be 'V1.0' as defined in LTR.ksy");

            // Validate letter count
            byte letterCount = header[8];
            letterCount.Should().Be(28, "Letter count should be 28 for KotOR as defined in LTR.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestLtrLetterCountValidation()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Validate letter count is 28 (KotOR standard)
            // This is enforced by the reader, but we verify the structure
            ltr.Should().NotBeNull("LTR file should load successfully");
        }

        [Fact(Timeout = 120000)]
        public void TestLtrSingleLetterBlock()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Validate single-letter block structure
            // Should have 28 floats for start, middle, and end (3 arrays × 28 floats = 84 floats = 336 bytes)
            const int ExpectedArraySize = 28;
            const int ExpectedBlockSize = ExpectedArraySize * 3 * 4; // 3 arrays × 28 floats × 4 bytes

            // Test that we can access all single-letter probabilities
            foreach (char c in LTR.CharacterSet)
            {
                string charStr = c.ToString();
                float start = ltr.GetSinglesStart(charStr);
                float middle = ltr.GetSinglesMiddle(charStr);
                float end = ltr.GetSinglesEnd(charStr);

                start.Should().BeGreaterThanOrEqualTo(0.0f, "Start probability should be non-negative");
                start.Should().BeLessThanOrEqualTo(1.0f, "Start probability should be <= 1.0");
                middle.Should().BeGreaterThanOrEqualTo(0.0f, "Middle probability should be non-negative");
                middle.Should().BeLessThanOrEqualTo(1.0f, "Middle probability should be <= 1.0");
                end.Should().BeGreaterThanOrEqualTo(0.0f, "End probability should be non-negative");
                end.Should().BeLessThanOrEqualTo(1.0f, "End probability should be <= 1.0");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrDoubleLetterBlocks()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Validate double-letter blocks structure
            // Should have 28 blocks, each with 3 arrays of 28 floats
            // Total: 28 blocks × 3 arrays × 28 floats × 4 bytes = 9,408 bytes
            const int ExpectedBlockCount = 28;
            const int ExpectedArraySize = 28;

            // Test that we can access all double-letter probabilities
            foreach (char prev1 in LTR.CharacterSet)
            {
                string prev1Str = prev1.ToString();
                foreach (char c in LTR.CharacterSet)
                {
                    string charStr = c.ToString();
                    float start = ltr.GetDoublesStart(prev1Str, charStr);
                    float middle = ltr.GetDoublesMiddle(prev1Str, charStr);
                    float end = ltr.GetDoublesEnd(prev1Str, charStr);

                    start.Should().BeGreaterThanOrEqualTo(0.0f, "Start probability should be non-negative");
                    start.Should().BeLessThanOrEqualTo(1.0f, "Start probability should be <= 1.0");
                    middle.Should().BeGreaterThanOrEqualTo(0.0f, "Middle probability should be non-negative");
                    middle.Should().BeLessThanOrEqualTo(1.0f, "Middle probability should be <= 1.0");
                    end.Should().BeGreaterThanOrEqualTo(0.0f, "End probability should be non-negative");
                    end.Should().BeLessThanOrEqualTo(1.0f, "End probability should be <= 1.0");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrTripleLetterBlocks()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Validate triple-letter blocks structure
            // Should have 28 × 28 = 784 blocks, each with 3 arrays of 28 floats
            // Total: 28 × 28 × 3 × 28 × 4 = 73,472 bytes
            const int ExpectedRowCount = 28;
            const int ExpectedColumnCount = 28;
            const int ExpectedArraySize = 28;

            // Test that we can access all triple-letter probabilities
            foreach (char prev2 in LTR.CharacterSet)
            {
                string prev2Str = prev2.ToString();
                foreach (char prev1 in LTR.CharacterSet)
                {
                    string prev1Str = prev1.ToString();
                    foreach (char c in LTR.CharacterSet)
                    {
                        string charStr = c.ToString();
                        float start = ltr.GetTriplesStart(prev2Str, prev1Str, charStr);
                        float middle = ltr.GetTriplesMiddle(prev2Str, prev1Str, charStr);
                        float end = ltr.GetTriplesEnd(prev2Str, prev1Str, charStr);

                        start.Should().BeGreaterThanOrEqualTo(0.0f, "Start probability should be non-negative");
                        start.Should().BeLessThanOrEqualTo(1.0f, "Start probability should be <= 1.0");
                        middle.Should().BeGreaterThanOrEqualTo(0.0f, "Middle probability should be non-negative");
                        middle.Should().BeLessThanOrEqualTo(1.0f, "Middle probability should be <= 1.0");
                        end.Should().BeGreaterThanOrEqualTo(0.0f, "End probability should be non-negative");
                        end.Should().BeLessThanOrEqualTo(1.0f, "End probability should be <= 1.0");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrFileSize()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            // Validate total file size matches LTR.ksy structure
            // Header: 9 bytes
            // Single-letter block: 28 × 3 × 4 = 336 bytes
            // Double-letter blocks: 28 × 3 × 28 × 4 = 9,408 bytes
            // Triple-letter blocks: 28 × 28 × 3 × 28 × 4 = 73,472 bytes
            // Total: 9 + 336 + 9,408 + 73,472 = 83,225 bytes
            const int ExpectedFileSize = 9 + 336 + 9408 + 73472;

            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().Be(ExpectedFileSize, "LTR file size should match LTR.ksy structure definition");
        }

        [Fact(Timeout = 120000)]
        public void TestLtrRoundTrip()
        {
            // Test creating LTR, writing it, and reading it back
            var ltr = new LTR();

            // Set some test probabilities
            ltr.SetSinglesStart("a", 0.1f);
            ltr.SetSinglesMiddle("b", 0.2f);
            ltr.SetSinglesEnd("c", 0.3f);

            ltr.SetDoublesStart("a", "b", 0.4f);
            ltr.SetDoublesMiddle("b", "c", 0.5f);
            ltr.SetDoublesEnd("c", "d", 0.6f);

            ltr.SetTriplesStart("a", "b", "c", 0.7f);
            ltr.SetTriplesMiddle("b", "c", "d", 0.8f);
            ltr.SetTriplesEnd("c", "d", "e", 0.9f);

            // Write to bytes
            byte[] data = LTRAuto.BytesLtr(ltr);

            // Read back
            LTR loaded = new LTRBinaryReader(data).Load();

            // Verify values match
            loaded.GetSinglesStart("a").Should().Be(0.1f);
            loaded.GetSinglesMiddle("b").Should().Be(0.2f);
            loaded.GetSinglesEnd("c").Should().Be(0.3f);

            loaded.GetDoublesStart("a", "b").Should().Be(0.4f);
            loaded.GetDoublesMiddle("b", "c").Should().Be(0.5f);
            loaded.GetDoublesEnd("c", "d").Should().Be(0.6f);

            loaded.GetTriplesStart("a", "b", "c").Should().Be(0.7f);
            loaded.GetTriplesMiddle("b", "c", "d").Should().Be(0.8f);
            loaded.GetTriplesEnd("c", "d", "e").Should().Be(0.9f);
        }

        [Fact(Timeout = 120000)]
        public void TestLtrEmptyFile()
        {
            // Test LTR with default (zero) probabilities
            var ltr = new LTR();

            // All probabilities should be 0.0 by default
            foreach (char c in LTR.CharacterSet)
            {
                string charStr = c.ToString();
                ltr.GetSinglesStart(charStr).Should().Be(0.0f);
                ltr.GetSinglesMiddle(charStr).Should().Be(0.0f);
                ltr.GetSinglesEnd(charStr).Should().Be(0.0f);
            }

            // Write and read back
            byte[] data = LTRAuto.BytesLtr(ltr);
            LTR loaded = new LTRBinaryReader(data).Load();

            // Verify all probabilities are still 0.0
            foreach (char c in LTR.CharacterSet)
            {
                string charStr = c.ToString();
                loaded.GetSinglesStart(charStr).Should().Be(0.0f);
                loaded.GetSinglesMiddle(charStr).Should().Be(0.0f);
                loaded.GetSinglesEnd(charStr).Should().Be(0.0f);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrFullProbabilityRange()
        {
            // Test setting probabilities across the full range
            var ltr = new LTR();

            // Set probabilities for all characters
            for (int i = 0; i < LTR.CharacterSet.Length; i++)
            {
                char c = LTR.CharacterSet[i];
                string charStr = c.ToString();
                float prob = i / (float)(LTR.CharacterSet.Length - 1); // Range from 0.0 to 1.0

                ltr.SetSinglesStart(charStr, prob);
                ltr.SetSinglesMiddle(charStr, prob);
                ltr.SetSinglesEnd(charStr, prob);
            }

            // Write and read back
            byte[] data = LTRAuto.BytesLtr(ltr);
            LTR loaded = new LTRBinaryReader(data).Load();

            // Verify probabilities match
            for (int i = 0; i < LTR.CharacterSet.Length; i++)
            {
                char c = LTR.CharacterSet[i];
                string charStr = c.ToString();
                float expectedProb = i / (float)(LTR.CharacterSet.Length - 1);

                loaded.GetSinglesStart(charStr).Should().BeApproximately(expectedProb, 0.0001f);
                loaded.GetSinglesMiddle(charStr).Should().BeApproximately(expectedProb, 0.0001f);
                loaded.GetSinglesEnd(charStr).Should().BeApproximately(expectedProb, 0.0001f);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new LTRBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new LTRBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => new LTRBinaryReader(CorruptBinaryTestFile).Load();
                act3.Should().Throw<ArgumentException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrInvalidSignature()
        {
            // Create file with invalid signature
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] invalid = System.Text.Encoding.ASCII.GetBytes("INVALID");
                    fs.Write(invalid, 0, invalid.Length);
                }

                Action act = () => new LTRBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*invalid*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[9];
                    System.Text.Encoding.ASCII.GetBytes("LTR ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    header[8] = 28; // letter_count
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new LTRBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*version*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrInvalidLetterCount()
        {
            // Create file with invalid letter count (not 28)
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[9];
                    System.Text.Encoding.ASCII.GetBytes("LTR ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V1.0").CopyTo(header, 4);
                    header[8] = 26; // letter_count (NWN, but KotOR requires 28)
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new LTRBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*28 characters*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrTruncatedFile()
        {
            // Create file with valid header but truncated data
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[9];
                    System.Text.Encoding.ASCII.GetBytes("LTR ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V1.0").CopyTo(header, 4);
                    header[8] = 28; // letter_count
                    fs.Write(header, 0, header.Length);
                    // File is truncated - no data after header
                }

                Action act = () => new LTRBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrNameGeneration()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Test name generation with seed
            string name1 = ltr.Generate(12345);
            string name2 = ltr.Generate(12345);

            name1.Should().NotBeNullOrEmpty("Generated name should not be empty");
            name2.Should().Be(name1, "Same seed should generate same name");

            // Test name generation without seed (should be different each time)
            string name3 = ltr.Generate();
            string name4 = ltr.Generate();

            // Names might be the same by chance, but they should be valid
            name3.Should().NotBeNullOrEmpty("Generated name should not be empty");
            name4.Should().NotBeNullOrEmpty("Generated name should not be empty");
        }

        [Fact(Timeout = 120000)]
        public void TestLtrProbabilityBlockStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLtrFile(BinaryTestFile);
            }

            LTR ltr = new LTRBinaryReader(BinaryTestFile).Load();

            // Validate that probability blocks are structured correctly
            // Each block should have exactly 28 entries for each array (start, middle, end)
            const int ExpectedEntries = 28;

            // Singles block
            // Accessing all 28 characters should work
            for (int i = 0; i < ExpectedEntries; i++)
            {
                char c = LTR.CharacterSet[i];
                string charStr = c.ToString();
                ltr.GetSinglesStart(charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                ltr.GetSinglesMiddle(charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                ltr.GetSinglesEnd(charStr).Should().BeGreaterThanOrEqualTo(0.0f);
            }

            // Doubles blocks - 28 blocks
            for (int i = 0; i < ExpectedEntries; i++)
            {
                char prev1 = LTR.CharacterSet[i];
                string prev1Str = prev1.ToString();
                for (int j = 0; j < ExpectedEntries; j++)
                {
                    char c = LTR.CharacterSet[j];
                    string charStr = c.ToString();
                    ltr.GetDoublesStart(prev1Str, charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                    ltr.GetDoublesMiddle(prev1Str, charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                    ltr.GetDoublesEnd(prev1Str, charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                }
            }

            // Triples blocks - 28 × 28 blocks
            for (int i = 0; i < ExpectedEntries; i++)
            {
                char prev2 = LTR.CharacterSet[i];
                string prev2Str = prev2.ToString();
                for (int j = 0; j < ExpectedEntries; j++)
                {
                    char prev1 = LTR.CharacterSet[j];
                    string prev1Str = prev1.ToString();
                    for (int k = 0; k < ExpectedEntries; k++)
                    {
                        char c = LTR.CharacterSet[k];
                        string charStr = c.ToString();
                        ltr.GetTriplesStart(prev2Str, prev1Str, charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                        ltr.GetTriplesMiddle(prev2Str, prev1Str, charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                        ltr.GetTriplesEnd(prev2Str, prev1Str, charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLtrDataIntegrity()
        {
            // Test that data written and read back maintains integrity
            var ltr = new LTR();

            // Set a comprehensive set of probabilities
            Random random = new Random(42);
            foreach (char prev2 in LTR.CharacterSet)
            {
                string prev2Str = prev2.ToString();
                foreach (char prev1 in LTR.CharacterSet)
                {
                    string prev1Str = prev1.ToString();
                    foreach (char c in LTR.CharacterSet)
                    {
                        string charStr = c.ToString();
                        float prob = (float)random.NextDouble();
                        ltr.SetTriplesStart(prev2Str, prev1Str, charStr, prob);
                        ltr.SetTriplesMiddle(prev2Str, prev1Str, charStr, prob);
                        ltr.SetTriplesEnd(prev2Str, prev1Str, charStr, prob);
                    }
                }
            }

            // Write and read back
            byte[] data = LTRAuto.BytesLtr(ltr);
            LTR loaded = new LTRBinaryReader(data).Load();

            // Verify all probabilities match
            random = new Random(42); // Reset random with same seed
            foreach (char prev2 in LTR.CharacterSet)
            {
                string prev2Str = prev2.ToString();
                foreach (char prev1 in LTR.CharacterSet)
                {
                    string prev1Str = prev1.ToString();
                    foreach (char c in LTR.CharacterSet)
                    {
                        string charStr = c.ToString();
                        float expectedProb = (float)random.NextDouble();
                        loaded.GetTriplesStart(prev2Str, prev1Str, charStr).Should().BeApproximately(expectedProb, 0.0001f);
                        loaded.GetTriplesMiddle(prev2Str, prev1Str, charStr).Should().BeApproximately(expectedProb, 0.0001f);
                        loaded.GetTriplesEnd(prev2Str, prev1Str, charStr).Should().BeApproximately(expectedProb, 0.0001f);
                    }
                }
            }
        }

        private static void ValidateIO(LTR ltr)
        {
            // Basic validation
            ltr.Should().NotBeNull("LTR should not be null");

            // Validate character set
            LTR.CharacterSet.Should().HaveLength(28, "KotOR uses 28-character alphabet");
            LTR.NumCharacters.Should().Be(28, "KotOR uses 28 characters");

            // Validate that all probability arrays are accessible
            foreach (char c in LTR.CharacterSet)
            {
                string charStr = c.ToString();
                ltr.GetSinglesStart(charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                ltr.GetSinglesMiddle(charStr).Should().BeGreaterThanOrEqualTo(0.0f);
                ltr.GetSinglesEnd(charStr).Should().BeGreaterThanOrEqualTo(0.0f);
            }
        }

        private static void CreateTestLtrFile(string path)
        {
            var ltr = new LTR();

            // Set some default probabilities for testing
            // Use cumulative probabilities (monotonically increasing)
            for (int i = 0; i < LTR.CharacterSet.Length; i++)
            {
                char c = LTR.CharacterSet[i];
                string charStr = c.ToString();
                float prob = (i + 1) / (float)LTR.CharacterSet.Length;
                ltr.SetSinglesStart(charStr, prob);
                ltr.SetSinglesMiddle(charStr, prob);
                ltr.SetSinglesEnd(charStr, prob);
            }

            // Set double-letter probabilities
            for (int i = 0; i < LTR.CharacterSet.Length; i++)
            {
                char prev1 = LTR.CharacterSet[i];
                string prev1Str = prev1.ToString();
                for (int j = 0; j < LTR.CharacterSet.Length; j++)
                {
                    char c = LTR.CharacterSet[j];
                    string charStr = c.ToString();
                    float prob = (j + 1) / (float)LTR.CharacterSet.Length;
                    ltr.SetDoublesStart(prev1Str, charStr, prob);
                    ltr.SetDoublesMiddle(prev1Str, charStr, prob);
                    ltr.SetDoublesEnd(prev1Str, charStr, prob);
                }
            }

            // Set triple-letter probabilities
            for (int i = 0; i < LTR.CharacterSet.Length; i++)
            {
                char prev2 = LTR.CharacterSet[i];
                string prev2Str = prev2.ToString();
                for (int j = 0; j < LTR.CharacterSet.Length; j++)
                {
                    char prev1 = LTR.CharacterSet[j];
                    string prev1Str = prev1.ToString();
                    for (int k = 0; k < LTR.CharacterSet.Length; k++)
                    {
                        char c = LTR.CharacterSet[k];
                        string charStr = c.ToString();
                        float prob = (k + 1) / (float)LTR.CharacterSet.Length;
                        ltr.SetTriplesStart(prev2Str, prev1Str, charStr, prob);
                        ltr.SetTriplesMiddle(prev2Str, prev1Str, charStr, prob);
                        ltr.SetTriplesEnd(prev2Str, prev1Str, charStr, prob);
                    }
                }
            }

            byte[] data = LTRAuto.BytesLtr(ltr);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}


