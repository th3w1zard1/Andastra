// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/HashUtil.java
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/HashUtil.java:21-67
    // Original: public class HashUtil
    /// <summary>
    /// Utility class for calculating cryptographic hashes of files.
    /// The primary consumer is NwnnsscompConfig, which fingerprints
    /// external nwnnsscomp.exe binaries so we can choose the correct argument
    /// schema for that compiler version.
    /// </summary>
    public static class HashUtil
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/HashUtil.java:29-44
        // Original: public static String calculateSHA256(File file) throws IOException
        /// <summary>
        /// Calculates the SHA256 hash of a file.
        /// </summary>
        /// <param name="file">The file to hash</param>
        /// <returns>The SHA256 hash as an uppercase hexadecimal string</returns>
        /// <exception cref="IOException">If the file cannot be read</exception>
        public static string CalculateSHA256(File file)
        {
            try
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    using (FileStream fs = new FileStream(file.FullName(), FileMode.Open, FileAccess.Read))
                    {
                        byte[] hashBytes = sha256.ComputeHash(fs);
                        return BytesToHex(hashBytes).ToUpperInvariant();
                    }
                }
            }
            catch (Exception e)
            {
                throw new IOException("SHA-256 calculation failed", e);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/HashUtil.java:56-66
        // Original: private static String bytesToHex(byte[] bytes)
        /// <summary>
        /// Converts a byte array to a hexadecimal string without delimiters.
        /// </summary>
        /// <param name="bytes">The byte array to convert</param>
        /// <returns>The hexadecimal string representation</returns>
        private static string BytesToHex(byte[] bytes)
        {
            StringBuilder hexString = new StringBuilder(2 * bytes.Length);
            foreach (byte b in bytes)
            {
                hexString.Append(b.ToString("x2"));
            }
            return hexString.ToString();
        }
    }
}

