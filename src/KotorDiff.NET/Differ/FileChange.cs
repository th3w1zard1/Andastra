// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:25-42
// Original: class FileChange: ...
using System.Collections.Generic;
using JetBrains.Annotations;

namespace KotorDiff.NET.Differ
{
    /// <summary>
    /// Represents a change to a file or resource.
    /// 1:1 port of FileChange from vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:25-42
    /// </summary>
    public class FileChange
    {
        public string Path { get; set; }
        public string ChangeType { get; set; }  // 'added', 'removed', 'modified', 'error'
        [CanBeNull] public string ResourceType { get; set; }
        [CanBeNull] public string OldContent { get; set; }
        [CanBeNull] public string NewContent { get; set; }
        [CanBeNull] public List<string> DiffLines { get; set; }

        public FileChange()
        {
            DiffLines = new List<string>();
        }

        public FileChange(string path, string changeType, [CanBeNull] string resourceType = null)
        {
            Path = path;
            ChangeType = changeType;
            ResourceType = resourceType;
            DiffLines = new List<string>();
        }
    }
}

