using System;
using System.IO;

namespace HolocronToolset.Blender
{
    /// <summary>
    /// Information about a detected Blender installation.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/blender/detection.py:34-66
    /// </summary>
    public class BlenderInfo
    {
        /// <summary>
        /// Path to the Blender executable.
        /// </summary>
        public string Executable { get; set; }

        /// <summary>
        /// Blender version as (major, minor, patch) tuple, or null if unknown.
        /// </summary>
        public (int Major, int Minor, int Patch)? Version { get; set; }

        /// <summary>
        /// Blender version as string (e.g., "4.2.0").
        /// </summary>
        public string VersionString { get; set; }

        /// <summary>
        /// Path to Blender addons directory.
        /// </summary>
        public string AddonsPath { get; set; }

        /// <summary>
        /// Path to Blender extensions directory (Blender 4.2+).
        /// </summary>
        public string ExtensionsPath { get; set; }

        /// <summary>
        /// Whether this is a valid Blender installation.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether kotorblender addon is installed.
        /// </summary>
        public bool HasKotorblender { get; set; }

        /// <summary>
        /// kotorblender addon version string.
        /// </summary>
        public string KotorblenderVersion { get; set; }

        /// <summary>
        /// Error message if detection failed.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Check if Blender version supports extensions (4.2+).
        /// </summary>
        public bool SupportsExtensions
        {
            get
            {
                if (!Version.HasValue)
                {
                    return false;
                }
                var v = Version.Value;
                return v.Major > 4 || (v.Major == 4 && v.Minor >= 2);
            }
        }

        /// <summary>
        /// Get the path where kotorblender should be installed.
        /// </summary>
        public string KotorblenderPath
        {
            get
            {
                if (SupportsExtensions && !string.IsNullOrEmpty(ExtensionsPath))
                {
                    return Path.Combine(ExtensionsPath, "user_default", "io_scene_kotor");
                }
                if (!string.IsNullOrEmpty(AddonsPath))
                {
                    return Path.Combine(AddonsPath, "io_scene_kotor");
                }
                return null;
            }
        }

        /// <summary>
        /// Initialize version string from version tuple.
        /// </summary>
        public void UpdateVersionString()
        {
            if (Version.HasValue)
            {
                var v = Version.Value;
                VersionString = $"{v.Major}.{v.Minor}.{v.Patch}";
            }
            else
            {
                VersionString = "";
            }
        }
    }
}

