using System.Collections.Generic;
using CSharpKOTOR.Formats.Capsule;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Extract
{
    // Thin wrapper matching PyKotor extract.capsule LazyCapsule semantics.
    public class LazyCapsuleWrapper
    {
        private readonly LazyCapsule _capsule;

        public LazyCapsuleWrapper(string path)
        {
            _capsule = new LazyCapsule(path);
        }

        public byte[] Resource(string resref, ResourceType restype)
        {
            return _capsule.GetResource(resref, restype);
        }

        public Dictionary<ResourceIdentifier, ResourceResult> Batch(List<ResourceIdentifier> queries)
        {
            Dictionary<ResourceIdentifier, ResourceResult> results = new Dictionary<ResourceIdentifier, ResourceResult>();
            foreach (var query in queries)
            {
                byte[] data = _capsule.GetResource(query.ResName, query.ResType);
                if (data == null)
                {
                    results[query] = null;
                    continue;
                }
                var result = new ResourceResult(query.ResName, query.ResType, _capsule.FilePath, data);
                results[query] = result;
            }
            return results;
        }
    }
}

