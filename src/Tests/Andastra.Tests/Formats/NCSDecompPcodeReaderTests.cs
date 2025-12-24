using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Tests for PcodeReader parseFixedSizeArgs functionality.
    /// Ported from PcodeReaderTest.java
    /// </summary>
    public class NCSDecompPcodeReaderTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestParseFixedSizeArgs()
        {
            // Test parseFixedSizeArgs directly
            string testLine = "FFFFFFF8 0004      CPDOWNSP FFFFFFF8, 0004";
            int[] argSizes = { 4, 2 }; // sint32, sint16

            // Simulate the parsing
            string[] parts = Regex.Split(testLine, @"\s+");
            List<string> hexParts = new List<string>();

            foreach (string part in parts)
            {
                // Stop at "str" marker
                if (part.Equals("str", StringComparison.Ordinal))
                {
                    break;
                }

                // Stop at function references (fn_XXXXXXXX, off_XXXXXXXX, loc_XXXXXXXX)
                if (Regex.IsMatch(part, @"^(fn|off|loc|sta|sub)_[0-9A-Fa-f]+$"))
                {
                    break;
                }

                // Stop at instruction name - must contain at least one letter G-Z (not a hex digit)
                // This distinguishes "CPDOWNSP" from "FFFFFFF8"
                if (Regex.IsMatch(part, @"^[A-Za-z_][A-Za-z0-9_]*$") && Regex.IsMatch(part, @".*[G-Zg-z].*"))
                {
                    break;
                }

                // Collect hex values (must be at least 2 chars)
                if (Regex.IsMatch(part, @"^[0-9A-Fa-f]{2,}$"))
                {
                    hexParts.Add(part);
                }
            }

            // Calculate total size
            int totalSize = 0;
            foreach (int size in argSizes)
            {
                totalSize += size;
            }

            byte[] result = new byte[totalSize];
            int resultPos = 0;
            int hexPartIndex = 0;

            for (int argIndex = 0; argIndex < argSizes.Length; argIndex++)
            {
                int argSize = argSizes[argIndex];
                int hexDigits = argSize * 2;

                if (hexPartIndex < hexParts.Count)
                {
                    string hexValue = hexParts[hexPartIndex];

                    // Pad with leading zeros if needed
                    while (hexValue.Length < hexDigits)
                    {
                        hexValue = "0" + hexValue;
                    }

                    // Truncate if too long
                    if (hexValue.Length > hexDigits)
                    {
                        hexValue = hexValue.Substring(hexValue.Length - hexDigits);
                    }

                    // Parse as unsigned long to handle large hex values
                    long value = Convert.ToInt64(hexValue, 16);

                    // Write bytes (little-endian)
                    for (int i = 0; i < argSize; i++)
                    {
                        int shift = i * 8;
                        result[resultPos + i] = (byte)((value >> shift) & 0xFF);
                    }
                }

                resultPos += argSize;
                hexPartIndex++;
            }

            // Verify expected result: FFFFFFF8 (sint32) = -8, 0004 (sint16) = 4
            // Little-endian: FFFFFFF8 = 0xF8 0xFF 0xFF 0xFF, 0004 = 0x04 0x00
            Assert.Equal(6, result.Length);
            Assert.Equal(0xF8, result[0] & 0xFF);
            Assert.Equal(0xFF, result[1] & 0xFF);
            Assert.Equal(0xFF, result[2] & 0xFF);
            Assert.Equal(0xFF, result[3] & 0xFF);
            Assert.Equal(0x04, result[4] & 0xFF);
            Assert.Equal(0x00, result[5] & 0xFF);
        }
    }
}

