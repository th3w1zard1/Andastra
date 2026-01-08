using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:287-529
    // Original: class Color:
    // Renamed to ParsingColor to avoid conflicts with Andastra.Runtime.Graphics.Color and Microsoft.Xna.Framework.Color
    public partial class ParsingColor : IEquatable<ParsingColor>
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public static readonly ParsingColor RED = new ParsingColor(1.0f, 0.0f, 0.0f);
        public static readonly ParsingColor GREEN = new ParsingColor(0.0f, 1.0f, 0.0f);
        public static readonly ParsingColor BLUE = new ParsingColor(0.0f, 0.0f, 1.0f);
        public static readonly ParsingColor BLACK = new ParsingColor(0.0f, 0.0f, 0.0f);
        public static readonly ParsingColor WHITE = new ParsingColor(1.0f, 1.0f, 1.0f);

        public ParsingColor(float r, float g, float b, float a = 1.0f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static ParsingColor FromRgbInteger(int value)
        {
            float r = (value & 0x000000FF) / 255f;
            float g = ((value & 0x0000FF00) >> 8) / 255f;
            float b = ((value & 0x00FF0000) >> 16) / 255f;
            return new ParsingColor(r, g, b);
        }

        public static ParsingColor FromRgbaInteger(int value)
        {
            float r = (value & 0x000000FF) / 255f;
            float g = ((value & 0x0000FF00) >> 8) / 255f;
            float b = ((value & 0x00FF0000) >> 16) / 255f;
            float a = ((value & unchecked((int)0xFF000000)) >> 24) / 255f;
            return new ParsingColor(r, g, b, a);
        }

        public static ParsingColor FromBgrInteger(int value)
        {
            float r = ((value & 0x00FF0000) >> 16) / 255f;
            float g = ((value & 0x0000FF00) >> 8) / 255f;
            float b = (value & 0x000000FF) / 255f;
            return new ParsingColor(r, g, b);
        }

        public static ParsingColor FromRgbVector3(Vector3 vector)
        {
            return new ParsingColor(vector.X, vector.Y, vector.Z);
        }

        public static ParsingColor FromBgrVector3(Vector3 vector)
        {
            return new ParsingColor(vector.Z, vector.Y, vector.X);
        }

        public static ParsingColor FromHexString(string hex)
        {
            string colorStr = hex.TrimStart('#').ToLowerInvariant();
            ParsingColor instance = new ParsingColor(0, 0, 0);

            if (colorStr.Length == 3)
            {
                instance.R = Convert.ToInt32(new string(colorStr[0], 2), 16) / 255f;
                instance.G = Convert.ToInt32(new string(colorStr[1], 2), 16) / 255f;
                instance.B = Convert.ToInt32(new string(colorStr[2], 2), 16) / 255f;
                instance.A = 1.0f;
            }
            else if (colorStr.Length == 4)
            {
                instance.R = Convert.ToInt32(new string(colorStr[0], 2), 16) / 255f;
                instance.G = Convert.ToInt32(new string(colorStr[1], 2), 16) / 255f;
                instance.B = Convert.ToInt32(new string(colorStr[2], 2), 16) / 255f;
                instance.A = Convert.ToInt32(new string(colorStr[3], 2), 16) / 255f;
            }
            else if (colorStr.Length == 6)
            {
                instance.R = Convert.ToInt32(colorStr.Substring(0, 2), 16) / 255f;
                instance.G = Convert.ToInt32(colorStr.Substring(2, 2), 16) / 255f;
                instance.B = Convert.ToInt32(colorStr.Substring(4, 2), 16) / 255f;
                instance.A = 1.0f;
            }
            else if (colorStr.Length == 8)
            {
                instance.R = Convert.ToInt32(colorStr.Substring(0, 2), 16) / 255f;
                instance.G = Convert.ToInt32(colorStr.Substring(2, 2), 16) / 255f;
                instance.B = Convert.ToInt32(colorStr.Substring(4, 2), 16) / 255f;
                instance.A = Convert.ToInt32(colorStr.Substring(6, 2), 16) / 255f;
            }
            else
            {
                throw new ArgumentException("Invalid hex color format: " + colorStr);
            }

            return instance;
        }

        public int ToRgbInteger()
        {
            int r = (int)(R * 255f) << 0;
            int g = (int)(G * 255f) << 8;
            int b = (int)(B * 255f) << 16;
            return r + g + b;
        }

        public int ToRgbaInteger()
        {
            int r = (int)(R * 255f) << 0;
            int g = (int)(G * 255f) << 8;
            int b = (int)(B * 255f) << 16;
            int a = (int)((A == 0 ? 1.0f : A) * 255f) << 24;
            return r + g + b + a;
        }

        public int ToBgrInteger()
        {
            int r = (int)(R * 255f) << 16;
            int g = (int)(G * 255f) << 8;
            int b = (int)(B * 255f);
            return r + g + b;
        }

        public Vector3 ToRgbVector3()
        {
            return new Vector3(R, G, B);
        }

        public Vector3 ToBgrVector3()
        {
            return new Vector3(B, G, R);
        }

        public override string ToString()
        {
            return R + " " + G + " " + B + " " + A;
        }

        public override int GetHashCode()
        {
            return (R, G, B, A == 0 ? 1.0f : A).GetHashCode();
        }

        public bool Equals(ParsingColor other)
        {
            if (other == null)
            {
                return false;
            }
            return GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            ParsingColor other = obj as ParsingColor;
            if (other == null)
            {
                return false;
            }
            return Equals(other);
        }
    }

    /// <summary>
    /// Type alias for ParsingColor to maintain compatibility with code expecting Color.
    /// </summary>
    public class Color : ParsingColor
    {
        public Color(float r, float g, float b, float a = 1.0f) : base(r, g, b, a)
        {
        }

        public Color(ParsingColor other) : base(other.R, other.G, other.B, other.A)
        {
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:528-572
    // Original: class WrappedInt:
    public class WrappedInt : IEquatable<WrappedInt>
    {
        private int _value;

        public WrappedInt(int value = 0)
        {
            _value = value;
        }

        public void Add(WrappedInt other)
        {
            if (other != null)
            {
                _value += other.Get();
            }
        }

        public void Add(int other)
        {
            _value += other;
        }

        public void Set(int value)
        {
            _value = value;
        }

        public int Get()
        {
            return _value;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public bool Equals(WrappedInt other)
        {
            if (other == null)
            {
                return false;
            }
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            WrappedInt other = obj as WrappedInt;
            if (other == null)
            {
                return false;
            }
            return Equals(other);
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:574-604
    // Original: class InventoryItem:
    public class InventoryItem : IEquatable<InventoryItem>
    {
        public ResRef ResRef { get; }
        public bool Droppable { get; }
        public bool Infinite { get; }

        public InventoryItem(ResRef resref, bool droppable = false, bool infinite = false)
        {
            ResRef = resref;
            Droppable = droppable;
            Infinite = infinite;
        }

        public override string ToString()
        {
            return ResRef.ToString();
        }

        public override int GetHashCode()
        {
            return ResRef.GetHashCode();
        }

        public bool Equals(InventoryItem other)
        {
            if (other == null)
            {
                return false;
            }

            return ResRef.Equals(other.ResRef) && Droppable == other.Droppable;
        }

        public override bool Equals(object obj)
        {
            InventoryItem other = obj as InventoryItem;
            if (other == null)
            {
                return false;
            }
            return Equals(other);
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:606-624
    // Original: class EquipmentSlot(Enum):
    [Flags]
    public enum EquipmentSlot
    {
        INVALID = 0,
        HEAD = 1,
        ARMOR = 2,
        GAUNTLET = 8,
        RIGHT_HAND = 16,
        LEFT_HAND = 32,
        RIGHT_ARM = 128,
        LEFT_ARM = 256,
        IMPLANT = 512,
        BELT = 1024,
        CLAW1 = 16384,
        CLAW2 = 32768,
        CLAW3 = 65536,
        HIDE = 131072,
        RIGHT_HAND_2 = 262144,
        LEFT_HAND_2 = 524288
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:626-713
    // Original: class CaseInsensitiveHashSet(set, Generic[T]):
    public class CaseInsensitiveHashSet<T> : HashSet<T>
    {
        private static IEqualityComparer<T> BuildComparer()
        {
            if (typeof(T) == typeof(string))
            {
                return (IEqualityComparer<T>)StringComparer.OrdinalIgnoreCase;
            }

            return EqualityComparer<T>.Default;
        }

        public CaseInsensitiveHashSet() : base(BuildComparer())
        {
        }

        public CaseInsensitiveHashSet(IEnumerable<T> collection) : base(collection ?? Enumerable.Empty<T>(), BuildComparer())
        {
        }

        public void Update(params IEnumerable<T>[] others)
        {
            if (others == null)
            {
                return;
            }

            foreach (IEnumerable<T> other in others)
            {
                if (other == null)
                {
                    continue;
                }

                foreach (T item in other)
                {
                    Add(item);
                }
            }
        }
    }
}

namespace Andastra.Parsing.Common
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
