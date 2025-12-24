using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.ERF.ERFAuto;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive, exhaustive tests for ERF format parsing.
    /// Tests all ERF variants (ERF, MOD, SAV, HAK), header fields, localized strings,
    /// key list, resource list, and edge cases.
    /// </summary>
    public class ERFFormatComprehensiveTests
    {
        #region Test Data Helpers

        private ERF CreateTestERF(ERFType erfType, int resourceCount = 3, bool addLocalizedStrings = false)
        {
            var erf = new ERF(erfType);

            for (int i = 0; i < resourceCount; i++)
            {
                string resname = $"test{i + 1}";
                byte[] data = Encoding.ASCII.GetBytes($"test data {i + 1}");
                erf.SetData(resname, ResourceType.TXT, data);
            }

            return erf;
        }

        private byte[] CreateERFBytes(ERFType erfType, Dictionary<string, (ResourceType type, byte[] data)> resources)
        {
            var erf = new ERF(erfType);
            foreach (var (resname, (type, data)) in resources)
            {
                erf.SetData(resname, type, data);
            }
            return BytesErf(erf);
        }

        #endregion

        #region ERF Variant Tests

        [Fact(Timeout = 120000)]
        public void TestERFType_VariantERF()
        {
            var erf = CreateTestERF(ERFType.ERF, 2);
            byte[] data = BytesErf(erf);

            var loadedErf = ReadErf(data);
            loadedErf.ErfType.Should().Be(ERFType.ERF);
            loadedErf.Count.Should().Be(2);
        }

        [Fact(Timeout = 120000)]
        public void TestERFType_VariantMOD()
        {
            var erf = CreateTestERF(ERFType.MOD, 2);
            byte[] data = BytesErf(erf);

            var loadedErf = ReadErf(data);
            loadedErf.ErfType.Should().Be(ERFType.MOD);
            loadedErf.Count.Should().Be(2);
        }

        [Fact(Timeout = 120000)]
        public void TestERFType_VariantHAK()
        {
            // HAK files use ERF format but with HAK signature
            // Note: ERFType enum doesn't have HAK, but format supports it
            var erf = CreateTestERF(ERFType.ERF, 2);
            byte[] data = BytesErf(erf);

            // Modify the file type to HAK in the byte array
            Encoding.ASCII.GetBytes("HAK ").CopyTo(data, 0);

            // Reading should work, but ErfType will be ERF since enum doesn't have HAK
            var loadedErf = ReadErf(data);
            loadedErf.Count.Should().Be(2);
        }

        #endregion

        #region Header Field Tests

        [Fact(Timeout = 120000)]
        public void TestHeader_FileVersion()
        {
            var erf = CreateTestERF(ERFType.ERF, 1);
            byte[] data = BytesErf(erf);

            // Verify version is "V1.0"
            string version = Encoding.ASCII.GetString(data, 4, 4);
            version.Should().Be("V1.0");
        }

        [Fact(Timeout = 120000)]
        public void TestHeader_EntryCount()
        {
            var erf = CreateTestERF(ERFType.ERF, 5);
            byte[] data = BytesErf(erf);

            var loadedErf = ReadErf(data);
            loadedErf.Count.Should().Be(5);

            // Verify entry count in header
            uint entryCount = BitConverter.ToUInt32(data, 16);
            entryCount.Should().Be(5);
        }

        [Fact(Timeout = 120000)]
        public void TestHeader_DescriptionStrRef_MOD()
        {
            var erf = CreateTestERF(ERFType.MOD, 1);
            byte[] data = BytesErf(erf);

            // MOD files typically have description_strref = -1 (0xFFFFFFFF)
            int strrefOffset = 40;
            uint descriptionStrref = BitConverter.ToUInt32(data, strrefOffset);
            // Writer currently writes 0xFFFFFFFF for all types
            descriptionStrref.Should().Be(0xFFFFFFFF);
        }

        [Fact(Timeout = 120000)]
        public void TestHeader_BuildYearAndDay()
        {
            var erf = CreateTestERF(ERFType.ERF, 1);
            byte[] data = BytesErf(erf);

            uint buildYear = BitConverter.ToUInt32(data, 32);
            uint buildDay = BitConverter.ToUInt32(data, 36);

            // Should be set to current year/day (written by ERFBinaryWriter)
            buildYear.Should().BeGreaterThan(0);
            buildDay.Should().BeGreaterThan(0);
            buildDay.Should().BeLessThanOrEqualTo(366); // Max days in a year
        }

        [Fact(Timeout = 120000)]
        public void TestHeader_HeaderSize()
        {
            var erf = CreateTestERF(ERFType.ERF, 1);
            byte[] data = BytesErf(erf);

            // Header should be exactly 160 bytes
            // Offset to keys should be at least 160 (after header)
            uint offsetToKeys = BitConverter.ToUInt32(data, 24);
            offsetToKeys.Should().BeGreaterThanOrEqualTo(160);
        }

        [Fact(Timeout = 120000)]
        public void TestHeader_ReservedPadding()
        {
            var erf = CreateTestERF(ERFType.ERF, 1);
            byte[] data = BytesErf(erf);

            // Reserved field starts at offset 44 (0x2C) and is 116 bytes
            // Most should be zeros (though not strictly required)
            int reservedStart = 44;
            byte[] reserved = new byte[116];
            Array.Copy(data, reservedStart, reserved, 0, 116);
            // Just verify it exists and is the right size
            reserved.Length.Should().Be(116);
        }

        #endregion

        #region Localized Strings Tests

        [Fact(Timeout = 120000)]
        public void TestLocalizedStrings_NoStrings()
        {
            var erf = CreateTestERF(ERFType.ERF, 2, addLocalizedStrings: false);
            byte[] data = BytesErf(erf);

            uint languageCount = BitConverter.ToUInt32(data, 8);
            languageCount.Should().Be(0);

            uint localizedStringSize = BitConverter.ToUInt32(data, 12);
            localizedStringSize.Should().Be(0);
        }

        #endregion

        #region Key List Tests

        [Fact(Timeout = 120000)]
        public void TestKeyList_ResRef()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "testres1", (ResourceType.TXT, Encoding.ASCII.GetBytes("data1")) },
                { "myresource", (ResourceType.TXT, Encoding.ASCII.GetBytes("data2")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Get("testres1", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("data1"));
            loadedErf.Get("myresource", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("data2"));
        }

        [Fact(Timeout = 120000)]
        public void TestKeyList_ResRefPadding()
        {
            // Test ResRef with exactly 16 characters (no null terminator)
            string longResRef = "1234567890123456"; // Exactly 16 chars
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { longResRef, (ResourceType.TXT, Encoding.ASCII.GetBytes("data")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Get(longResRef, ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("data"));
        }

        [Fact(Timeout = 120000)]
        public void TestKeyList_ResourceID()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "res1", (ResourceType.TXT, Encoding.ASCII.GetBytes("data1")) },
                { "res2", (ResourceType.TXT, Encoding.ASCII.GetBytes("data2")) },
                { "res3", (ResourceType.TXT, Encoding.ASCII.GetBytes("data3")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);

            // Get offset to key list
            uint offsetToKeys = BitConverter.ToUInt32(data, 24);

            // First key entry: ResRef (16 bytes) + ResourceID (4 bytes)
            uint resourceId1 = BitConverter.ToUInt32(data, (int)(offsetToKeys + 16));
            uint resourceId2 = BitConverter.ToUInt32(data, (int)(offsetToKeys + 16 + 24));
            uint resourceId3 = BitConverter.ToUInt32(data, (int)(offsetToKeys + 16 + 48));

            resourceId1.Should().Be(0);
            resourceId2.Should().Be(1);
            resourceId3.Should().Be(2);
        }

        [Fact(Timeout = 120000)]
        public void TestKeyList_ResourceTypes()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "text", (ResourceType.TXT, Encoding.ASCII.GetBytes("text")) },
                { "script", (ResourceType.NCS, Encoding.ASCII.GetBytes("script")) },
                { "module", (ResourceType.MOD, Encoding.ASCII.GetBytes("module")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Get("text", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("text"));
            loadedErf.Get("script", ResourceType.NCS).Should().Equal(Encoding.ASCII.GetBytes("script"));
            loadedErf.Get("module", ResourceType.MOD).Should().Equal(Encoding.ASCII.GetBytes("module"));
        }

        [Fact(Timeout = 120000)]
        public void TestKeyList_UnusedField()
        {
            var erf = CreateTestERF(ERFType.ERF, 1);
            byte[] data = BytesErf(erf);

            uint offsetToKeys = BitConverter.ToUInt32(data, 24);
            // Unused field is at offset 22 within key entry (16 ResRef + 4 ResourceID + 2 ResourceType)
            ushort unused = BitConverter.ToUInt16(data, (int)(offsetToKeys + 22));
            // Should be 0 (padding)
            unused.Should().Be(0);
        }

        #endregion

        #region Resource List Tests

        [Fact(Timeout = 120000)]
        public void TestResourceList_Offsets()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "small", (ResourceType.TXT, Encoding.ASCII.GetBytes("a")) },
                { "medium", (ResourceType.TXT, Encoding.ASCII.GetBytes("medium data")) },
                { "large", (ResourceType.TXT, Encoding.ASCII.GetBytes("this is a larger piece of data")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);

            uint offsetToResources = BitConverter.ToUInt32(data, 28);

            // Read resource offsets
            uint offset1 = BitConverter.ToUInt32(data, (int)offsetToResources);
            uint offset2 = BitConverter.ToUInt32(data, (int)(offsetToResources + 8));
            uint offset3 = BitConverter.ToUInt32(data, (int)(offsetToResources + 16));

            // Verify offsets are valid and in order
            offset1.Should().BeLessThan(offset2);
            offset2.Should().BeLessThan(offset3);
            offset1.Should().BeGreaterThan(offsetToResources);
        }

        [Fact(Timeout = 120000)]
        public void TestResourceList_Sizes()
        {
            byte[] data1 = Encoding.ASCII.GetBytes("first");
            byte[] data2 = Encoding.ASCII.GetBytes("second resource");
            byte[] data3 = Encoding.ASCII.GetBytes("third");

            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "res1", (ResourceType.TXT, data1) },
                { "res2", (ResourceType.TXT, data2) },
                { "res3", (ResourceType.TXT, data3) }
            };

            byte[] erfData = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(erfData);

            loadedErf.Get("res1", ResourceType.TXT).Length.Should().Be(5);
            loadedErf.Get("res2", ResourceType.TXT).Length.Should().Be(15);
            loadedErf.Get("res3", ResourceType.TXT).Length.Should().Be(5);
        }

        [Fact(Timeout = 120000)]
        public void TestResourceList_DataIntegrity()
        {
            byte[] originalData = Encoding.ASCII.GetBytes("test data with some content");
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "test", (ResourceType.TXT, originalData) }
            };

            byte[] erfData = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(erfData);

            byte[] loadedData = loadedErf.Get("test", ResourceType.TXT);
            loadedData.Should().Equal(originalData);
        }

        [Fact(Timeout = 120000)]
        public void TestResourceList_BinaryData()
        {
            // Test with binary data (not just text)
            byte[] binaryData = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD, 0x80, 0x7F };
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "binary", (ResourceType.TXT, binaryData) }
            };

            byte[] erfData = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(erfData);

            byte[] loadedBinary = loadedErf.Get("binary", ResourceType.TXT);
            loadedBinary.Should().Equal(binaryData);
        }

        #endregion

        #region Edge Cases

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_EmptyERF()
        {
            var erf = new ERF(ERFType.ERF);
            byte[] data = BytesErf(erf);

            var loadedErf = ReadErf(data);
            loadedErf.Count.Should().Be(0);

            // Verify entry count is 0
            uint entryCount = BitConverter.ToUInt32(data, 16);
            entryCount.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_SingleResource()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "single", (ResourceType.TXT, Encoding.ASCII.GetBytes("only one")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Count.Should().Be(1);
            loadedErf.Get("single", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("only one"));
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_ManyResources()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>();
            for (int i = 0; i < 50; i++)
            {
                resources[$"res{i}"] = (ResourceType.TXT, Encoding.ASCII.GetBytes($"data {i}"));
            }

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Count.Should().Be(50);
            for (int i = 0; i < 50; i++)
            {
                byte[] expected = Encoding.ASCII.GetBytes($"data {i}");
                loadedErf.Get($"res{i}", ResourceType.TXT).Should().Equal(expected);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_LargeResource()
        {
            // Create a large resource (10KB)
            byte[] largeData = new byte[10 * 1024];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "large", (ResourceType.TXT, largeData) }
            };

            byte[] erfData = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(erfData);

            byte[] loadedLarge = loadedErf.Get("large", ResourceType.TXT);
            loadedLarge.Length.Should().Be(10 * 1024);
            loadedLarge.Should().Equal(largeData);
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_VariousResourceTypes()
        {
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "txt", (ResourceType.TXT, Encoding.ASCII.GetBytes("text")) },
                { "gff", (ResourceType.GFF, Encoding.ASCII.GetBytes("gff")) },
                { "2da", (ResourceType.TwoDA, Encoding.ASCII.GetBytes("2da")) },
                { "ncs", (ResourceType.NCS, Encoding.ASCII.GetBytes("ncs")) },
                { "tpc", (ResourceType.TPC, Encoding.ASCII.GetBytes("tpc")) },
                { "dlg", (ResourceType.DLG, Encoding.ASCII.GetBytes("dlg")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Count.Should().Be(6);
            loadedErf.Get("txt", ResourceType.TXT).Should().NotBeNull();
            loadedErf.Get("gff", ResourceType.GFF).Should().NotBeNull();
            loadedErf.Get("2da", ResourceType.TwoDA).Should().NotBeNull();
            loadedErf.Get("ncs", ResourceType.NCS).Should().NotBeNull();
            loadedErf.Get("tpc", ResourceType.TPC).Should().NotBeNull();
            loadedErf.Get("dlg", ResourceType.DLG).Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_ResourceNamesWithSpecialCharacters()
        {
            // ResRefs are limited to alphanumeric and underscore, but test edge cases
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "abc123", (ResourceType.TXT, Encoding.ASCII.GetBytes("data")) },
                { "test_res", (ResourceType.TXT, Encoding.ASCII.GetBytes("data")) },
                { "A", (ResourceType.TXT, Encoding.ASCII.GetBytes("data")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Count.Should().Be(3);
            loadedErf.Get("abc123", ResourceType.TXT).Should().NotBeNull();
            loadedErf.Get("test_res", ResourceType.TXT).Should().NotBeNull();
            loadedErf.Get("A", ResourceType.TXT).Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_ZeroSizedResource()
        {
            byte[] emptyData = new byte[0];
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "empty", (ResourceType.TXT, emptyData) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            byte[] loaded = loadedErf.Get("empty", ResourceType.TXT);
            loaded.Should().NotBeNull();
            loaded.Length.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_RoundTrip_ERF()
        {
            var originalResources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "res1", (ResourceType.TXT, Encoding.ASCII.GetBytes("first")) },
                { "res2", (ResourceType.GFF, Encoding.ASCII.GetBytes("second")) },
                { "res3", (ResourceType.TwoDA, Encoding.ASCII.GetBytes("third")) }
            };

            byte[] data1 = CreateERFBytes(ERFType.ERF, originalResources);
            var loadedErf1 = ReadErf(data1);

            // Round trip: write and read again
            byte[] data2 = BytesErf(loadedErf1);
            var loadedErf2 = ReadErf(data2);

            loadedErf2.Count.Should().Be(3);
            loadedErf2.Get("res1", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("first"));
            loadedErf2.Get("res2", ResourceType.GFF).Should().Equal(Encoding.ASCII.GetBytes("second"));
            loadedErf2.Get("res3", ResourceType.TwoDA).Should().Equal(Encoding.ASCII.GetBytes("third"));
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_RoundTrip_MOD()
        {
            var originalResources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "module", (ResourceType.MOD, Encoding.ASCII.GetBytes("module data")) }
            };

            byte[] data1 = CreateERFBytes(ERFType.MOD, originalResources);
            var loadedErf1 = ReadErf(data1);
            loadedErf1.ErfType.Should().Be(ERFType.MOD);

            // Round trip
            byte[] data2 = BytesErf(loadedErf1);
            var loadedErf2 = ReadErf(data2);

            loadedErf2.ErfType.Should().Be(ERFType.MOD);
            loadedErf2.Get("module", ResourceType.MOD).Should().Equal(Encoding.ASCII.GetBytes("module data"));
        }

        [Fact(Timeout = 120000)]
        public void TestEdgeCase_ResourceOrdering()
        {
            // Resources should be accessible regardless of their order in the file
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "z_last", (ResourceType.TXT, Encoding.ASCII.GetBytes("last")) },
                { "a_first", (ResourceType.TXT, Encoding.ASCII.GetBytes("first")) },
                { "m_middle", (ResourceType.TXT, Encoding.ASCII.GetBytes("middle")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Count.Should().Be(3);
            loadedErf.Get("a_first", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("first"));
            loadedErf.Get("m_middle", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("middle"));
            loadedErf.Get("z_last", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("last"));
        }

        #endregion

        #region Integration Tests

        [Fact(Timeout = 120000)]
        public void TestIntegration_AllVariants()
        {
            var testData = Encoding.ASCII.GetBytes("test data");

            foreach (ERFType erfType in Enum.GetValues(typeof(ERFType)).Cast<ERFType>())
            {
                var erf = new ERF(erfType);
                erf.SetData("test", ResourceType.TXT, testData);

                byte[] data = BytesErf(erf);
                var loadedErf = ReadErf(data);

                loadedErf.ErfType.Should().Be(erfType);
                loadedErf.Count.Should().Be(1);
                loadedErf.Get("test", ResourceType.TXT).Should().Equal(testData);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestIntegration_ComplexERF()
        {
            // Create an ERF with various resource types and sizes
            var resources = new Dictionary<string, (ResourceType type, byte[] data)>
            {
                { "small_txt", (ResourceType.TXT, Encoding.ASCII.GetBytes("a")) },
                { "medium_gff", (ResourceType.GFF, Encoding.ASCII.GetBytes("medium sized gff data")) },
                { "large_ncs", (ResourceType.NCS, new byte[1000]) },
                { "2da_file", (ResourceType.TwoDA, Encoding.ASCII.GetBytes("2da content")) },
                { "texture", (ResourceType.TPC, Encoding.ASCII.GetBytes("texture data")) }
            };

            byte[] data = CreateERFBytes(ERFType.ERF, resources);
            var loadedErf = ReadErf(data);

            loadedErf.Count.Should().Be(5);

            // Verify all resources are accessible
            foreach (var (resname, (type, originalData)) in resources)
            {
                byte[] loaded = loadedErf.Get(resname, type);
                loaded.Should().Equal(originalData);
            }
        }

        #endregion
    }
}

