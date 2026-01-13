using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using BioWare.NET.Common;

namespace BioWare.NET.Common
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/common/misc.py:287-529
    // Original: class Color:
    public class Color : IEquatable<Color>
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public static readonly Color RED = new Color(1.0f, 0.0f, 0.0f);
        public static readonly Color GREEN = new Color(0.0f, 1.0f, 0.0f);
        public static readonly Color BLUE = new Color(0.0f, 0.0f, 1.0f);
        public static readonly Color BLACK = new Color(0.0f, 0.0f, 0.0f);
        public static readonly Color WHITE = new Color(1.0f, 1.0f, 1.0f);

        public Color(float r, float g, float b, float a = 1.0f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Color FromRgbInteger(int value)
        {
            float r = (value & 0x000000FF) / 255f;
            float g = ((value & 0x0000FF00) >> 8) / 255f;
            float b = ((value & 0x00FF0000) >> 16) / 255f;
            return new Color(r, g, b);
        }

        public static Color FromRgbaInteger(int value)
        {
            float r = (value & 0x000000FF) / 255f;
            float g = ((value & 0x0000FF00) >> 8) / 255f;
            float b = ((value & 0x00FF0000) >> 16) / 255f;
            float a = ((value & unchecked((int)0xFF000000)) >> 24) / 255f;
            return new Color(r, g, b, a);
        }

        public static Color FromBgrInteger(int value)
        {
            float r = ((value & 0x00FF0000) >> 16) / 255f;
            float g = ((value & 0x0000FF00) >> 8) / 255f;
            float b = (value & 0x000000FF) / 255f;
            return new Color(r, g, b);
        }

        public static Color FromRgbVector3(Vector3 vector)
        {
            return new Color(vector.X, vector.Y, vector.Z);
        }

        public static Color FromBgrVector3(Vector3 vector)
        {
            return new Color(vector.Z, vector.Y, vector.X);
        }

        public static Color FromHexString(string hex)
        {
            string colorStr = hex.TrimStart('#').ToLowerInvariant();
            Color instance = new Color(0, 0, 0);

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

        public bool Equals(Color other)
        {
            if (other == null)
            {
                return false;
            }
            return GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            Color other = obj as Color;
            if (other == null)
            {
                return false;
            }
            return Equals(other);
        }
    }

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/common/misc.py:528-572
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

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/common/misc.py:574-604
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

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/common/misc.py:606-624
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

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/common/misc.py:626-713
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

