// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:69-80
// Original: class GlobalConfig: ... _global_config: GlobalConfig = GlobalConfig()
using System.IO;

namespace KotorDiff.AppCore
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:69-80
    // Original: class GlobalConfig: ...
    public class GlobalConfig
    {
        public static GlobalConfig Instance { get; } = new GlobalConfig();

        public FileInfo OutputLog { get; set; }
        public bool? LoggingEnabled { get; set; }
        public KotorDiffConfig Config { get; set; }
        public Andastra.Parsing.Mods.ModificationsByType ModificationsByType { get; set; }
    }
}

