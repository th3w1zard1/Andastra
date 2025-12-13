// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:45-66
// Original: class DiffResult: ...
using System.Collections.Generic;
using System.Linq;

namespace KotorDiff.NET.Differ
{
    /// <summary>
    /// Container for diff results between two installations.
    /// 1:1 port of DiffResult from vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:45-66
    /// </summary>
    public class DiffResult
    {
        public List<FileChange> Changes { get; set; }
        public List<string> Errors { get; set; }

        public DiffResult()
        {
            Changes = new List<FileChange>();
            Errors = new List<string>();
        }

        /// <summary>
        /// Add a file change to the results.
        /// </summary>
        public void AddChange(FileChange change)
        {
            Changes.Add(change);
        }

        /// <summary>
        /// Add an error message to the results.
        /// </summary>
        public void AddError(string error)
        {
            Errors.Add(error);
        }

        /// <summary>
        /// Get all changes of a specific type.
        /// </summary>
        public List<FileChange> GetChangesByType(string changeType)
        {
            return Changes.Where(change => change.ChangeType == changeType).ToList();
        }

        /// <summary>
        /// Get all changes for a specific resource type.
        /// </summary>
        public List<FileChange> GetChangesByResourceType(string resourceType)
        {
            return Changes.Where(change => change.ResourceType == resourceType).ToList();
        }
    }
}

