using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Extract.Chitin;
using BioWare.NET.Resource;
using ChitinClass = BioWare.NET.Extract.Chitin.Chitin;

namespace BioWare.NET.Extract
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
