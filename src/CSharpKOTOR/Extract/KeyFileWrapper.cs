using CSharpKOTOR.Formats.KEY;

namespace CSharpKOTOR.Extract
{
    // Thin wrapper to mirror PyKotor extract.keyfile.KEYFile (read-only).
    public class KeyFileWrapper
    {
        private readonly KEY _key;

        public KeyFileWrapper(string path)
        {
            _key = KEYAuto.ReadKey(path);
        }

        public KEY Inner => _key;
    }
}

