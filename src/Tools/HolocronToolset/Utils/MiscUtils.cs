using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.RIM;

namespace HolocronToolset.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:117
    // Original: def get_nums(string_input: str) -> list[int]:
    public static class MiscUtils
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:117-140
        // Original: def get_nums(string_input: str) -> list[int]:
        public static List<int> GetNums(string stringInput)
        {
            var nums = new List<int>();
            var currentNum = new StringBuilder();
            foreach (char c in stringInput + " ")
            {
                if (char.IsDigit(c))
                {
                    currentNum.Append(c);
                }
                else if (currentNum.Length > 0)
                {
                    if (int.TryParse(currentNum.ToString(), out int num))
                    {
                        nums.Add(num);
                    }
                    currentNum.Clear();
                }
            }
            return nums;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:143-145
        // Original: def open_link(link: str):
        //     url = QUrl(link)
        //     QDesktopServices.openUrl(url)
        /// <summary>
        /// Opens a link (URL or file path) using the system's default application.
        /// Matches PyKotor's open_link function behavior using QDesktopServices.openUrl.
        /// Uses Avalonia's Launcher API for cross-platform compatibility.
        /// </summary>
        /// <param name="link">The URL or file path to open</param>
        public static void OpenLink(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return;
            }

            // Fire-and-forget async operation to maintain synchronous API signature
            // This matches PyKotor's synchronous behavior while using Avalonia's async Launcher API
            Task.Run(async () =>
            {
                try
                {
                    // Check if the link is a URL (starts with http://, https://, mailto:, etc.)
                    // or a file path
                    bool isUrl = IsUrl(link);
                    
                    if (isUrl)
                    {
                        // Handle URLs using Process.Start (cross-platform)
                        // This matches PyKotor's QDesktopServices.openUrl behavior
                        if (Uri.TryCreate(link, UriKind.Absolute, out Uri uri))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = uri.ToString(),
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            // Invalid URI format, try as file path
                            await OpenFileAsync(link);
                        }
                    }
                    else
                    {
                        // Handle file paths using Process.Start
                        await OpenFileAsync(link);
                    }
                }
                catch
                {
                    // Ignore errors - matches PyKotor's behavior of silently failing
                    // This ensures the application continues to function even if link opening fails
                }
            });
        }

        /// <summary>
        /// Determines if a string is a URL (http://, https://, mailto:, etc.) or a file path.
        /// </summary>
        /// <param name="link">The string to check</param>
        /// <returns>True if the string appears to be a URL, false if it's a file path</returns>
        private static bool IsUrl(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return false;
            }

            // Trim whitespace
            link = link.Trim();

            // Check for common URL schemes
            // This matches how QUrl in PyKotor determines if something is a URL
            string lowerLink = link.ToLowerInvariant();
            
            // Common URL schemes that QDesktopServices.openUrl handles
            return lowerLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   lowerLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   lowerLink.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                   lowerLink.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                   lowerLink.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                   lowerLink.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                   lowerLink.StartsWith("sms:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Opens a file using Process.Start (cross-platform).
        /// Handles both absolute and relative file paths.
        /// </summary>
        /// <param name="filePath">The file path to open</param>
        private static Task OpenFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return Task.CompletedTask;
            }

            try
            {
                // Resolve relative paths to absolute paths
                string absolutePath = Path.IsPathRooted(filePath) 
                    ? filePath 
                    : Path.GetFullPath(filePath);

                // Check if file exists
                if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
                {
                    // File doesn't exist, try opening as URL anyway (might be a protocol handler)
                    if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri uri))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = uri.ToString(),
                            UseShellExecute = true
                        });
                    }
                    return Task.CompletedTask;
                }

                // Use Process.Start for cross-platform file opening
                Process.Start(new ProcessStartInfo
                {
                    FileName = absolutePath,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore errors - matches PyKotor's behavior
            }

            return Task.CompletedTask;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:148-153
        // Original: def clamp(value: float, min_value: float, max_value: float) -> float:
        public static float Clamp(float value, float minValue, float maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        public static double Clamp(double value, double minValue, double maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        public static int Clamp(int value, int minValue, int maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:221-262
        // Original: def get_resource_from_file(filepath, resname, restype) -> bytes:
        public static byte[] GetResourceFromFile(string filepath, string resname, BioWare.NET.Resource.ResourceType restype)
        {
            if (string.IsNullOrEmpty(filepath) || !System.IO.File.Exists(filepath))
            {
                throw new FileNotFoundException($"File not found: {filepath}");
            }

            string ext = System.IO.Path.GetExtension(filepath).ToLowerInvariant();

            // Check if it's an ERF type file
            if (ext == ".erf" || ext == ".mod" || ext == ".sav" || ext == ".hak")
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:249-251
                // Original: erf: ERF = read_erf(filepath); data = erf.get(resname, restype)
                ERF erf = ERFAuto.ReadErf(filepath);
                byte[] data = erf.Get(resname, restype);
                
                if (data == null)
                {
                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:258-260
                    // Original: if data is None: raise ValueError("Could not find resource in RIM/ERF")
                    throw new ArgumentException($"Could not find resource '{resname}.{restype.Extension}' in ERF file '{filepath}'");
                }
                
                return data;
            }
            else if (ext == ".rim")
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:252-254
                // Original: rim: RIM = read_rim(filepath); data = rim.get(resname, restype)
                RIM rim = RIMAuto.ReadRim(filepath);
                byte[] data = rim.Get(resname, restype);
                
                if (data == null)
                {
                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:258-260
                    // Original: if data is None: raise ValueError("Could not find resource in RIM/ERF")
                    throw new ArgumentException($"Could not find resource '{resname}.{restype.Extension}' in RIM file '{filepath}'");
                }
                
                return data;
            }
            else
            {
                // Regular file
                return System.IO.File.ReadAllBytes(filepath);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:25-34
        // Original: MODIFIER_KEY_NAMES: dict[Qt.Key, str] = { ... }
        private static readonly Dictionary<Key, string> ModifierKeyNames = new Dictionary<Key, string>
        {
            { Key.LeftCtrl, "CTRL" },
            { Key.RightCtrl, "CTRL" },
            { Key.LeftShift, "SHIFT" },
            { Key.RightShift, "SHIFT" },
            { Key.LeftAlt, "ALT" },
            { Key.RightAlt, "ALT" },
            { Key.LWin, "META" },
            { Key.RWin, "META" },
            { Key.LeftAlt, "ALT" },
            { Key.RightAlt, "ALT" },
            { Key.CapsLock, "CAPSLOCK" },
            { Key.NumLock, "NUMLOCK" },
            { Key.Scroll, "SCROLLLOCK" }
        };

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/misc.py:185-188
        // Original: def get_qt_key_string_localized(key: Qt.Key | str | int | bytes) -> str:
        /// <summary>
        /// Gets a localized, user-friendly string representation of an Avalonia Key.
        /// Matches PyKotor's get_qt_key_string_localized function behavior.
        /// </summary>
        /// <param name="key">The Avalonia Key to convert to string</param>
        /// <returns>A user-friendly string representation of the key (e.g., "CTRL", "SHIFT", "A", "F1")</returns>
        public static string GetKeyStringLocalized(Key key)
        {
            // Check if it's a modifier key first
            if (ModifierKeyNames.TryGetValue(key, out string modifierName))
            {
                return modifierName;
            }

            // For non-modifier keys, convert to readable format
            // Remove "Key_" prefix if present and convert to uppercase
            string keyName = key.ToString();
            
            // Remove common prefixes
            if (keyName.StartsWith("Key", StringComparison.OrdinalIgnoreCase))
            {
                keyName = keyName.Substring(3);
            }
            
            // Handle special cases
            if (keyName.StartsWith("D", StringComparison.OrdinalIgnoreCase) && keyName.Length == 2 && char.IsDigit(keyName[1]))
            {
                // D0-D9 -> 0-9
                return keyName[1].ToString();
            }
            
            // Convert CONTROL to CTRL for consistency
            keyName = keyName.Replace("CONTROL", "CTRL", StringComparison.OrdinalIgnoreCase);
            
            // Return uppercase, stripped of common prefixes
            return keyName.ToUpperInvariant().Trim();
        }

        /// <summary>
        /// Checks if a key is a modifier key (Ctrl, Shift, Alt, etc.)
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the key is a modifier key</returns>
        public static bool IsModifierKey(Key key)
        {
            return ModifierKeyNames.ContainsKey(key);
        }

        /// <summary>
        /// Gets the TopLevel window from the current Avalonia application.
        /// This is used to access StorageProvider for launching URIs and files.
        /// 
        /// Implements comprehensive fallback strategy matching PyKotor's QApplication.activeWindow() behavior:
        /// 1. Try MainWindow from desktop lifetime (primary window)
        /// 2. Try focused window from desktop.Windows (currently active window)
        /// 3. Try visible window from desktop.Windows (any visible window)
        /// 4. Try any window from desktop.Windows (last resort from application lifetime)
        /// 5. Try focused window from WindowUtils tracked windows (toolset-managed windows)
        /// 6. Try visible window from WindowUtils tracked windows (toolset-managed visible windows)
        /// 7. Try any window from WindowUtils tracked windows (last resort from toolset tracking)
        /// 
        /// This ensures we can always find a valid TopLevel window for StorageProvider operations,
        /// matching PyKotor's behavior where QApplication.activeWindow() returns the active window
        /// or any available window if no window has focus.
        /// </summary>
        /// <returns>The TopLevel window if available, null otherwise</returns>
        private static TopLevel GetTopLevel()
        {
            // Try to get TopLevel from Application.Current
            var app = Avalonia.Application.Current;
            if (app == null)
            {
                return null;
            }

            // Try to get TopLevel from application lifetime
            if (app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                // Strategy 1: Try MainWindow first (primary window, matches PyKotor's main window behavior)
                if (desktopLifetime.MainWindow != null)
                {
                    return desktopLifetime.MainWindow;
                }

                // Strategy 2: Try to find a focused window from desktop.Windows
                // This matches PyKotor's QApplication.activeWindow() behavior
                var focusedWindow = desktopLifetime.Windows.FirstOrDefault(w => w.IsFocused);
                if (focusedWindow != null)
                {
                    return focusedWindow;
                }

                // Strategy 3: Try to find any visible window from desktop.Windows
                // This handles cases where windows exist but none have focus
                var visibleWindow = desktopLifetime.Windows.FirstOrDefault(w => w.IsVisible);
                if (visibleWindow != null)
                {
                    return visibleWindow;
                }

                // Strategy 4: Try any window from desktop.Windows (last resort from application lifetime)
                // This ensures we return something if windows exist but aren't visible/focused
                if (desktopLifetime.Windows.Count > 0)
                {
                    return desktopLifetime.Windows[0];
                }
            }

            // Strategy 5-7: WindowUtils tracking removed to break circular dependency
            // These strategies required Editors project reference which created circular dependency
            // Fall back to desktop.Windows enumeration which should cover most cases

            // No windows available - return null
            // The calling code should handle the null case gracefully
            return null;
        }
    }
}
