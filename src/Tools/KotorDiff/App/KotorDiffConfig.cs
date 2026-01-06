// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:43-58
// Original: @dataclass class KotorDiffConfig: ...
using System.Collections.Generic;
using System.IO;

namespace KotorDiff.AppCore
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:43-58
    // Original: @dataclass class KotorDiffConfig: ...
    public class KotorDiffConfig
    {
        public List<object> Paths { get; set; }
        public DirectoryInfo TslPatchDataPath { get; set; }
        public string IniFilename { get; set; } = "changes.ini";
        public FileInfo OutputLogPath { get; set; }
        public string LogLevel { get; set; } = "info";
        public string OutputMode { get; set; } = "full";
        public bool UseColors { get; set; } = true;
        public bool CompareHashes { get; set; } = true;
        public bool UseProfiler { get; set; } = false;
        public List<string> Filters { get; set; }
        public bool LoggingEnabled { get; set; } = true;
        public bool UseIncrementalWriter { get; set; } = false;
    }
}

