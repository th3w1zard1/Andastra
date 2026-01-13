using System;
using System.Globalization;

namespace BioWare.NET.Utility
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/misc.py:348-387
    // Original: def is_int(val: str | int | Buffer | SupportsInt | SupportsIndex) -> bool:
    /// <summary>
    /// Can be cast to an int without raising an error.
    ///
    /// Args:
    ///     val: The value to try to convert
    ///
    /// Returns:
    ///     True if val can be converted else False
    /// </summary>
    public static class UtilityMisc
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/misc.py:348-366
        // Original: def is_int(val: str | int | Buffer | SupportsInt | SupportsIndex) -> bool:
        public static bool IsInt(object val)
        {
            if (val == null)
            {
                return false;
            }

            if (val is int || val is long || val is short || val is byte || val is sbyte || val is uint || val is ulong || val is ushort)
            {
                return true;
            }

            if (val is string str)
            {
                return int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            }

            try
            {
                Convert.ToInt32(val);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/misc.py:369-387
        // Original: def is_float(val: str | float | Buffer | SupportsFloat | SupportsIndex) -> bool:
        /// <summary>
        /// Can be cast to a float without raising an error.
        ///
        /// Args:
        ///     val: The value to try to convert
        ///
        /// Returns:
        ///     True if val can be converted else False
        /// </summary>
        public static bool IsFloat(object val)
        {
            if (val == null)
            {
                return false;
            }

            if (val is float || val is double || val is decimal)
            {
                return true;
            }

            if (val is string str)
            {
                return float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            }

            try
            {
                Convert.ToDouble(val);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Matching PyKotor implementation at Libraries/Utility/src/utility/misc.py
        // Original: def generate_hash(data: bytes) -> str:
        /// <summary>
        /// Generate SHA256 hash of data.
        /// </summary>
        public static string GenerateHash(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            using (global::System.Security.Cryptography.SHA256 sha256 = global::System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
