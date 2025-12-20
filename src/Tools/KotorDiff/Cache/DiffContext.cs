// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:997-1028
// Original: @dataclass class DiffContext: ...
using System.IO;

namespace KotorDiff.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:997-1028
    // Original: @dataclass class DiffContext: ...
    public class DiffContext
    {
        public string File1Rel { get; set; }
        public string File2Rel { get; set; }
        public string Ext { get; set; }
        public string ResRef { get; set; }

        // Resolution order location types (for resolution-aware diffing)
        public string File1LocationType { get; set; } // Location type in vanilla/older install (Override, Modules (.mod), etc.)
        public string File2LocationType { get; set; } // Location type in modded/newer install
        public string File1Filepath { get; set; } // Full filepath in base installation (for StrRef reference finding)
        public string File2Filepath { get; set; } // Full filepath in target installation (for module name extraction)
        // Note: Installation objects can be accessed via filepaths if needed
        // public Installation File2Installation { get; set; } // Target installation object

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1014-1027
        // Original: @property def where(self) -> str: ...
        public string Where
        {
            get
            {
                if (!string.IsNullOrEmpty(ResRef))
                {
                    // For resources inside containers (capsules/BIFs)
                    return $"{File2Rel}/{ResRef}.{Ext}";
                }
                // For loose files, just return the full path from modded/target
                return File2Rel ?? "";
            }
        }

        public DiffContext(string file1Rel, string file2Rel, string ext, string resRef = null)
        {
            File1Rel = file1Rel;
            File2Rel = file2Rel;
            Ext = ext;
            ResRef = resRef;
        }
    }
}

