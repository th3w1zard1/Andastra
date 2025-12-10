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
            return _capsule.Resource(resref, restype);
        }

        public Dictionary<ResourceIdentifier, ResourceResult> Batch(List<ResourceIdentifier> queries)
        {
            return _capsule.Batch(queries);
        }
    }
}

