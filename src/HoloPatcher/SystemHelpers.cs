using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace HoloPatcher
{
    /// <summary>
    /// System helper methods matching Python's utility functions.
    /// </summary>
    public static class SystemHelpers
    {
        // Windows API imports for console hiding
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        /// <summary>
        /// Hides the console window in GUI mode - matches Python's hide_console.
        /// </summary>
        public static void HideConsole()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_HIDE);
                }
            }
        }

        /// <summary>
        /// Shows the console window.
        /// </summary>
        public static void ShowConsole()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                IntPtr handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_SHOW);
                }
            }
        }

        /// <summary>
        /// Plays the system completion sound - matches Python's play_complete_sound.
        /// </summary>
        public static void PlayCompleteSound()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Play the system "exclamation" sound (MB_ICONEXCLAMATION = 0x30)
                Console.Beep(800, 200);
            }
        }

        /// <summary>
        /// Plays the system error sound - matches Python's play_error_sound.
        /// </summary>
        public static void PlayErrorSound()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Play the system 'error' sound (MB_ICONHAND = 0x10)
                Console.Beep(300, 400);
            }
        }

        /// <summary>
        /// Fix permissions on a directory - matches Python's fix_permissions/gain_access.
        /// </summary>
        public static void FixPermissions(string directory, Action<string> logAction = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Unix-like systems, use chmod
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"-R 755 \"{directory}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (var process = Process.Start(psi))
                    {
                        process?.WaitForExit();
                    }
                    logAction?.Invoke($"Fixed permissions for: {directory}");
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"[ERROR] Failed to fix permissions: {ex.Message}");
                }
                return;
            }

            // Windows-specific permission handling
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        // Remove read-only attribute
                        FileAttributes attrs = File.GetAttributes(file);
                        if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(file, attrs & ~FileAttributes.ReadOnly);
                            logAction?.Invoke($"Removed read-only from: {file}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke($"[WARNING] Could not modify: {file} - {ex.Message}");
                    }
                }

                foreach (string dir in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileAttributes attrs = File.GetAttributes(dir);
                        if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(dir, attrs & ~FileAttributes.ReadOnly);
                            logAction?.Invoke($"Removed read-only from directory: {dir}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke($"[WARNING] Could not modify directory: {dir} - {ex.Message}");
                    }
                }

                logAction?.Invoke($"Successfully processed permissions for: {directory}");
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"Failed to fix permissions for {directory}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convert all files and folders in directory to lowercase - matches Python's lowercase_files_and_folders.
        /// For iOS compatibility.
        /// </summary>
        public static void FixCaseSensitivity(string directory, Action<string> logAction = null)
        {
            // Process files and folders from deepest to shallowest (topdown=False in Python)
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                string dir = Path.GetDirectoryName(file) ?? "";
                string fileName = Path.GetFileName(file);
                string lowerName = fileName.ToLowerInvariant();

                if (fileName != lowerName)
                {
                    string newPath = Path.Combine(dir, lowerName);
                    // Use temp rename to handle case-insensitive file systems
                    string tempPath = Path.Combine(dir, Guid.NewGuid().ToString());
                    File.Move(file, tempPath);
                    File.Move(tempPath, newPath);
                    logAction?.Invoke($"Renaming {file} to '{lowerName}'");
                }
            }

            // Get all directories sorted by depth (deepest first)
            var directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length)
                .ToList();

            foreach (string dir in directories)
            {
                string parentDir = Path.GetDirectoryName(dir) ?? "";
                string dirName = Path.GetFileName(dir);
                string lowerName = dirName.ToLowerInvariant();

                if (dirName != lowerName)
                {
                    string newPath = Path.Combine(parentDir, lowerName);
                    // Use temp rename to handle case-insensitive file systems
                    string tempPath = Path.Combine(parentDir, Guid.NewGuid().ToString());
                    Directory.Move(dir, tempPath);
                    Directory.Move(tempPath, newPath);
                    logAction?.Invoke($"Renaming {dir} to '{lowerName}'");
                }
            }

            // Handle the root directory itself
            string rootDirName = Path.GetFileName(directory);
            string lowerRootName = rootDirName.ToLowerInvariant();
            if (rootDirName != lowerRootName)
            {
                string parentPath = Path.GetDirectoryName(directory) ?? "";
                string newRootPath = Path.Combine(parentPath, lowerRootName);
                string tempRootPath = Path.Combine(parentPath, Guid.NewGuid().ToString());
                Directory.Move(directory, tempRootPath);
                Directory.Move(tempRootPath, newRootPath);
                logAction?.Invoke($"Renaming {directory} to '{lowerRootName}'");
            }
        }

        /// <summary>
        /// Check if directory has access - matches Python's has_access.
        /// </summary>
        public static bool HasAccess(string path, bool recurse = false)
        {
            try
            {
                if (File.Exists(path))
                {
                    // Check file access
                    using (var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
                    {
                        return true;
                    }
                }

                if (Directory.Exists(path))
                {
                    // Check directory access
                    string testFile = Path.Combine(path, ".holopatcher_access_test");
                    try
                    {
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch
                    {
                        return false;
                    }

                    if (recurse)
                    {
                        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                using (var fs = File.Open(file, FileMode.Open, FileAccess.ReadWrite))
                                {
                                    // Access ok
                                }
                            }
                            catch
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}

