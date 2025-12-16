using System.IO;
using BioWareCSharp.Common.Formats.KEY;
using BioWareCSharp.Common.Resources;

namespace BioWareCSharp.Common.Extract
{
    // Thin wrapper to mirror PyKotor extract.keywriter.KEYWriter.
    public static class KeyWriterWrapper
    {
        public static void WriteKey(KEY key, string targetPath)
        {
            KEYAuto.WriteKey(key, targetPath, ResourceType.KEY);
        }

        public static byte[] BytesKey(KEY key)
        {
            using (var ms = new MemoryStream())
            {
                KEYAuto.WriteKey(key, ms, ResourceType.KEY);
                return ms.ToArray();
            }
        }
    }
}

