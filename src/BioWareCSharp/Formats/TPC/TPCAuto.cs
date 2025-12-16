using System;
using System.IO;
using BioWareCSharp.Common;
using BioWareCSharp.Common.Resources;

namespace BioWareCSharp.Common.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_auto.py
    // Simplified: detect_tpc, read_tpc, write_tpc, bytes_tpc
    public static class TPCAuto
    {
        public static ResourceType DetectTpc(object source, int offset = 0)
        {
            try
            {
                if (source is string path)
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".dds")
                    {
                        return ResourceType.DDS;
                    }
                }
                using (RawBinaryReader reader = CreateReader(source, offset, 128))
                {
                    byte[] sample = reader.ReadBytes(128);
                    if (sample.Length >= 4 && sample[0] == (byte)'D' && sample[1] == (byte)'D' && sample[2] == (byte)'S' && sample[3] == (byte)' ')
                    {
                        return ResourceType.DDS;
                    }
                    if (sample.Length < 100)
                    {
                        return ResourceType.TGA;
                    }
                    for (int i = 15; i < Math.Min(sample.Length, 100); i++)
                    {
                        if (sample[i] != 0)
                        {
                            return ResourceType.TGA;
                        }
                    }
                    return ResourceType.TPC;
                }
            }
            catch
            {
                return ResourceType.INVALID;
            }
        }

        public static TPC ReadTpc(object source, int offset = 0, int? size = null, object txiSource = null)
        {
            ResourceType format = DetectTpc(source, offset);
            TPC loaded;
            if (format == ResourceType.TPC)
            {
                loaded = new TPCBinaryReader(source is string s ? File.ReadAllBytes(s) : source as byte[], offset, size ?? 0).Load();
            }
            else if (format == ResourceType.DDS)
            {
                if (source is string filepath)
                {
                    loaded = new TPCDDSReader(filepath, offset, size).Load();
                }
                else if (source is byte[] data)
                {
                    loaded = new TPCDDSReader(data, offset, size).Load();
                }
                else if (source is Stream stream)
                {
                    loaded = new TPCDDSReader(stream, offset, size).Load();
                }
                else
                {
                    throw new ArgumentException("Unsupported source type for DDS reading");
                }
            }
            else if (format == ResourceType.TGA)
            {
                if (source is string filepath)
                {
                    loaded = new TPCTGAReader(filepath, offset, size).Load();
                }
                else if (source is byte[] data)
                {
                    loaded = new TPCTGAReader(data, offset, size).Load();
                }
                else if (source is Stream stream)
                {
                    loaded = new TPCTGAReader(stream, offset, size).Load();
                }
                else
                {
                    throw new ArgumentException("Unsupported source type for TGA reading");
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported TPC format: {format}");
            }
            if (txiSource is string txiPath && File.Exists(txiPath))
            {
                loaded.Txi = File.ReadAllText(txiPath);
                loaded.TxiObject = new TXI.TXI(loaded.Txi);
            }
            return loaded;
        }

        public static void WriteTpc(TPC tpc, object target, ResourceType fileFormat = null)
        {
            ResourceType fmt = fileFormat ?? ResourceType.TPC;
            if (fmt == ResourceType.TPC)
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
            else if (fmt == ResourceType.TGA)
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
            else
            {
                throw new ArgumentException($"Unsupported format specified: {fmt}");
            }
        }

        public static byte[] BytesTpc(TPC tpc, ResourceType fileFormat = null)
        {
            ResourceType fmt = fileFormat ?? ResourceType.TPC;
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
            using (var ms = new MemoryStream())
            {
                WriteTpc(tpc, ms, fmt);
                return ms.ToArray();
            }
        }

        private static RawBinaryReader CreateReader(object source, int offset, int size)
        {
            if (source is string filepath)
            {
                return RawBinaryReader.FromFile(filepath, offset, size);
            }
            if (source is byte[] bytes)
            {
                return RawBinaryReader.FromBytes(bytes, offset, size);
            }
            if (source is Stream stream)
            {
                return RawBinaryReader.FromStream(stream, offset, size);
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for TPC");
        }
    }
}

