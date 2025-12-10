using System.IO;
using CSharpKOTOR.Formats.KEY;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Extract
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

