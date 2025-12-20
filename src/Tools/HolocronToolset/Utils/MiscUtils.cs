using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Input;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.RIM;

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
        public static void OpenLink(string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return;
            }

            try
            {
                var uri = new Uri(link);
                // TODO: SIMPLIFIED - Using Process.Start as workaround, should use Avalonia's Launcher when TopLevel is available
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = link,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore errors
            }
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
        public static byte[] GetResourceFromFile(string filepath, string resname, Andastra.Parsing.Resource.ResourceType restype)
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
            { Key.AltLeft, "ALT" },
            { Key.AltRight, "ALT" },
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
    }
}
