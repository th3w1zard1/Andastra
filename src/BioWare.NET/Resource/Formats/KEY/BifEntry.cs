using System;

namespace BioWare.NET.Resource.Formats.KEY
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/key/key_data.py:65-148
    // Original: class BifEntry
    public class BifEntry : IEquatable<BifEntry>
    {
        public string Filename { get; set; }
        public int Filesize { get; set; }
        public ushort Drives { get; set; }

        public BifEntry()
        {
            Filename = string.Empty;
            Filesize = 0;
            Drives = 0;
        }

        public override bool Equals(object obj)
        {
            return obj is BifEntry other && Equals(other);
        }

        public bool Equals(BifEntry other)
        {
            if (other == null)
            {
                return false;
            }
            return Filename == other.Filename && Filesize == other.Filesize && Drives == other.Drives;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Filename, Filesize, Drives);
        }

        public override string ToString()
        {
            return string.Concat(Filename, "(", Filesize, " bytes)");
        }
    }
}

