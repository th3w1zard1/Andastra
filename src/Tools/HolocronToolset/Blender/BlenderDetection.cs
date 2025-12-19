using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace HolocronToolset.Blender
{
    /// <summary>
    /// Blender installation detection for Windows, macOS, and Linux.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/blender/detection.py
    /// </summary>
    public static class BlenderDetection
    {
        private const int MinVersionMajor = 3;
        private const int MinVersionMinor = 6;
        private const int MinVersionPatch = 0;

        /// <summary>
        /// Find all valid Blender installations on the system.
        /// Similar to find_kotor_paths_from_default pattern.
        /// </summary>
        public static List<BlenderInfo> FindAllBlenderInstallations()
        {
            var candidates = new List<string>();

            // Check PATH
            string pathBlender = GetBlenderFromPath();
            if (!string.IsNullOrEmpty(pathBlender))
            {
                candidates.Add(pathBlender);
            }

            // Windows registry
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                candidates.AddRange(GetWindowsRegistryBlenderPaths());
            }

            // Common paths
            candidates.AddRange(GetCommonBlenderPaths());

            // Remove duplicates while preserving order
            var uniqueCandidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                try
                {
                    string resolved = Path.GetFullPath(candidate);
                    if (!seen.Contains(resolved))
                    {
                        seen.Add(resolved);
                        uniqueCandidates.Add(candidate);
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Build info for each valid installation
            var installations = new List<BlenderInfo>();

            foreach (var candidate in uniqueCandidates)
            {
                var version = GetBlenderVersion(candidate);
                if (version == null)
                {
                    continue;
                }

                if (version.Value.Major < MinVersionMajor ||
                    (version.Value.Major == MinVersionMajor && version.Value.Minor < MinVersionMinor) ||
                    (version.Value.Major == MinVersionMajor && version.Value.Minor == MinVersionMinor && version.Value.Patch < MinVersionPatch))
                {
                    continue;
                }

                var (addonsPath, extensionsPath) = GetBlenderConfigPaths(version.Value);

                var info = new BlenderInfo
                {
                    Executable = candidate,
                    Version = version,
                    AddonsPath = addonsPath,
                    ExtensionsPath = extensionsPath,
                    IsValid = true
                };
                info.UpdateVersionString();

                info.HasKotorblender = CheckKotorblenderInstalled(info);
                installations.Add(info);
            }

            // Sort by version (newest first), then by kotorblender status
            installations.Sort((a, b) =>
            {
                int kotorCompare = b.HasKotorblender.CompareTo(a.HasKotorblender);
                if (kotorCompare != 0) return kotorCompare;

                if (!a.Version.HasValue && !b.Version.HasValue) return 0;
                if (!a.Version.HasValue) return 1;
                if (!b.Version.HasValue) return -1;

                var aV = a.Version.Value;
                var bV = b.Version.Value;

                int majorCompare = bV.Major.CompareTo(aV.Major);
                if (majorCompare != 0) return majorCompare;

                int minorCompare = bV.Minor.CompareTo(aV.Minor);
                if (minorCompare != 0) return minorCompare;

                return bV.Patch.CompareTo(aV.Patch);
            });

            return installations;
        }

        /// <summary>
        /// Find a valid Blender installation.
        /// </summary>
        public static BlenderInfo FindBlenderExecutable(string customPath = null)
        {
            // Custom path takes priority
            if (!string.IsNullOrEmpty(customPath))
            {
                string executable = null;
                var custom = new DirectoryInfo(customPath);
                if (custom.Exists)
                {
                    // Check for blender executable in directory
                    foreach (var name in new[] { "blender", "blender.exe", "Blender" })
                    {
                        var exe = new FileInfo(Path.Combine(custom.FullName, name));
                        if (exe.Exists)
                        {
                            executable = exe.FullName;
                            break;
                        }
                    }

                    // macOS .app bundle
                    if (executable == null && customPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    {
                        var exe = new FileInfo(Path.Combine(custom.FullName, "Contents", "MacOS", "Blender"));
                        if (exe.Exists)
                        {
                            executable = exe.FullName;
                        }
                    }
                }
                else
                {
                    var customFile = new FileInfo(customPath);
                    if (customFile.Exists)
                    {
                        executable = customFile.FullName;
                    }
                }

                if (!string.IsNullOrEmpty(executable))
                {
                    var version = GetBlenderVersion(executable);
                    if (version.HasValue)
                    {
                        var v = version.Value;
                        if (v.Major >= MinVersionMajor && (v.Major > MinVersionMajor || v.Minor >= MinVersionMinor))
                        {
                            var (addonsPath, extensionsPath) = GetBlenderConfigPaths(v);
                            var info = new BlenderInfo
                            {
                                Executable = executable,
                                Version = version,
                                AddonsPath = addonsPath,
                                ExtensionsPath = extensionsPath,
                                IsValid = true
                            };
                            info.UpdateVersionString();
                            info.HasKotorblender = CheckKotorblenderInstalled(info);
                            return info;
                        }
                    }
                }
            }

            // Find all installations and return the best one
            var installations = FindAllBlenderInstallations();
            return installations.FirstOrDefault();
        }

        /// <summary>
        /// Detect Blender installation with full status information.
        /// </summary>
        public static BlenderInfo DetectBlender(string customPath = null)
        {
            var info = FindBlenderExecutable(customPath);

            if (info == null)
            {
                return new BlenderInfo
                {
                    Executable = "",
                    IsValid = false,
                    Error = "No valid Blender installation found. Please install Blender 3.6 or later."
                };
            }

            if (!info.HasKotorblender)
            {
                info.Error = $"Blender {info.VersionString} found but kotorblender add-on is not installed. " +
                            "Click 'Install kotorblender' to install it automatically.";
            }

            return info;
        }

        /// <summary>
        /// Get Blender paths from Windows registry.
        /// </summary>
        private static List<string> GetWindowsRegistryBlenderPaths()
        {
            var paths = new List<string>();

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return paths;
            }

            try
            {
                var registryKeys = new[]
                {
                    (RegistryHive.LocalMachine, @"SOFTWARE\BlenderFoundation\Blender"),
                    (RegistryHive.CurrentUser, @"SOFTWARE\BlenderFoundation\Blender"),
                    (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\BlenderFoundation\Blender")
                };

                foreach (var (hive, keyPath) in registryKeys)
                {
                    try
                    {
                        using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                        using (var key = baseKey.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                // Enumerate subkeys (version numbers like "4.2", "3.6")
                                foreach (var versionKeyName in key.GetSubKeyNames())
                                {
                                    try
                                    {
                                        using (var versionKey = key.OpenSubKey(versionKeyName))
                                        {
                                            if (versionKey != null)
                                            {
                                                var installPath = versionKey.GetValue("") as string;
                                                if (!string.IsNullOrEmpty(installPath))
                                                {
                                                    var exePath = Path.Combine(installPath, "blender.exe");
                                                    if (File.Exists(exePath))
                                                    {
                                                        paths.Add(exePath);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                // Registry access may fail
            }

            return paths;
        }

        /// <summary>
        /// Get common Blender installation paths based on OS.
        /// </summary>
        private static List<string> GetCommonBlenderPaths()
        {
            var paths = new List<string>();
            var system = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Darwin" : "Linux";

            if (system == "Windows")
            {
                var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");

                var windowsPaths = new[]
                {
                    Path.Combine(programFiles ?? "", "Blender Foundation"),
                    Path.Combine(programFilesX86 ?? "", "Blender Foundation"),
                    Path.Combine(localAppData ?? "", "Blender Foundation"),
                    Path.Combine(programFiles ?? "", "Steam", "steamapps", "common", "Blender"),
                    Path.Combine(programFilesX86 ?? "", "Steam", "steamapps", "common", "Blender"),
                    @"C:\ProgramData\chocolatey\lib\blender\tools",
                    @"C:\Blender",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Blender")
                };

                foreach (var basePath in windowsPaths)
                {
                    if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                    {
                        continue;
                    }

                    // Check for versioned directories
                    try
                    {
                        var dir = new DirectoryInfo(basePath);
                        foreach (var item in dir.GetDirectories())
                        {
                            if (item.Name.ToLowerInvariant().StartsWith("blender"))
                            {
                                var exe = new FileInfo(Path.Combine(item.FullName, "blender.exe"));
                                if (exe.Exists)
                                {
                                    paths.Add(exe.FullName);
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            else if (system == "Darwin")
            {
                var appsDirs = new[]
                {
                    "/Applications",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications")
                };

                foreach (var appsDir in appsDirs)
                {
                    if (!Directory.Exists(appsDir))
                    {
                        continue;
                    }

                    try
                    {
                        var dir = new DirectoryInfo(appsDir);
                        foreach (var item in dir.GetDirectories())
                        {
                            if (item.Name.StartsWith("Blender", StringComparison.OrdinalIgnoreCase) && item.Extension == ".app")
                            {
                                var exe = new FileInfo(Path.Combine(item.FullName, "Contents", "MacOS", "Blender"));
                                if (exe.Exists)
                                {
                                    paths.Add(exe.FullName);
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Homebrew paths
                var homebrewPaths = new[] { "/opt/homebrew/bin/blender", "/usr/local/bin/blender" };
                foreach (var path in homebrewPaths)
                {
                    if (File.Exists(path))
                    {
                        paths.Add(path);
                    }
                }
            }
            else // Linux
            {
                var linuxPaths = new[]
                {
                    "/usr/bin/blender",
                    "/usr/local/bin/blender",
                    "/snap/bin/blender",
                    "/var/lib/snapd/snap/bin/blender",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "flatpak", "exports", "bin", "org.blender.Blender"),
                    "/var/lib/flatpak/exports/bin/org.blender.Blender",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "blender"),
                    "/opt/blender/blender"
                };

                foreach (var path in linuxPaths)
                {
                    if (File.Exists(path))
                    {
                        paths.Add(path);
                    }
                }

                // Check for versioned installations in /opt and home
                var optDirs = new[] { "/opt", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
                foreach (var optDir in optDirs)
                {
                    if (!Directory.Exists(optDir))
                    {
                        continue;
                    }

                    try
                    {
                        var dir = new DirectoryInfo(optDir);
                        foreach (var item in dir.GetDirectories())
                        {
                            if (item.Name.ToLowerInvariant().StartsWith("blender"))
                            {
                                var exe = new FileInfo(Path.Combine(item.FullName, "blender"));
                                if (exe.Exists)
                                {
                                    paths.Add(exe.FullName);
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            return paths;
        }

        /// <summary>
        /// Get Blender from system PATH.
        /// </summary>
        private static string GetBlenderFromPath()
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            var pathDirs = pathEnv.Split(Path.PathSeparator);
            foreach (var dir in pathDirs)
            {
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                foreach (var name in new[] { "blender", "blender.exe", "Blender" })
                {
                    var exePath = Path.Combine(dir, name);
                    if (File.Exists(exePath))
                    {
                        return exePath;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get Blender version from executable.
        /// </summary>
        private static (int Major, int Minor, int Patch)? GetBlenderVersion(string executable)
        {
            if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
            {
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    process.WaitForExit(10000); // 10 second timeout

                    string output = process.StandardOutput.ReadToEnd();
                    if (string.IsNullOrEmpty(output))
                    {
                        output = process.StandardError.ReadToEnd();
                    }

                    // Parse version from output like "Blender 4.2.0"
                    var match = Regex.Match(output, @"Blender\s+(\d+)\.(\d+)\.(\d+)");
                    if (match.Success)
                    {
                        int major = int.Parse(match.Groups[1].Value);
                        int minor = int.Parse(match.Groups[2].Value);
                        int patch = int.Parse(match.Groups[3].Value);
                        return (major, minor, patch);
                    }
                }
            }
            catch
            {
                // Failed to get version
            }

            return null;
        }

        /// <summary>
        /// Get Blender addons and extensions paths for a given version.
        /// </summary>
        private static (string AddonsPath, string ExtensionsPath) GetBlenderConfigPaths((int Major, int Minor, int Patch) version)
        {
            string versionStr = $"{version.Major}.{version.Minor}";
            string addonsPath = null;
            string extensionsPath = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var appData = Environment.GetEnvironmentVariable("APPDATA");
                if (!string.IsNullOrEmpty(appData))
                {
                    var basePath = Path.Combine(appData, "Blender Foundation", "Blender", versionStr);
                    addonsPath = Path.Combine(basePath, "scripts", "addons");
                    extensionsPath = Path.Combine(basePath, "extensions");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var basePath = Path.Combine(home, "Library", "Application Support", "Blender", versionStr);
                addonsPath = Path.Combine(basePath, "scripts", "addons");
                extensionsPath = Path.Combine(basePath, "extensions");
            }
            else // Linux
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var basePath = Path.Combine(home, ".config", "blender", versionStr);
                addonsPath = Path.Combine(basePath, "scripts", "addons");
                extensionsPath = Path.Combine(basePath, "extensions");
            }

            return (addonsPath, extensionsPath);
        }

        /// <summary>
        /// Check if kotorblender is installed and get its version.
        /// </summary>
        private static bool CheckKotorblenderInstalled(BlenderInfo info)
        {
            string kotorblenderPath = info.KotorblenderPath;
            if (string.IsNullOrEmpty(kotorblenderPath))
            {
                return false;
            }

            var initFile = new FileInfo(Path.Combine(kotorblenderPath, "__init__.py"));
            if (!initFile.Exists)
            {
                return false;
            }

            // Try to extract version from __init__.py
            try
            {
                string content = File.ReadAllText(initFile.FullName);
                var match = Regex.Match(content, @"\""version\""\s*:\s*\((\d+),\s*(\d+),\s*(\d+)\)");
                if (match.Success)
                {
                    info.KotorblenderVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                    return true;
                }
            }
            catch
            {
                // Failed to read or parse
            }

            // File exists but couldn't parse version - assume installed
            return true;
        }
    }
}

