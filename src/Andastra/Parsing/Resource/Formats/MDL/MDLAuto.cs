using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Andastra.Parsing;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Formats.MDL
{
    // Comprehensive detector and dispatcher for MDL/MDL_ASCII formats
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py
    // Reference: vendor/MDLOps/MDLOpsM.pm:412-435 - Binary vs ASCII format detection
    // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - File Identification section
    public static class MDLAuto
    {
        // Known function pointers for binary MDL validation (swkotor2.exe: GeometryHeader constants)
        private const uint K1_FUNCTION_POINTER0 = 4273776;
        private const uint K2_FUNCTION_POINTER0 = 4285200;
        private const uint K1_ANIM_FUNCTION_POINTER0 = 4273392;
        private const uint K2_ANIM_FUNCTION_POINTER0 = 4284816;
        private const uint K1_FUNCTION_POINTER1 = 4216096;
        private const uint K2_FUNCTION_POINTER1 = 4216320;
        private const uint K1_ANIM_FUNCTION_POINTER1 = 4451552;
        private const uint K2_ANIM_FUNCTION_POINTER1 = 4522928;

        // Maximum reasonable file sizes (1GB for MDL, 100MB for MDX)
        private const uint MAX_MDL_SIZE = 1073741824;
        private const uint MAX_MDX_SIZE = 104857600;

        // Minimum file size for binary MDL (file header + geometry header minimum)
        private const int MIN_BINARY_MDL_SIZE = 92;

        // ASCII MDL keywords (case-insensitive)
        private static readonly string[] ASCII_KEYWORDS = {
            "newmodel", "beginmodelgeom", "endmodelgeom", "node", "endnode",
            "newanim", "doneanim", "setsupermodel", "classification", "ignorefog",
            "setanimationscale", "headlink", "compress_quaternions"
        };

        private static Andastra.Parsing.Common.RawBinaryReader CreateReader(object source, int offset, int? size = null)
        {
            if (source is string path)
            {
                return Andastra.Parsing.Common.RawBinaryReader.FromFile(path, offset, size);
            }
            if (source is byte[] bytes)
            {
                return Andastra.Parsing.Common.RawBinaryReader.FromBytes(bytes, offset, size);
            }
            if (source is Stream stream)
            {
                return Andastra.Parsing.Common.RawBinaryReader.FromStream(stream, offset, size);
            }
            throw new ArgumentException("Unsupported source type for MDL");
        }

        /// <summary>
        /// Comprehensive MDL format detector with validation.
        /// Detects binary MDL vs ASCII MDL format with thorough validation.
        /// Reference: vendor/MDLOps/MDLOpsM.pm:412-435 - modeltype() function
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - File Identification section
        /// </summary>
        /// <param name="source">Source of the MDL data (string path, byte[], or Stream)</param>
        /// <param name="offset">Byte offset into the source data</param>
        /// <returns>ResourceType.MDL for binary format, ResourceType.MDL_ASCII for ASCII format, ResourceType.INVALID on error</returns>
        public static ResourceType DetectMdl(object source, int offset = 0)
        {
            try
            {
                // Read first 4 bytes to check for binary MDL signature
                using (var reader = CreateReader(source, offset, 4))
                {
                    var first4 = reader.ReadBytes(4);
                    if (first4.Length != 4)
                    {
                        return ResourceType.INVALID;
                    }

                    // Binary MDL detection: first 4 bytes must be 0x00000000
                    // Reference: vendor/MDLOps/MDLOpsM.pm:427 - if ($buffer eq "\000\000\000\000")
                    if (first4[0] == 0 && first4[1] == 0 && first4[2] == 0 && first4[3] == 0)
                    {
                        // Validate binary MDL structure
                        if (ValidateBinaryMdl(source, offset))
                        {
                            return ResourceType.MDL;
                        }
                        // If validation fails, might be corrupted binary or invalid data
                        // Fall through to ASCII detection
                    }

                    // ASCII MDL detection: check for text keywords and printable characters
                    return DetectAsciiMdl(source, offset);
                }
            }
            catch (FileNotFoundException)
            {
                return ResourceType.INVALID;
            }
            catch (DirectoryNotFoundException)
            {
                return ResourceType.INVALID;
            }
            catch (UnauthorizedAccessException)
            {
                return ResourceType.INVALID;
            }
            catch (IOException)
            {
                return ResourceType.INVALID;
            }
            catch
            {
                return ResourceType.INVALID;
            }
        }

        /// <summary>
        /// Validates binary MDL file structure.
        /// Checks file header (unused, MDL size, MDX size) and optionally geometry header function pointers.
        /// Reference: swkotor2.exe: MDLBinaryReader.GeometryHeader constants
        /// </summary>
        private static bool ValidateBinaryMdl(object source, int offset)
        {
            try
            {
                // Read file header (12 bytes: unused, mdl_size, mdx_size)
                using (var reader = CreateReader(source, offset, 12))
                {
                    uint unused = reader.ReadUInt32();
                    if (unused != 0)
                    {
                        return false; // First 4 bytes should be 0
                    }

                    uint mdlSize = reader.ReadUInt32();
                    uint mdxSize = reader.ReadUInt32();

                    // Validate sizes are reasonable
                    if (mdlSize == 0 || mdlSize > MAX_MDL_SIZE)
                    {
                        return false;
                    }
                    if (mdxSize > MAX_MDX_SIZE)
                    {
                        return false;
                    }

                    // Check minimum file size
                    if (mdlSize < MIN_BINARY_MDL_SIZE)
                    {
                        return false;
                    }

                    // Optionally validate geometry header function pointers
                    // This provides additional confidence but may fail on custom/patched executables
                    try
                    {
                        using (var geomReader = CreateReader(source, offset + 12, 8))
                        {
                            uint funcPtr0 = geomReader.ReadUInt32();
                            uint funcPtr1 = geomReader.ReadUInt32();

                            // Check if function pointers match known K1/K2 values
                            bool validFuncPtr0 = funcPtr0 == K1_FUNCTION_POINTER0 || funcPtr0 == K2_FUNCTION_POINTER0 ||
                                                funcPtr0 == K1_ANIM_FUNCTION_POINTER0 || funcPtr0 == K2_ANIM_FUNCTION_POINTER0;
                            bool validFuncPtr1 = funcPtr1 == K1_FUNCTION_POINTER1 || funcPtr1 == K2_FUNCTION_POINTER1 ||
                                                funcPtr1 == K1_ANIM_FUNCTION_POINTER1 || funcPtr1 == K2_ANIM_FUNCTION_POINTER1;

                            // Function pointer validation is optional - don't fail if they don't match
                            // (custom executables or patches may have different addresses)
                            // But if they do match, we have high confidence it's a valid binary MDL
                        }
                    }
                    catch
                    {
                        // If we can't read geometry header, still accept based on file header validation
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects ASCII MDL format by checking for known keywords and validating text content.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - ASCII MDL format section
        /// </summary>
        private static ResourceType DetectAsciiMdl(object source, int offset)
        {
            try
            {
                // Read more bytes for ASCII detection (up to 512 bytes to find keywords)
                int sampleSize = 512;
                byte[] sample;
                int actualSize;

                if (source is string path)
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        sample = new byte[Math.Min(sampleSize, (int)(fs.Length - offset))];
                        actualSize = fs.Read(sample, 0, sample.Length);
                    }
                }
                else if (source is byte[] bytes)
                {
                    actualSize = Math.Min(sampleSize, bytes.Length - offset);
                    if (actualSize <= 0)
                    {
                        return ResourceType.INVALID;
                    }
                    sample = new byte[actualSize];
                    Array.Copy(bytes, offset, sample, 0, actualSize);
                }
                else if (source is Stream stream)
                {
                    long originalPos = stream.Position;
                    stream.Seek(offset, SeekOrigin.Begin);
                    sample = new byte[Math.Min(sampleSize, (int)(stream.Length - offset))];
                    actualSize = stream.Read(sample, 0, sample.Length);
                    stream.Position = originalPos;
                }
                else
                {
                    return ResourceType.INVALID;
                }

                if (actualSize < 4)
                {
                    return ResourceType.INVALID;
                }

                // Convert to string for keyword searching
                // Try UTF-8 first, fall back to ASCII if invalid
                string text;
                try
                {
                    text = Encoding.UTF8.GetString(sample, 0, actualSize);
                }
                catch
                {
                    // If UTF-8 fails, try ASCII
                    text = Encoding.ASCII.GetString(sample, 0, actualSize);
                }

                // Check if content is primarily printable ASCII text
                int printableCount = 0;
                int nonPrintableCount = 0;
                for (int i = 0; i < Math.Min(actualSize, 256); i++)
                {
                    byte b = sample[i];
                    // Allow printable ASCII (32-126), tab (9), newline (10), carriage return (13)
                    if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13)
                    {
                        printableCount++;
                    }
                    else if (b != 0) // Null bytes are common in binary, but not in ASCII MDL
                    {
                        nonPrintableCount++;
                    }
                }

                // If we have too many non-printable characters, it's likely not ASCII MDL
                if (nonPrintableCount > printableCount / 4)
                {
                    return ResourceType.INVALID;
                }

                // Normalize text for keyword searching (lowercase, handle whitespace)
                string normalizedText = text.ToLowerInvariant();

                // Check for ASCII MDL keywords
                // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - ASCII MDL format keywords
                bool foundKeyword = false;
                foreach (var keyword in ASCII_KEYWORDS)
                {
                    // Use word boundary matching to avoid false positives
                    if (Regex.IsMatch(normalizedText, @"\b" + Regex.Escape(keyword.ToLowerInvariant()) + @"\b"))
                    {
                        foundKeyword = true;
                        break;
                    }
                }

                // Also check for comment markers (#) which are common in ASCII MDL files
                bool hasComment = normalizedText.Contains("#");

                // If we found keywords or comments, and content is primarily text, it's ASCII MDL
                if (foundKeyword || (hasComment && printableCount > nonPrintableCount * 2))
                {
                    return ResourceType.MDL_ASCII;
                }

                // If content is primarily printable but no keywords found, still likely ASCII
                // (might be a minimal or corrupted ASCII MDL)
                if (printableCount > nonPrintableCount * 3)
                {
                    return ResourceType.MDL_ASCII;
                }

                return ResourceType.INVALID;
            }
            catch
            {
                return ResourceType.INVALID;
            }
        }

        /// <summary>
        /// Reads an MDL file from the source, automatically detecting format or using the specified format.
        /// Comprehensive dispatcher with validation and error handling.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:read_mdl()
        /// </summary>
        /// <param name="source">Source of the MDL data (string path, byte[], or Stream)</param>
        /// <param name="offset">Byte offset into the source data</param>
        /// <param name="size">Number of bytes to read (0 or null for entire source)</param>
        /// <param name="sourceExt">Source of the MDX data (for binary MDL)</param>
        /// <param name="offsetExt">Offset into the MDX source data</param>
        /// <param name="sizeExt">Number of bytes to read from MDX source</param>
        /// <param name="fileFormat">Explicit format to use (null for auto-detection)</param>
        /// <returns>Parsed MDL instance</returns>
        /// <exception cref="ArgumentException">Thrown when format cannot be determined or is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when file path does not exist</exception>
        /// <exception cref="IOException">Thrown when file cannot be read</exception>
        public static MDLData.MDL ReadMdl(object source, int offset = 0, int? size = null, object sourceExt = null, int offsetExt = 0, int sizeExt = 0, ResourceType fileFormat = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source), "MDL source cannot be null");
            }

            // Detect format if not explicitly provided
            ResourceType fmt = fileFormat ?? DetectMdl(source, offset);

            // Validate detected format
            if (fmt == ResourceType.INVALID)
            {
                string sourceDesc = source is string ? $"file '{source}'" : source.GetType().Name;
                throw new ArgumentException($"Failed to determine the format of the MDL file at offset {offset}. The file may be corrupted, empty, or in an unsupported format.", nameof(source));
            }

            // Dispatch to appropriate reader based on format
            try
            {
                if (fmt == ResourceType.MDL)
                {
                    // Binary MDL format
                    return new MDLBinaryReader(source, offset, size ?? 0, sourceExt, offsetExt, sizeExt).Load();
                }
                if (fmt == ResourceType.MDL_ASCII)
                {
                    // ASCII MDL format
                    return CreateAsciiReader(source, offset, size ?? 0).Load();
                }

                // Unsupported format
                throw new ArgumentException($"Unsupported MDL format: {fmt}. Only MDL (binary) and MDL_ASCII formats are supported.", nameof(fileFormat));
            }
            catch (FileNotFoundException ex)
            {
                throw new FileNotFoundException($"MDL file not found: {ex.Message}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new DirectoryNotFoundException($"MDL directory not found: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access denied to MDL file: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"Error reading MDL file: {ex.Message}", ex);
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException as-is (format errors)
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Error parsing MDL file in {fmt} format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Reads an MDL file with fast loading optimized for rendering (binary MDL only).
        /// Fast loading skips animations and controllers for performance.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:read_mdl_fast()
        /// </summary>
        /// <param name="source">Source of the MDL data (string path, byte[], or Stream)</param>
        /// <param name="offset">Byte offset into the source data</param>
        /// <param name="size">Number of bytes to read (0 or null for entire source)</param>
        /// <param name="sourceExt">Source of the MDX data (for binary MDL)</param>
        /// <param name="offsetExt">Offset into the MDX source data</param>
        /// <param name="sizeExt">Number of bytes to read from MDX source</param>
        /// <returns>Parsed MDL instance with minimal data for rendering</returns>
        /// <exception cref="ArgumentException">Thrown when format cannot be determined or is invalid</exception>
        /// <exception cref="FileNotFoundException">Thrown when file path does not exist</exception>
        /// <exception cref="IOException">Thrown when file cannot be read</exception>
        public static MDLData.MDL ReadMdlFast(object source, int offset = 0, int? size = null, object sourceExt = null, int offsetExt = 0, int sizeExt = 0)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source), "MDL source cannot be null");
            }

            // Detect format
            ResourceType fmt = DetectMdl(source, offset);

            // Validate detected format
            if (fmt == ResourceType.INVALID)
            {
                string sourceDesc = source is string ? $"file '{source}'" : source.GetType().Name;
                throw new ArgumentException($"Failed to determine the format of the MDL file at offset {offset}. The file may be corrupted, empty, or in an unsupported format.", nameof(source));
            }

            // Dispatch to appropriate reader
            try
            {
                if (fmt == ResourceType.MDL)
                {
                    // Binary MDL with fast loading (skips animations/controllers)
                    return new MDLBinaryReader(source, offset, size ?? 0, sourceExt, offsetExt, sizeExt, fastLoad: true).Load();
                }
                if (fmt == ResourceType.MDL_ASCII)
                {
                    // ASCII MDL doesn't support fast loading, fall back to regular loading
                    // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:165
                    return CreateAsciiReader(source, offset, size ?? 0).Load();
                }

                // Unsupported format
                throw new ArgumentException($"Unsupported MDL format: {fmt}. Only MDL (binary) and MDL_ASCII formats are supported.");
            }
            catch (FileNotFoundException ex)
            {
                throw new FileNotFoundException($"MDL file not found: {ex.Message}", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new DirectoryNotFoundException($"MDL directory not found: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access denied to MDL file: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"Error reading MDL file: {ex.Message}", ex);
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException as-is (format errors)
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Error parsing MDL file in {fmt} format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes an MDL instance to the target location in the specified format.
        /// Comprehensive dispatcher with validation and error handling.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:write_mdl()
        /// </summary>
        /// <param name="mdl">MDL instance to write</param>
        /// <param name="target">Target location (string path, Stream, or byte[] for binary MDL)</param>
        /// <param name="fileFormat">Format to write (MDL or MDL_ASCII, defaults to MDL)</param>
        /// <param name="targetExt">Target location for MDX data (for binary MDL, defaults to same as target)</param>
        /// <exception cref="ArgumentNullException">Thrown when mdl or target is null</exception>
        /// <exception cref="ArgumentException">Thrown when format is unsupported or target type is invalid</exception>
        /// <exception cref="IOException">Thrown when file cannot be written</exception>
        public static void WriteMdl(MDLData.MDL mdl, object target, ResourceType fileFormat = null, object targetExt = null)
        {
            if (mdl == null)
            {
                throw new ArgumentNullException(nameof(mdl), "MDL instance cannot be null");
            }
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target), "MDL target cannot be null");
            }

            ResourceType fmt = fileFormat ?? ResourceType.MDL;

            // Validate format
            if (fmt != ResourceType.MDL && fmt != ResourceType.MDL_ASCII)
            {
                throw new ArgumentException($"Unsupported format specified: {fmt}. Use ResourceType.MDL or ResourceType.MDL_ASCII.", nameof(fileFormat));
            }

            try
            {
                if (fmt == ResourceType.MDL)
                {
                    // Binary MDL format
                    // targetExt defaults to target if not specified
                    new MDLBinaryWriter(mdl, target, targetExt ?? target).Write();
                }
                else if (fmt == ResourceType.MDL_ASCII)
                {
                    // ASCII MDL format - only supports string paths and Streams
                    if (target is string filepath)
                    {
                        new MDLAsciiWriter(mdl, filepath).Write();
                    }
                    else if (target is Stream stream)
                    {
                        new MDLAsciiWriter(mdl, stream).Write();
                    }
                    else
                    {
                        throw new ArgumentException($"Target type '{target.GetType().Name}' is not supported for MDL_ASCII format. Use string (file path) or Stream.", nameof(target));
                    }
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new DirectoryNotFoundException($"MDL directory not found: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Access denied to MDL file: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"Error writing MDL file: {ex.Message}", ex);
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentException as-is (validation errors)
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error writing MDL file in {fmt} format: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates an MDLAsciiReader for the given source with validation.
        /// </summary>
        /// <param name="source">Source of the ASCII MDL data</param>
        /// <param name="offset">Byte offset into the source data</param>
        /// <param name="size">Number of bytes to read (0 for entire source)</param>
        /// <returns>MDLAsciiReader instance</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null</exception>
        /// <exception cref="ArgumentException">Thrown when source type is unsupported</exception>
        private static MDLAsciiReader CreateAsciiReader(object source, int offset, int size)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source), "MDL ASCII source cannot be null");
            }

            if (source is string path)
            {
                return new MDLAsciiReader(path, offset, size);
            }
            if (source is byte[] bytes)
            {
                return new MDLAsciiReader(bytes, offset, size);
            }
            if (source is Stream stream)
            {
                return new MDLAsciiReader(stream, offset, size);
            }

            throw new ArgumentException($"Unsupported source type '{source.GetType().Name}' for MDL ASCII. Use string (file path), byte[], or Stream.", nameof(source));
        }

        /// <summary>
        /// Returns the MDL data in the specified format (MDL or MDL_ASCII) as a byte array.
        /// Convenience method that writes to a MemoryStream and returns the bytes.
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_auto.py:bytes_mdl()
        /// </summary>
        /// <param name="mdl">MDL instance to convert to bytes</param>
        /// <param name="fileFormat">Format to use (MDL or MDL_ASCII, defaults to MDL)</param>
        /// <returns>Byte array containing the MDL data in the specified format</returns>
        /// <exception cref="ArgumentNullException">Thrown when mdl is null</exception>
        /// <exception cref="ArgumentException">Thrown when format is unsupported</exception>
        /// <exception cref="IOException">Thrown when writing fails</exception>
        public static byte[] BytesMdl(MDLData.MDL mdl, ResourceType fileFormat = null)
        {
            if (mdl == null)
            {
                throw new ArgumentNullException(nameof(mdl), "MDL instance cannot be null");
            }

            ResourceType fmt = fileFormat ?? ResourceType.MDL;

            // Validate format
            if (fmt != ResourceType.MDL && fmt != ResourceType.MDL_ASCII)
            {
                throw new ArgumentException($"Unsupported format specified: {fmt}. Use ResourceType.MDL or ResourceType.MDL_ASCII.", nameof(fileFormat));
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    WriteMdl(mdl, ms, fmt);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Error converting MDL to bytes in {fmt} format: {ex.Message}", ex);
            }
        }
    }
}

