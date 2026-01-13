using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Resource;
using FileResource = BioWare.NET.Extract.FileResource;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/generic_file_saver.py:29
    // Original: class FileSaveHandler(Generic[T]):
    public class FileSaveHandler
    {
        private List<FileResource> _resources;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/generic_file_saver.py:30-36
        // Original: def __init__(self, resources: Sequence[T], parent: QWidget | None = None):
        public FileSaveHandler(List<FileResource> resources)
        {
            _resources = resources ?? new List<FileResource>();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/generic_file_saver.py:50-74
        // Original: def save_files(self, paths_to_write: dict[T, Path] | None = None) -> dict[T, Path]:
        public Dictionary<FileResource, string> SaveFiles(Dictionary<FileResource, string> pathsToWrite = null)
        {
            var successfullySavedPaths = new Dictionary<FileResource, string>();
            var failedExtractions = new Dictionary<string, Exception>();

            if (pathsToWrite == null)
            {
                pathsToWrite = BuildPathsToWrite();
            }

            foreach (var kvp in pathsToWrite)
            {
                try
                {
                    byte[] data = kvp.Key.GetData();
                    File.WriteAllBytes(kvp.Value, data);
                    successfullySavedPaths[kvp.Key] = kvp.Value;
                }
                catch (Exception ex)
                {
                    failedExtractions[kvp.Value] = ex;
                }
            }

            if (failedExtractions.Count > 0)
            {
                HandleFailedExtractions(failedExtractions);
            }

            return successfullySavedPaths;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/generic_file_saver.py:76-109
        // Original: def build_paths_to_write(self) -> dict[T, Path]:
        private Dictionary<FileResource, string> BuildPathsToWrite()
        {
            var pathsToWrite = new Dictionary<FileResource, string>();

            if (_resources.Count == 1)
            {
                // Single file - prompt for save location
                // Will be implemented when file dialogs are available
                var resource = _resources[0];
                string defaultPath = $"{resource.ResName}.{resource.ResType.Extension}";
                pathsToWrite[resource] = defaultPath;
            }
            else if (_resources.Count > 1)
            {
                // Multiple files - prompt for folder
                // Will be implemented when file dialogs are available
                string folderPath = Path.GetTempPath();
                foreach (var resource in _resources)
                {
                    string filePath = Path.Combine(folderPath, $"{resource.ResName}.{resource.ResType.Extension}");
                    pathsToWrite[resource] = filePath;
                }
            }

            return pathsToWrite;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/save/generic_file_saver.py:200-234
        // Original: def _handle_failed_extractions(self, failed_extractions: dict[Path, Exception]):
        private void HandleFailedExtractions(Dictionary<string, Exception> failedExtractions)
        {
            // Show error message - will be implemented when MessageBox is available
            foreach (var kvp in failedExtractions)
            {
                System.Console.WriteLine($"Failed to save {kvp.Key}: {kvp.Value}");
            }
        }
    }
}
