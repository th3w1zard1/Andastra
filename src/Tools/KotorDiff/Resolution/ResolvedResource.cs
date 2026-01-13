// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:65-74
// Original: @dataclass class ResolvedResource: ...
using System.Collections.Generic;
using System.IO;
using BioWare.NET;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace KotorDiff.Resolution
{
    /// <summary>
    /// A resource resolved through the game's priority order.
    /// 1:1 port of ResolvedResource from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:65-74
    /// </summary>
    public class ResolvedResource
    {
        public ResourceIdentifier Identifier { get; set; }
        [CanBeNull] public byte[] Data { get; set; }
        public string SourceLocation { get; set; } // Human-readable description of where it was found
        [CanBeNull] public string LocationType { get; set; } // Type of location (Override, Modules, Chitin, etc.)
        [CanBeNull] public string Filepath { get; set; } // Full path to the file containing this resource
        [CanBeNull] public Dictionary<string, List<string>> AllLocations { get; set; } // All locations where resource was found

        public ResolvedResource()
        {
            AllLocations = new Dictionary<string, List<string>>();
        }
    }
}

