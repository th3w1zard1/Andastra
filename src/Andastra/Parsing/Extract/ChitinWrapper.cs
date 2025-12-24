using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract.Chitin;
using Andastra.Parsing.Resource;
using ChitinClass = Andastra.Parsing.Extract.Chitin.Chitin;

namespace Andastra.Parsing.Extract
{
    // Thin wrapper matching PyKotor extract.chitin.Chitin semantics (read-only).
    public class ChitinWrapper
    {
        private readonly ChitinClass _chitin;

        public ChitinWrapper(string keyPath, string basePath = null)
        {
            _chitin = new ChitinClass(keyPath, basePath);
        }

        public List<FileResource> Resources => _chitin.ToList();
    }
}
