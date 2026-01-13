using System;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at utility/common/misc_string/util.py
    // Original: ireplace function for case-insensitive string replacement
    public static class StringUtils
    {
        // Matching PyKotor implementation at utility/common/misc_string/util.py
        // Original: def ireplace(text: str, old: str, new: str) -> str:
        /// <summary>
        /// Case-insensitive string replacement.
        /// </summary>
        public static string IReplace(string text, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldValue))
            {
                return text ?? string.Empty;
            }

            int index = 0;
            while ((index = text.IndexOf(oldValue, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                text = text.Substring(0, index) + newValue + text.Substring(index + oldValue.Length);
                index += newValue.Length;
            }

            return text;
        }
    }
}
