// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:700-770
// Original: @dataclass class PathInfo: ...
using System;
using System.IO;
using BioWare.NET.Installation;

namespace KotorDiff.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:700-770
    // Original: @dataclass class PathInfo: ...
    public class PathInfo
    {
        public int Index { get; set; }
        public bool IsInstallation { get; set; }
        public bool IsFolder { get; set; }
        public bool IsFile { get; set; }
        public object PathOrInstallation { get; set; } // Can be string, DirectoryInfo, FileInfo, or Installation
        public string Name { get; set; }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:750-770
        // Original: @classmethod def from_path_or_installation(...): ...
        public static PathInfo FromPathOrInstallation(object pathOrInstallation, int index)
        {
            var info = new PathInfo
            {
                Index = index
            };

            if (pathOrInstallation is Installation installation)
            {
                info.IsInstallation = true;
                info.PathOrInstallation = installation;
                    info.Name = installation.Path;
            }
            else if (pathOrInstallation is string pathStr)
            {
                if (Directory.Exists(pathStr))
                {
                    info.IsFolder = true;
                    info.PathOrInstallation = new DirectoryInfo(pathStr);
                    info.Name = new DirectoryInfo(pathStr).Name;
                }
                else if (File.Exists(pathStr))
                {
                    info.IsFile = true;
                    info.PathOrInstallation = new FileInfo(pathStr);
                    info.Name = new FileInfo(pathStr).Name;
                }
            }
            else if (pathOrInstallation is DirectoryInfo dirInfo)
            {
                info.IsFolder = true;
                info.PathOrInstallation = dirInfo;
                info.Name = dirInfo.Name;
            }
            else if (pathOrInstallation is FileInfo fileInfo)
            {
                info.IsFile = true;
                info.PathOrInstallation = fileInfo;
                info.Name = fileInfo.Name;
            }

            return info;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:772-779
        // Original: def get_path(self) -> Path: ...
        public object GetPath()
        {
            if (IsInstallation && PathOrInstallation is Installation installation)
            {
                return new DirectoryInfo(installation.Path);
            }
            return PathOrInstallation;
        }
    }
}

