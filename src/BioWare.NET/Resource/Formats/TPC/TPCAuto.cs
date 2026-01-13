using System;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_auto.py
    // swkotor.exe/swkotor2.exe: TPC file format detection and I/O operations
    // Complete implementation with BioWare DDS header heuristic, automatic TXI file detection, and comprehensive error handling
    public static class TPCAuto
    {
        /// <summary>
        /// Returns what format the TPC data is believed to be in.
        /// This function performs a basic check and does not guarantee accuracy of the result or integrity of the data.
        /// Matching PyKotor detect_tpc implementation with BioWare DDS header heuristic.
        /// </summary>
        /// <param name="source">Source of the TPC data (string path, byte[], Stream, or RawBinaryReader).</param>
        /// <param name="offset">Offset into the source data.</param>
        /// <returns>The format of the TPC data (TPC, TGA, DDS, or INVALID).</returns>
        public static ResourceType DetectTpc(object source, int offset = 0)
        {
            // Check file extension first if source is a path
            if (source is string path)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".dds")
                {
                    return ResourceType.DDS;
                }
            }

            // Perform format detection on sample data
            ResourceType fileFormat = ResourceType.INVALID;
            try
            {
                byte[] sample = null;
                RawBinaryReader reader = null;
                bool shouldDisposeReader = false;

                try
                {
                    if (source is string filepath)
                    {
                        reader = RawBinaryReader.FromFile(filepath, offset);
                        shouldDisposeReader = true;
                        sample = reader.ReadBytes(128);
                    }
                    else if (source is byte[] bytes)
                    {
                        int sampleSize = Math.Min(128, bytes.Length - offset);
                        if (sampleSize > 0)
                        {
                            sample = new byte[sampleSize];
                            Array.Copy(bytes, offset, sample, 0, sampleSize);
                        }
                    }
                    else if (source is Stream stream)
                    {
                        reader = RawBinaryReader.FromStream(stream, offset);
                        shouldDisposeReader = true;
                        sample = reader.ReadBytes(128);
                    }
                    else if (source is RawBinaryReader existingReader)
                    {
                        int savedPosition = existingReader.Position;
                        existingReader.Seek(offset);
                        sample = existingReader.ReadBytes(128);
                        existingReader.Seek(savedPosition);
                    }

                    if (sample != null)
                    {
                        fileFormat = DoCheck(sample);
                    }
                }
                finally
                {
                    if (shouldDisposeReader && reader != null)
                    {
                        reader.Dispose();
                    }
                }
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                fileFormat = ResourceType.INVALID;
            }
            catch
            {
                fileFormat = ResourceType.INVALID;
            }

            return fileFormat;
        }

        /// <summary>
        /// Internal format detection logic matching PyKotor do_check implementation.
        /// Checks for DDS magic, BioWare DDS header heuristic, and TPC/TGA differentiation.
        /// </summary>
        private static ResourceType DoCheck(byte[] sample)
        {
            // Check for standard DDS magic "DDS "
            if (sample.Length >= 4 &&
                sample[0] == (byte)'D' &&
                sample[1] == (byte)'D' &&
                sample[2] == (byte)'S' &&
                sample[3] == (byte)' ')
            {
                return ResourceType.DDS;
            }

            // BioWare DDS header heuristic: width/height/bpp/datasize (uint32 LE)
            // swkotor.exe/swkotor2.exe: BioWare uses custom DDS format with different header structure
            if (sample.Length >= 20)
            {
                uint width = BitConverter.ToUInt32(sample, 0);
                uint height = BitConverter.ToUInt32(sample, 4);
                uint bpp = BitConverter.ToUInt32(sample, 8);
                uint dataSize = BitConverter.ToUInt32(sample, 12);

                // Validate BioWare DDS header: reasonable dimensions and bpp values
                if (width > 0 && width < 0x8000 &&
                    height > 0 && height < 0x8000 &&
                    (bpp == 3 || bpp == 4))
                {
                    // Calculate expected data size based on bpp
                    uint expected = (bpp == 3) ? (width * height) / 2 : width * height;
                    if (dataSize == expected)
                    {
                        return ResourceType.DDS;
                    }
                }
            }

            // TPC files have padding bytes (zeros) in header region (bytes 15-99)
            // TGA files typically have non-zero data in this region
            if (sample.Length < 100)
            {
                return ResourceType.TGA;
            }

            // Check for TPC padding pattern: bytes 15-99 should be zeros
            for (int i = 15; i < Math.Min(sample.Length, 100); i++)
            {
                if (sample[i] != 0)
                {
                    return ResourceType.TGA;
                }
            }

            return ResourceType.TPC;
        }

        /// <summary>
        /// Returns a TPC instance from the source.
        /// The file format (TPC, TGA, or DDS) is automatically determined before parsing the data.
        /// Matching PyKotor read_tpc implementation with automatic TXI file detection.
        /// </summary>
        /// <param name="source">The source of the data (string path, byte[], Stream, or RawBinaryReader).</param>
        /// <param name="offset">The byte offset of the file inside the data.</param>
        /// <param name="size">Number of bytes allowed to read from the stream. If null, uses the whole stream.</param>
        /// <param name="txiSource">An optional source to the TXI data to use. If this is a filepath, it *must* exist on disk. If null and source is a filepath, automatically looks for .txi file.</param>
        /// <returns>An TPC instance.</returns>
        public static TPC ReadTpc(object source, int offset = 0, int? size = null, object txiSource = null)
        {
            ResourceType fileFormat = DetectTpc(source, offset);

            TPC loadedTpc;
            if (fileFormat == ResourceType.TPC)
            {
                if (source is string filepath)
                {
                    loadedTpc = new TPCBinaryReader(filepath, offset, size ?? 0).Load();
                }
                else if (source is byte[] data)
                {
                    loadedTpc = new TPCBinaryReader(data, offset, size ?? 0).Load();
                }
                else if (source is Stream stream)
                {
                    loadedTpc = new TPCBinaryReader(stream, offset, size ?? 0).Load();
                }
                else
                {
                    throw new ArgumentException("Unsupported source type for TPC reading");
                }
            }
            else if (fileFormat == ResourceType.TGA)
            {
                if (source is string filepath)
                {
                    loadedTpc = new TPCTGAReader(filepath, offset, size).Load();
                }
                else if (source is byte[] data)
                {
                    loadedTpc = new TPCTGAReader(data, offset, size).Load();
                }
                else if (source is Stream stream)
                {
                    loadedTpc = new TPCTGAReader(stream, offset, size).Load();
                }
                else
                {
                    throw new ArgumentException("Unsupported source type for TGA reading");
                }
            }
            else if (fileFormat == ResourceType.DDS)
            {
                if (source is string filepath)
                {
                    loadedTpc = new TPCDDSReader(filepath, offset, size).Load();
                }
                else if (source is byte[] data)
                {
                    loadedTpc = new TPCDDSReader(data, offset, size).Load();
                }
                else if (source is Stream stream)
                {
                    loadedTpc = new TPCDDSReader(stream, offset, size).Load();
                }
                else
                {
                    throw new ArgumentException("Unsupported source type for DDS reading");
                }
            }
            else
            {
                throw new ArgumentException("Failed to determine the format of the TPC/TGA/DDS file.");
            }

            // Automatic TXI file detection: if txiSource is null and source is a filepath, look for .txi file
            // Matching PyKotor read_tpc TXI handling logic
            if (txiSource == null && source is string sourcePath)
            {
                string txiPath = Path.ChangeExtension(sourcePath, ".txi");
                if (File.Exists(txiPath))
                {
                    txiSource = txiPath;
                }
            }
            else if (txiSource is string txiPathString)
            {
                // Ensure .txi extension
                if (!txiPathString.EndsWith(".txi", StringComparison.OrdinalIgnoreCase))
                {
                    txiPathString = Path.ChangeExtension(txiPathString, ".txi");
                }
                if (File.Exists(txiPathString))
                {
                    txiSource = txiPathString;
                }
                else
                {
                    txiSource = null;
                }
            }

            // Load TXI data if available
            if (txiSource != null)
            {
                if (txiSource is string txiFilePath && File.Exists(txiFilePath))
                {
                    try
                    {
                        string txiContent = File.ReadAllText(txiFilePath, Encoding.ASCII);
                        loadedTpc.Txi = txiContent;
                        loadedTpc.TxiObject = new TXI.TXI(loadedTpc.Txi);
                    }
                    catch
                    {
                        // Ignore TXI loading errors, continue without TXI data
                    }
                }
                else if (txiSource is byte[] txiBytes)
                {
                    try
                    {
                        string txiContent = Encoding.ASCII.GetString(txiBytes);
                        loadedTpc.Txi = txiContent;
                        loadedTpc.TxiObject = new TXI.TXI(loadedTpc.Txi);
                    }
                    catch
                    {
                        // Ignore TXI loading errors, continue without TXI data
                    }
                }
                else if (txiSource is Stream txiStream)
                {
                    try
                    {
                        using (var reader = new StreamReader(txiStream, Encoding.ASCII, leaveOpen: true))
                        {
                            string txiContent = reader.ReadToEnd();
                            loadedTpc.Txi = txiContent;
                            loadedTpc.TxiObject = new TXI.TXI(loadedTpc.Txi);
                        }
                    }
                    catch
                    {
                        // Ignore TXI loading errors, continue without TXI data
                    }
                }
            }

            return loadedTpc;
        }

        /// <summary>
        /// Writes the TPC data to the target location with the specified format (TPC, TGA, DDS, or BMP).
        /// Matching PyKotor write_tpc implementation.
        /// </summary>
        /// <param name="tpc">The TPC file being written.</param>
        /// <param name="target">The location to write the data to (string path, Stream, or byte[] via MemoryStream).</param>
        /// <param name="fileFormat">The file format (TPC, TGA, DDS, or BMP). Defaults to TPC.</param>
        public static void WriteTpc(TPC tpc, object target, ResourceType fileFormat = null)
        {
            if (tpc == null)
            {
                throw new ArgumentNullException(nameof(tpc));
            }

            ResourceType fmt = fileFormat ?? ResourceType.TPC;

            if (fmt == ResourceType.TGA)
            {
                if (target is string filepath)
                {
                    new TPCTGAWriter(tpc, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new TPCTGAWriter(tpc, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for TGA");
                }
            }
            else if (fmt == ResourceType.BMP)
            {
                if (target is string filepath)
                {
                    new TPCBMPWriter(tpc, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new TPCBMPWriter(tpc, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for BMP");
                }
            }
            else if (fmt == ResourceType.DDS)
            {
                if (target is string filepath)
                {
                    new TPCDDSWriter(tpc, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new TPCDDSWriter(tpc, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for DDS");
                }
            }
            else if (fmt == ResourceType.TPC)
            {
                if (target is string filepath)
                {
                    new TPCBinaryWriter(tpc, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new TPCBinaryWriter(tpc, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for TPC");
                }
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use TPC, TGA, DDS or BMP.");
            }
        }

        /// <summary>
        /// Returns the TPC data in the specified format (TPC, TGA, DDS, or BMP) as a byte array.
        /// This is a convenience method that wraps the write_tpc() method.
        /// Matching PyKotor bytes_tpc implementation.
        /// </summary>
        /// <param name="tpc">The target TPC object.</param>
        /// <param name="fileFormat">The file format (TPC, TGA, DDS, or BMP). Defaults to TPC.</param>
        /// <returns>The TPC data as a byte array.</returns>
        public static byte[] BytesTpc(TPC tpc, ResourceType fileFormat = null)
        {
            if (tpc == null)
            {
                throw new ArgumentNullException(nameof(tpc));
            }

            ResourceType fmt = fileFormat ?? ResourceType.TPC;

            // Use GetBytes() for DDS, TGA, and BMP for performance (avoids MemoryStream overhead)
            if (fmt == ResourceType.DDS)
            {
                using (var writer = new TPCDDSWriter(tpc))
                {
                    writer.Write(autoClose: false);
                    return writer.GetBytes();
                }
            }
            if (fmt == ResourceType.TGA)
            {
                using (var writer = new TPCTGAWriter(tpc))
                {
                    writer.Write(autoClose: false);
                    return writer.GetBytes();
                }
            }
            if (fmt == ResourceType.BMP)
            {
                using (var writer = new TPCBMPWriter(tpc))
                {
                    writer.Write(autoClose: false);
                    return writer.GetBytes();
                }
            }

            // Use MemoryStream for TPC format (matching original implementation pattern)
            // TPCBinaryWriter doesn't have GetBytes(), so use MemoryStream approach
            using (var ms = new MemoryStream())
            {
                WriteTpc(tpc, ms, fmt);
                return ms.ToArray();
            }
        }
    }
}

