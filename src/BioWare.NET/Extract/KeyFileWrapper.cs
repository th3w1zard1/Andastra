using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.KEY;
using BioWare.NET.Resource;

namespace BioWare.NET.Extract
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/keyfile.py:13-18
    // Original: @dataclass class BIFResource:
    public class BIFResource
    {
        public string Name { get; set; }
        public ResourceType Type { get; set; }
        public int BifIndex { get; set; }
        public int ResIndex { get; set; }
    }

    // Thin wrapper to mirror PyKotor extract.keyfile.KEYFile (read-only).
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/keyfile.py:43-130
    public class KeyFileWrapper
    {
        private readonly KEY _key;

        public KeyFileWrapper(string path)
        {
            _key = KEYAuto.ReadKey(path);
        }

        public KEY Inner => _key;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/keyfile.py:63-65
        // Original: def get_resources(self) -> list[BIFResource]:
        public List<BIFResource> GetResources()
        {
            var result = new List<BIFResource>();
            foreach (var entry in _key.KeyEntries)
            {
                result.Add(new BIFResource
                {
                    Name = entry.ResRef.ToString(),
                    Type = entry.ResType,
                    BifIndex = entry.BifIndex,
                    ResIndex = entry.ResIndex
                });
            }
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/keyfile.py:59-61
        // Original: def get_bifs(self) -> list[str]:
        public List<string> GetBifs()
        {
            return _key.BifEntries.Select(b => b.Filename).ToList();
        }
    }
}
