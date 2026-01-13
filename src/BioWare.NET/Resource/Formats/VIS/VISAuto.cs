using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.VIS
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_auto.py
    // Original: read_vis, write_vis, bytes_vis functions
    public static class VISAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_auto.py:13-40
        // Original: def read_vis(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> VIS
        public static VIS ReadVis(object source, int offset = 0, int? size = null)
        {
            int sizeValue = size ?? 0;
            if (source is string filepath)
            {
                return new VISAsciiReader(filepath, offset, sizeValue).Load();
            }
            if (source is byte[] bytes)
            {
                return new VISAsciiReader(bytes, offset, sizeValue).Load();
            }
            if (source is Stream stream)
            {
                return new VISAsciiReader(stream, offset, sizeValue).Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for VIS");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_auto.py:43-66
        // Original: def write_vis(vis: VIS, target: TARGET_TYPES, file_format: ResourceType = ResourceType.VIS)
        public static void WriteVis(VIS vis, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.VIS;
            if (format != ResourceType.VIS)
            {
                throw new ArgumentException("Unsupported format specified; use VIS.");
            }

            if (target is string filepath)
            {
                new VISAsciiWriter(vis, filepath).Write();
            }
            else if (target is Stream stream)
            {
                new VISAsciiWriter(vis, stream).Write();
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for VIS");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/vis/vis_auto.py:69-92
        // Original: def bytes_vis(vis: VIS, file_format: ResourceType = ResourceType.VIS) -> bytes
        public static byte[] BytesVis(VIS vis, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.VIS;
            using (var ms = new MemoryStream())
            {
                WriteVis(vis, ms, format);
                return ms.ToArray();
            }
        }
    }
}

