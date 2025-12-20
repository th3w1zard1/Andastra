using System;
using System.IO;
using SharpCompress.Compressors.LZMA;

namespace Andastra.Utility.LZMA
{
    /// <summary>
    /// Minimal helper for raw LZMA1 compression/decompression (no headers) matching PyKotor bzf.py (lzma.FORMAT_RAW, FILTER_LZMA1).
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/bzf.py:130-134
    /// Original: return lzma.decompress(compressed_data, format=lzma.FORMAT_RAW, filters=[{"id": lzma.FILTER_LZMA1}])
    /// 
    /// Implementation uses SharpCompress for raw LZMA1 format decompression matching Python's lzma.FORMAT_RAW with FILTER_LZMA1.
    /// The properties are fixed for BZF files: lc=3, lp=0, pb=2, dict=8MB (0x5D, 0x00, 0x00, 0x80, 0x00).
    /// </summary>
    internal static class LzmaHelper
    {
        // LZMA properties for raw LZMA1 format: lc=3, lp=0, pb=2, dict=8MB
        // These properties match the standard BZF file format used in KotOR games
        private static readonly byte[] LzmaProperties = { 0x5D, 0x00, 0x00, 0x80, 0x00 };

        /// <summary>
        /// Decompresses raw LZMA1 compressed data matching PyKotor's lzma.decompress with FORMAT_RAW and FILTER_LZMA1.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:18-46
        /// </summary>
        /// <param name="compressedData">The compressed LZMA1 data (raw format, no container headers)</param>
        /// <param name="uncompressedSize">The expected uncompressed size of the data</param>
        /// <returns>The decompressed data</returns>
        /// <exception cref="InvalidDataException">Thrown if decompression fails or size mismatch occurs</exception>
        public static byte[] Decompress(byte[] compressedData, int uncompressedSize)
        {
            if (compressedData == null)
            {
                throw new ArgumentNullException(nameof(compressedData));
            }

            if (compressedData.Length == 0)
            {
                throw new ArgumentException("Compressed data cannot be empty", nameof(compressedData));
            }

            if (uncompressedSize <= 0)
            {
                throw new ArgumentException("Uncompressed size must be positive", nameof(uncompressedSize));
            }

            // Handle raw LZMA1 format: try with known properties first (matching Python's FORMAT_RAW, FILTER_LZMA1)
            // Python's lzma.decompress with FORMAT_RAW handles properties automatically, but we use fixed properties
            // for BZF files which match the standard format (lc=3, lp=0, pb=2, dict=8MB)
            try
            {
                return DecompressWithProperties(compressedData, uncompressedSize, LzmaProperties);
            }
            catch (Exception ex)
            {
                // Fallback: try with properties extracted from stream (handles containerized LZMA with padding)
                // This matches PyKotor's fallback behavior in _decompress_bzf_payload
                try
                {
                    return DecompressWithExtractedProperties(compressedData, uncompressedSize);
                }
                catch (Exception fallbackEx)
                {
                    throw new InvalidDataException(
                        $"Failed to decompress LZMA1 data. Primary error: {ex.Message}. Fallback error: {fallbackEx.Message}",
                        ex);
                }
            }
        }

        /// <summary>
        /// Decompresses using known LZMA properties (standard BZF format).
        /// For raw LZMA1, properties are embedded at the start of the stream, so we skip them.
        /// Matching xoreos implementation at src/common/lzma.cpp:66-122
        /// </summary>
        private static byte[] DecompressWithProperties(byte[] compressedData, int uncompressedSize, byte[] properties)
        {
            // For raw LZMA1, properties are embedded at the start (5 bytes for LZMA1)
            // SharpCompress expects properties separately, so we need to check if they're embedded
            // and skip them if they match our known properties
            const int lzma1PropertiesSize = 5;
            byte[] actualProperties;
            byte[] dataToDecompress;
            int skipOffset = 0;

            // Check if properties are embedded at the start and match our known properties
            if (compressedData.Length >= lzma1PropertiesSize)
            {
                actualProperties = new byte[lzma1PropertiesSize];
                Array.Copy(compressedData, 0, actualProperties, 0, lzma1PropertiesSize);

                // If embedded properties match our known properties, skip them
                bool propertiesMatch = true;
                for (int i = 0; i < lzma1PropertiesSize; i++)
                {
                    if (actualProperties[i] != properties[i])
                    {
                        propertiesMatch = false;
                        break;
                    }
                }

                if (propertiesMatch)
                {
                    // Properties are embedded and match, skip them
                    skipOffset = lzma1PropertiesSize;
                    dataToDecompress = new byte[compressedData.Length - skipOffset];
                    Array.Copy(compressedData, skipOffset, dataToDecompress, 0, dataToDecompress.Length);
                }
                else
                {
                    // Properties don't match, try using extracted properties or assume they're not embedded
                    // For BZF files, properties should match, but handle gracefully
                    dataToDecompress = compressedData;
                    actualProperties = properties;
                }
            }
            else
            {
                // Data too short, use as-is (shouldn't happen for valid BZF)
                dataToDecompress = compressedData;
                actualProperties = properties;
            }

            using (MemoryStream inputStream = new MemoryStream(dataToDecompress))
            using (LzmaStream lzmaStream = new LzmaStream(actualProperties, inputStream))
            using (MemoryStream outputStream = new MemoryStream(uncompressedSize))
            {
                lzmaStream.CopyTo(outputStream);
                byte[] result = outputStream.ToArray();

                // Verify decompressed size matches expected size (matching PyKotor's size validation)
                if (result.Length != uncompressedSize)
                {
                    throw new InvalidDataException(
                        $"Decompressed size mismatch: got {result.Length}, expected {uncompressedSize}");
                }

                return result;
            }
        }

        /// <summary>
        /// Attempts to decompress by handling containerized LZMA streams and padding.
        /// This matches PyKotor's fallback logic in _decompress_bzf_payload (io_bif.py:18-46).
        /// </summary>
        private static byte[] DecompressWithExtractedProperties(byte[] compressedData, int uncompressedSize)
        {
            // PyKotor fallback: try containerized format with padding removal
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/io_bif.py:23-40
            byte[] cleanedPayload = compressedData;

            while (true)
            {
                try
                {
                    // Try with containerized LZMA format (LZMADecompressor handles container format)
                    // Note: SharpCompress doesn't have a direct equivalent to Python's LZMADecompressor
                    // So we try with properties and various payload cleaning strategies
                    
                    // Try with known properties and cleaned payload
                    byte[] result = TryDecompressWithCleanedPayload(cleanedPayload, uncompressedSize);
                    if (result != null && result.Length == uncompressedSize)
                    {
                        return result;
                    }

                    // Try stripping null padding from the end (PyKotor compatibility)
                    byte[] stripped = StripNullPadding(cleanedPayload);
                    if (stripped.Length == cleanedPayload.Length)
                    {
                        // No more padding to strip, break and try final fallback
                        break;
                    }

                    cleanedPayload = stripped;
                }
                catch
                {
                    // Continue with more aggressive cleaning
                    byte[] stripped = StripNullPadding(cleanedPayload);
                    if (stripped.Length == cleanedPayload.Length)
                    {
                        break;
                    }
                    cleanedPayload = stripped;
                }
            }

            // Final fallback: try with original data assuming properties are not embedded
            // (SharpCompress might handle embedded properties automatically)
            try
            {
                using (MemoryStream inputStream = new MemoryStream(compressedData))
                using (LzmaStream lzmaStream = new LzmaStream(LzmaProperties, inputStream))
                using (MemoryStream outputStream = new MemoryStream(uncompressedSize))
                {
                    lzmaStream.CopyTo(outputStream);
                    byte[] result = outputStream.ToArray();
                    if (result.Length == uncompressedSize)
                    {
                        return result;
                    }
                }
            }
            catch
            {
                // Rethrow original exception
            }

            // If all attempts fail, throw
            throw new InvalidDataException("Failed to decompress LZMA1 data with all fallback methods");
        }

        /// <summary>
        /// Attempts decompression with a cleaned payload (properties skipped if embedded).
        /// </summary>
        private static byte[] TryDecompressWithCleanedPayload(byte[] payload, int uncompressedSize)
        {
            const int lzma1PropertiesSize = 5;

            // Try with properties skipped (embedded case)
            if (payload.Length > lzma1PropertiesSize)
            {
                byte[] dataWithoutProperties = new byte[payload.Length - lzma1PropertiesSize];
                Array.Copy(payload, lzma1PropertiesSize, dataWithoutProperties, 0, dataWithoutProperties.Length);

                try
                {
                    using (MemoryStream inputStream = new MemoryStream(dataWithoutProperties))
                    using (LzmaStream lzmaStream = new LzmaStream(LzmaProperties, inputStream))
                    using (MemoryStream outputStream = new MemoryStream(uncompressedSize))
                    {
                        lzmaStream.CopyTo(outputStream);
                        return outputStream.ToArray();
                    }
                }
                catch
                {
                    // Properties might not be embedded, try next approach
                }
            }

            // Try with full payload (properties not embedded, or SharpCompress handles them)
            try
            {
                using (MemoryStream inputStream = new MemoryStream(payload))
                using (LzmaStream lzmaStream = new LzmaStream(LzmaProperties, inputStream))
                using (MemoryStream outputStream = new MemoryStream(uncompressedSize))
                {
                    lzmaStream.CopyTo(outputStream);
                    return outputStream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Strips null padding from the end of the payload (PyKotor compatibility).
        /// </summary>
        private static byte[] StripNullPadding(byte[] payload)
        {
            int newLength = payload.Length;
            while (newLength > 0 && payload[newLength - 1] == 0)
            {
                newLength--;
            }

            if (newLength == payload.Length)
            {
                return payload;
            }

            byte[] stripped = new byte[newLength];
            Array.Copy(payload, stripped, newLength);
            return stripped;
        }

        /// <summary>
        /// Compresses data using raw LZMA1 format matching PyKotor's compression behavior.
        /// </summary>
        /// <param name="uncompressedData">The uncompressed data to compress</param>
        /// <returns>The compressed LZMA1 data (raw format, no container headers)</returns>
        /// <exception cref="ArgumentException">Thrown if input data is null or empty</exception>
        public static byte[] Compress(byte[] uncompressedData)
        {
            if (uncompressedData == null)
            {
                throw new ArgumentNullException(nameof(uncompressedData));
            }

            if (uncompressedData.Length == 0)
            {
                throw new ArgumentException("Uncompressed data cannot be empty", nameof(uncompressedData));
            }

            // Note: SharpCompress's LzmaStream constructor signature used for decompression
            // For compression, we need to use LzmaEncoderStream or similar
            // However, SharpCompress may not support raw LZMA1 compression directly
            // For now, this is a placeholder that will need the LZMA SDK for full implementation

            // TODO: Implement LZMA compression using LZMA SDK or SharpCompress encoder
            // This requires additional investigation of SharpCompress compression API
            throw new NotImplementedException(
                "LZMA compression is not yet implemented. Compression requires additional LZMA SDK integration or SharpCompress encoder support.");
        }
    }
}

