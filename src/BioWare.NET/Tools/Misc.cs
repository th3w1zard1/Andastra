using System;
using System.IO;
using JetBrains.Annotations;

namespace BioWare.NET.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py
    // Original: def normalize_ext(str_repr: os.PathLike | str) -> os.PathLike | str:
    public static class FileHelpers
    {
        public static string NormalizeExt(string strRepr)
        {
            if (string.IsNullOrEmpty(strRepr))
            {
                return "";
            }
            if (strRepr[0] == '.')
            {
                return $"stem{strRepr}";
            }
            if (!strRepr.Contains("."))
            {
                return $"stem.{strRepr}";
            }
            return strRepr;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:23-33
        // Original: def normalize_stem(str_repr: os.PathLike | str) -> os.PathLike | str:
        public static string NormalizeStem(string strRepr)
        {
            if (string.IsNullOrEmpty(strRepr))
            {
                return "";
            }
            if (strRepr.EndsWith("."))
            {
                return $"{strRepr}ext";
            }
            if (!strRepr.Contains("."))
            {
                return $"{strRepr}.ext";
            }
            return strRepr;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:36-40
        // Original: def is_nss_file(filepath: os.PathLike | str) -> bool:
        public static bool IsNssFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            return Path.GetExtension(NormalizeExt(filepath)).Equals(".nss", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:43-47
        // Original: def is_mod_file(filepath: os.PathLike | str) -> bool:
        public static bool IsModFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            return Path.GetExtension(NormalizeExt(filepath)).Equals(".mod", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:50-54
        // Original: def is_erf_file(filepath: os.PathLike | str) -> bool:
        public static bool IsErfFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            return Path.GetExtension(NormalizeExt(filepath)).Equals(".erf", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:57-61
        // Original: def is_sav_file(filepath: os.PathLike | str) -> bool:
        public static bool IsSavFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            return Path.GetExtension(NormalizeExt(filepath)).Equals(".sav", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:64-68
        // Original: def is_any_erf_type_file(filepath: os.PathLike | str) -> bool:
        public static bool IsAnyErfTypeFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            string ext = Path.GetExtension(NormalizeExt(filepath)).ToLowerInvariant();
            return ext == ".erf" || ext == ".mod" || ext == ".sav";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:71-75
        // Original: def is_rim_file(filepath: os.PathLike | str) -> bool:
        public static bool IsRimFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            return Path.GetExtension(NormalizeExt(filepath)).Equals(".rim", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:78-87
        // Original: def is_bif_file(filepath: os.PathLike | str) -> bool:
        public static bool IsBifFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            // Fast path: use string operations instead of Path for better performance
            string lowerPath = filepath.ToLowerInvariant();
            return lowerPath.EndsWith(".bif", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:90-97
        // Original: def is_bzf_file(filepath: os.PathLike | str) -> bool:
        public static bool IsBzfFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            // Fast path: use string operations instead of Path for better performance
            return filepath.ToLowerInvariant().EndsWith(".bzf", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:100-111
        // Original: def is_capsule_file(filepath: os.PathLike | str) -> bool:
        public static bool IsCapsuleFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            // Fast path: use string operations instead of Path for better performance
            // Check common extensions directly without creating path objects
            string lowerPath = filepath.ToLowerInvariant();
            return lowerPath.EndsWith(".erf", StringComparison.OrdinalIgnoreCase) ||
                   lowerPath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) ||
                   lowerPath.EndsWith(".rim", StringComparison.OrdinalIgnoreCase) ||
                   lowerPath.EndsWith(".sav", StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/misc.py:114-118
        // Original: def is_storage_file(filepath: os.PathLike | str) -> bool:
        public static bool IsStorageFile(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                return false;
            }
            string ext = Path.GetExtension(NormalizeExt(filepath)).ToLowerInvariant();
            return ext == ".erf" || ext == ".mod" || ext == ".sav" || ext == ".rim" || ext == ".bif";
        }
    }
}

