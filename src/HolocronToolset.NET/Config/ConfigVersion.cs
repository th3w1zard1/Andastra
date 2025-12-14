using System;
using System.Linq;

namespace HolocronToolset.NET.Config
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_version.py:4
    // Original: def version_to_toolset_tag(version: str) -> str:
    public static class ConfigVersion
    {
        public static string VersionToToolsetTag(string version)
        {
            const int majorMinorPatchCount = 2;
            if (version.Count(c => c == '.') == majorMinorPatchCount)
            {
                int firstDotIndex = version.IndexOf('.');
                int secondDotIndex = version.IndexOf('.', firstDotIndex + 1);
                version = version.Substring(0, secondDotIndex) + version.Substring(secondDotIndex + 1);
            }
            return $"v{version}-toolset";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_version.py:14
        // Original: def toolset_tag_to_version(tag: str) -> str:
        public static string ToolsetTagToVersion(string tag)
        {
            string numericPart = new string(tag.Where(c => char.IsDigit(c) || c == '.').ToArray());
            string[] parts = numericPart.Split('.');

            const int majorMinorPatchLen = 3;
            if (parts.Length == majorMinorPatchLen)
            {
                return string.Join(".", parts);
            }
            const int majorMinorLen = 2;
            if (parts.Length == majorMinorLen)
            {
                return string.Join(".", parts);
            }

            // Handle the legacy typo format (missing second dot)
            // When there's only one part like "400", it means "4.0.0"
            const int majorLen = 1;
            string major = parts[0];
            if (parts.Length > majorLen && parts[1].Length > 1)
            {
                // Assume the minor version always precedes the concatenated patch version
                string minor = parts[1].Substring(0, 1);
                string patch = parts[1].Substring(1);
                return $"{major}.{minor}.{patch}";
            }
            else if (parts.Length == 1 && parts[0].Length >= 3)
            {
                // Handle case like "400" -> "4.0.0"
                string majorStr = parts[0].Substring(0, 1);
                string minorStr = parts[0].Length > 1 ? parts[0].Substring(1, 1) : "0";
                string patchStr = parts[0].Length > 2 ? parts[0].Substring(2) : "0";
                return $"{majorStr}.{minorStr}.{patchStr}";
            }

            return $"{major}.0.0";
        }
    }
}
