using System.Collections.Generic;
using System.Linq;
using BioWareCSharp.Common.Formats.Chitin;
using BioWareCSharp.Common.Resources;

namespace BioWareCSharp.Common.Extract
{
    // Thin wrapper matching PyKotor extract.chitin.Chitin semantics (read-only).
    public class ChitinWrapper
    {
        private readonly Chitin _chitin;

        public ChitinWrapper(string keyPath, string basePath = null)
        {
            _chitin = new Chitin(keyPath, basePath);
        }

        public List<FileResource> Resources => _chitin.ToList();
    }
}

